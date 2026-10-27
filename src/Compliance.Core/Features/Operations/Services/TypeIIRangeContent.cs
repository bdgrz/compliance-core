using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>Reads and writes the immutable content referenced by Type II range facts; the realm is the tenant.</summary>
static class TypeIIRangeContent
{
    /// <summary>Where a caller reads the status of an accepted operation.</summary>
    public const string StatusUrlTemplate = "/api/v1/platform/operations/{operation_id}";

    public static Task<TypeIIRangeIntent> ReadIntentAsync(IImmutableContentStore content, string realm,
        ImmutableContentRef reference, CancellationToken ct) =>
        content.ReadJsonAsync<TypeIIRangeIntent>(realm, reference, ct);

    /// <summary>Writes the Unit result content a terminal fact references as its operation result.</summary>
    public static async Task<ImmutableContentRef> UnitResultAsync(IImmutableContentStore content, string realm,
        Uuid rootOperationId, CancellationToken ct)
    {
        var unit = await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, UnitResult.Schema),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return await content.WriteJsonAsync(realm, Uuid.CreateVersion5(rootOperationId, "mutation-result-content.v1"),
            "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema, new StandardResponseBody(unit)),
            ct).ConfigureAwait(false);
    }

    /// <summary>The frozen 202 first response of a root admission.</summary>
    public static async Task<FirstResponseDescriptor> AcceptedResponseAsync(IImmutableContentStore content,
        string realm, Uuid operationId, DateTimeOffset acceptedAt, CancellationToken ct)
    {
        var acknowledgment = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(),
            "operation-accepted-response.v1", new OperationAcceptedResponse(operationId,
                StatusUrlTemplate.Replace("{operation_id}", operationId.ToString(), StringComparison.Ordinal),
                acceptedAt), ct).ConfigureAwait(false);
        return new FirstResponseDescriptor(202, ImmutableContentStoreExtensions.JsonMediaType,
            "operation-accepted-response.v1", new StandardResponseBody(acknowledgment));
    }

    /// <summary>The 202 acknowledgment of an accepted or replayed admission, read from the frozen first response.</summary>
    public static async Task<Result<OperationAcceptedResponse>> AcknowledgmentAsync(IImmutableContentStore content,
        string realm, Result<AcceptedAppendResult> outcome, CancellationToken ct)
    {
        if (!outcome.IsSuccess)
            return Result<OperationAcceptedResponse>.Failure(outcome.Error!);
        var descriptor = outcome.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null,
        };
        if (descriptor is null)
            return Result<OperationAcceptedResponse>.Failure(AcceptedAppendResult.ConflictError);
        if (descriptor.Body is not StandardResponseBody body)
            throw new InvalidOperationException("A Type II range acknowledgment is standard content.");
        return Result<OperationAcceptedResponse>.Success(await content
            .ReadJsonAsync<OperationAcceptedResponse>(realm, body.Content, ct).ConfigureAwait(false));
    }
}
