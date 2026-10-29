using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.NativeOperations.Services;

/// <summary>
/// Writes, reads and freezes the immutable content that native facts and first responses reference; the realm is
/// the tenant. Every value is the complete typed structure, so no list is capped by an event size, and a frozen
/// response replays exactly after the owner changed.
/// </summary>
public static class NativeContent
{
    /// <summary>The content realm of a tenant.</summary>
    public static string Realm(Uuid tenantId) => tenantId.ToString();

    /// <summary>Publishes the value as immutable JSON content under a fresh content id.</summary>
    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, Uuid tenantId, string schema,
        T value, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), schema, value, ct);
    }

    /// <summary>Reads verified immutable JSON content.</summary>
    public static Task<T> ReadAsync<T>(IImmutableContentStore content, Uuid tenantId, ImmutableContentRef reference,
        CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(content);
        return content.ReadJsonAsync<T>(Realm(tenantId), reference, ct);
    }

    /// <summary>Freezes a typed 200 response: the body content, the mutation result wrapping it and the descriptor.</summary>
    public static async Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeAsync<T>(
        IImmutableContentStore content, Uuid tenantId, string schema, T value, CancellationToken ct = default)
        where T : notnull
    {
        var body = await WriteAsync(content, tenantId, schema, value, ct).ConfigureAwait(false);
        var result = await content.WriteJsonAsync(Realm(tenantId), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(schema, new StandardResponseBody(body)), ct).ConfigureAwait(false);
        return (new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, schema,
            new StandardResponseBody(body)), result);
    }

    /// <summary>Freezes the 200 response of an operation that returns no value.</summary>
    public static Task<(FirstResponseDescriptor Response, ImmutableContentRef Result)> FreezeUnitAsync(
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct = default) =>
        FreezeAsync(content, tenantId, UnitResult.Schema, new UnitResult(), ct);

    /// <summary>The typed value of an accepted or replayed 200 admission, read from its frozen first response.</summary>
    public static async Task<Result<T>> ResponseAsync<T>(IImmutableContentStore content, Uuid tenantId,
        Result<AcceptedAppendResult> outcome, CancellationToken ct = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!outcome.IsSuccess)
            return Result<T>.Failure(outcome.Error!);
        var descriptor = DescriptorOf(outcome.Value);
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A native 200 response is standard content.");
        return Result<T>.Success(await ReadAsync<T>(content, tenantId, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The outcome of an accepted or replayed admission whose declared result is Unit.</summary>
    public static Result UnitOutcome(Result<AcceptedAppendResult> outcome)
    {
        if (!outcome.IsSuccess)
            return Result.Failure(outcome.Error!);
        return DescriptorOf(outcome.Value) is null ? Result.Failure(AcceptedAppendResult.ConflictError) : Result.Success;
    }

    static FirstResponseDescriptor? DescriptorOf(AcceptedAppendResult? accepted) => accepted switch
    {
        AcceptedAppendResult.Committed committed => committed.Response,
        AcceptedAppendResult.Replayed replayed => replayed.Response,
        _ => null,
    };
}
