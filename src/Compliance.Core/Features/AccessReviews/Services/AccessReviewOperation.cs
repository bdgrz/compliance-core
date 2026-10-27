using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
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
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Services;

/// <summary>Shared construction of the provenance and first-response guards of access review root operations.</summary>
static class AccessReviewOperation
{
    /// <summary>The episode of every first-owner access review operation.</summary>
    public const string InitialEpisode = "initial";

    /// <summary>The provenance of a root access review operation on its own owner stream.</summary>
    public static OperationProvenance Provenance(Uuid logicalId, string commandKind, EventStreamAddress owner,
        ActorReference actor, long? expectedRevision, string intentDigest, DateTimeOffset acceptedAt) => new(
        logicalId, logicalId, null, commandKind, actor, owner, InitialEpisode,
        expectedRevision is { } expected ? (ulong)expected : null, intentDigest, acceptedAt, owner, owner, owner);

    /// <summary>Turns a passed guard and the prepared response into the first response, or the guard failure.</summary>
    public static Result<FirstResponseDescriptor> Prepare(Result guard, FirstResponseDescriptor? response,
        string changedMessage)
    {
        if (!guard.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(guard.Error!);
        return response is null
            ? Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, changedMessage,
                true))
            : Result<FirstResponseDescriptor>.Success(response);
    }

    /// <summary>A stale owner revision as a conflict naming the current revision.</summary>
    public static Result Stale(string record, long currentRevision) => Result.Failure(
        VersionedRecordRules.StaleRevision(record, currentRevision).ToRequestError());

    public static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
