using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.UserIdentities.Handlers;
namespace Bdgrz.Compliance.Features.SourceUse.Processes;

/// <summary>Shared provenance and closed results of the generic source-use receivers.</summary>
public static class SourceUseReceiverSupport
{
    /// <summary>
    ///     Provenance of a receiver fact: it copies the root header of the frozen intent and names the receiver's own
    ///     stream as emitting, affected and expected-revision owner.
    /// </summary>
    public static OperationProvenance Child(Uuid commandId, string commandKind, Uuid causationEventId,
        DurableIntentHeader header, EventStreamAddress owner, DateTimeOffset acceptedAt)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(owner);
        return new OperationProvenance(commandId, header.RootOperationId, causationEventId, commandKind,
            header.OriginalActor, owner, header.Episode, header.OriginalExpectedRevision, header.AuthoredIntentSha256,
            acceptedAt, owner, owner, header.FirstOwner);
    }

    /// <summary>The closed result of a receiver that proved its operation is a terminal no-op.</summary>
    public static Result<ReceiverOutcome> Superseded(string reason, NativeFactReference? evidence) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Superseded(reason, evidence));

    /// <summary>The closed result of a receiver that appended its fact.</summary>
    public static Result<ReceiverOutcome> Applied(NativeFactReference fact) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(fact));

    /// <summary>The closed result of a receiver that found its exact fact already accepted.</summary>
    public static Result<ReceiverOutcome> Replayed(NativeFactReference fact) =>
        Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(fact));

    /// <summary>A changed intent or invalid proof: a permanent conflict that quarantines the process.</summary>
    public static Result<ReceiverOutcome> Permanent(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, false));

    /// <summary>A dependency or projection that is not visible yet: the process retries the step.</summary>
    public static Result<ReceiverOutcome> Transient(string message) =>
        Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, message, true));
}
