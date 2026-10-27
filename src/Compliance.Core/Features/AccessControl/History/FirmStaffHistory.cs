using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.History;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.History;

/// <summary>Complete transient staff chronology at the exact hydrated physical and native cut.</summary>
public static class FirmStaffHistory
{
    public static async Task<FirmStaffHistorySnapshot> ReadAsync(IDomainEventReader events, FirmStaffRecord owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var digest = FirmIndependenceReplayDigest.Empty;
        var logical = new HashSet<Uuid>();
        var changes = new List<FirmStaffChangeRecorded>();
        var uses = new SourceUseHistoryValidation(owner.Stream);
        FirmStaffRegistrationAccepted? accepted = null;
        FirmStaffRegistrationRejected? rejected = null;
        FirmStaffChangeRecorded? latest = null;
        ulong revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("A staff history contains another owner or repeated logical operation.");
            var operation = native.Operation;
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected staff acceptance differs from the exact payload and complete metadata.");
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } kind)
            {
                var source = (ISourceUseFact)fact;
                if (source.Owner != owner.Stream || kind.Granted && latest is null ||
                    SourceRevision(fact) != (long)revision ||
                    kind.Purpose is not (SourceUsePurpose.ProfessionalDutyRecording or SourceUsePurpose.EngagementPersonalDecision))
                    throw Invalid("A staff use has another owner, purpose or business revision.");
                uses.Observe(kind.Purpose, kind.Granted, source);
                continue;
            }
            switch (fact)
            {
                case FirmStaffRegistrationAccepted value:
                    if (accepted is not null || revision != 0 || value.Owner != owner.Stream || value.Revision != 0 ||
                        value.Generation != FirmStaffEffects.Generation || value.FirstResponse is null ||
                        operation.LogicalId != owner.StaffMemberId ||
                        !NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RegisterFirmStaff), 0) ||
                        operation.Actor.Kind != "platform_operator")
                        throw Invalid("A staff registration must be its exact first root admission.");
                    accepted = value;
                    break;
                case FirmStaffRegistrationRejected value:
                    if (accepted is null || rejected is not null || latest is not null || value.Owner != owner.Stream ||
                        value.Revision != 0 || value.Generation != FirmStaffEffects.Generation || value.Intent != accepted.Intent ||
                        !Child(operation, accepted, nameof(RejectFirmStaffRegistration)))
                        throw Invalid("A staff rejection must close its exact pending registration.");
                    rejected = value;
                    break;
                case FirmStaffChangeRecorded value:
                    if (accepted is null || rejected is not null || value.Intent != accepted.Intent ||
                        value.Revision != revision + 1 || value.OperationResult is null)
                        throw Invalid("A staff change must follow its exact accepted registration and previous revision.");
                    if (value.ChangeKind == "register")
                    {
                        if (latest is not null || value.OperationId != owner.StaffMemberId ||
                            !Child(operation, accepted, nameof(CompleteFirmStaffRegistration)))
                            throw Invalid("A staff record completes its accepted registration once.");
                    }
                    else if (value.ChangeKind != "status" || latest is null || value.OperationId != operation.LogicalId ||
                             value.FirstResponse is null || uses.Active.Count != 0 ||
                             !NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(SetFirmStaffStatus), revision) ||
                             operation.Actor.Kind != "platform_operator")
                        throw Invalid("A staff status change requires its own root and unheld predecessor revision.");
                    latest = value;
                    revision++;
                    changes.Add(value);
                    break;
                default:
                    throw Invalid("The staff stream contains an unsupported native fact.");
            }
        }
        if (revision != owner.Revision || accepted != owner.Accepted || (rejected is not null) != owner.IsRejected || latest != owner.Latest)
            throw Invalid("The staff history differs from its compact registration and current authority.");
        try
        {
            owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        }
        catch (InvalidOperationException error)
        {
            throw Invalid($"The staff source-use prefix cannot be verified: {error.Message}");
        }
        owner.ReplayCut.Confirm(owner, digest);
        return new FirmStaffHistorySnapshot(changes);
    }

    internal static async Task VerifyClaimAsync(IDomainEventReader events, FirmStaffIdentityClaim owner,
        CancellationToken ct)
    {
        var facts = await NativeOwnerHistory.ReadAsync<FirmStaffIdentityClaimed>(events, owner, ct).ConfigureAwait(false);
        if (facts.Count == 0 && owner.Claimed is null)
            return;
        if (facts.Count != 1 || owner.Claimed is not { } claimed || facts[0] != claimed ||
            facts[0].Metadata != claimed.Metadata || claimed.Owner != owner.Stream || claimed.Revision != 1 ||
            claimed.Generation != FirmStaffEffects.Generation || claimed.Operation.RootOperationId == Uuid.Empty ||
            claimed.Operation.Owner != owner.Stream || claimed.Operation.Actor.Kind != "platform_operator" ||
            claimed.Operation.CommandKind != nameof(ClaimFirmStaffIdentity) || claimed.Operation.CausationEventId is null ||
            claimed.Operation.FirstOwner != new FirmStaffRecord(claimed.Operation.RootOperationId).Stream ||
            claimed.Operation.AffectedOwner != owner.Stream || claimed.Operation.ExpectedRevisionOwner != owner.Stream ||
            claimed.Operation.OriginalExpectedRevision != 0)
            throw Invalid("The permanent canonical-user claim differs from its exact committed registration child fact.");
    }

    static bool Child(OperationProvenance child, FirmStaffRegistrationAccepted accepted, string kind) =>
        child.RootOperationId == accepted.Operation.RootOperationId && child.CausationEventId == accepted.Metadata.EventId &&
        child.CommandKind == kind && (child.FirstOwner ?? child.Owner) == accepted.Operation.Owner &&
        child.AffectedOwner == child.Owner && child.ExpectedRevisionOwner == child.Owner &&
        child.Actor == accepted.Operation.Actor && child.Episode == accepted.Operation.Episode &&
        child.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        child.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256;

    static long SourceRevision(DomainEvent fact) => fact switch
    {
        FirmStaffProfessionalDutyUseAuthorized value => value.Revision,
        FirmStaffProfessionalDutyUseSettled value => value.Revision,
        FirmStaffRecordEngagementUseAuthorized value => value.Revision,
        FirmStaffRecordEngagementUseSettled value => value.Revision,
        _ => -1,
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
