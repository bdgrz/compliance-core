using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>
///     Reads and writes the immutable content referenced by service epoch, engagement and preview facts. Every client
///     owner lives in the realm of its tenant, so its content does too. A synchronous command freezes the complete
///     resulting view once, as both the first response and the operation result.
/// </summary>
static class IndependenceContent
{
    public const string ServiceSchema = "NonattestServiceRecorded.ServiceContent.v1";
    public const string EvaluationSchema = "ClientIndependenceEvaluated.EvaluationContent.v1";
    public const string EngagementChangeSchema = "ServiceEngagementMutationRecorded.change.v1";
    public const string EngagementSchema = "ServiceEngagementMutationRecorded.OperationResult.v1";
    public const string RevocationSchema = "ServiceEngagementAssignmentRevoked.OperationResult.v1";
    public const string ResultSchema = "mutation-result-content.v1";

    /// <summary>The realm of every owner and content item of one client tenant.</summary>
    public static string Realm(Uuid tenantId) => tenantId.ToString();

    public static Task<ImmutableContentRef> WriteAsync<T>(IImmutableContentStore content, string realm, string schema,
        T value, CancellationToken ct) where T : notnull =>
        content.WriteJsonAsync(realm, Uuid.CreateVersion4(), schema, value, ct);

    /// <summary>Wraps the complete view content as the exact original typed result of a mutation.</summary>
    public static Task<ImmutableContentRef> WriteResultAsync(IImmutableContentStore content, string realm,
        string schema, ImmutableContentRef view, CancellationToken ct) =>
        content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ResultSchema,
            new MutationResultContent(schema, new StandardResponseBody(view)), ct);

    /// <summary>The first response of a 200 command: the complete view as standard content.</summary>
    public static FirstResponseDescriptor Ok(string schema, ImmutableContentRef view) => new(200,
        ImmutableContentStoreExtensions.JsonMediaType, schema, new StandardResponseBody(view));

    /// <summary>Reads the view a retained result content points at.</summary>
    public static async Task<T> ReadResultAsync<T>(IImmutableContentStore content, string realm,
        ImmutableContentRef result, CancellationToken ct) where T : notnull
    {
        var wrapper = await content.ReadJsonAsync<MutationResultContent>(realm, result, ct).ConfigureAwait(false);
        if (wrapper.Value is not StandardResponseBody body)
            throw new ImmutableContentIntegrityException("A client independence result is standard content.");
        return await content.ReadJsonAsync<T>(realm, body.Content, ct).ConfigureAwait(false);
    }

    /// <summary>The view of the original response of a committed or replayed 200 command.</summary>
    public static async ValueTask<Result<T>> ReadOutcomeAsync<T>(IImmutableContentStore content, string realm,
        Result<AcceptedAppendResult> outcome, CancellationToken ct) where T : notnull
    {
        if (!outcome.IsSuccess)
            return Result<T>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<T>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A client independence response is standard content.");
        return Result<T>.Success(await content.ReadJsonAsync<T>(realm, body.Content, ct).ConfigureAwait(false));
    }

    /// <summary>The 202 acknowledgment of a committed or replayed admission.</summary>
    public static async ValueTask<Result<OperationAcceptedResponse>> ReadAcknowledgmentAsync(
        IImmutableContentStore content, string realm, Result<AcceptedAppendResult> outcome, CancellationToken ct) =>
        await ReadOutcomeAsync<OperationAcceptedResponse>(content, realm, outcome, ct).ConfigureAwait(false);

    public static Result<T> Failure<T>(RequestErrorKind kind, string message, bool transient = false) =>
        Result<T>.Failure(new RequestError(kind, message, transient));
}
