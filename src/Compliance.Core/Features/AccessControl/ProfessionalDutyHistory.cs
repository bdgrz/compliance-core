using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Complete transient designation chronology at its actual hydrated physical/native prefix.</summary>
public static class ProfessionalDutyHistory
{
    public static async Task<ProfessionalDutyHistorySnapshot> ReadAsync(IDomainEventReader events,
        FirmProfessionalDutyDesignation owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var accepted = new Dictionary<Uuid, ProfessionalDutyChangeAccepted>();
        var terminal = new Dictionary<Uuid, DomainEvent>();
        var logical = new HashSet<Uuid>();
        var uses = new SourceUseHistoryValidation(owner.Stream);
        var digest = FirmIndependenceReplayDigest.Empty;
        ProfessionalDutyChangeAccepted? pending = null;
        DomainEvent? latest = null;
        ulong revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("A designation history contains another owner or repeated logical operation.");
            var operation = native.Operation;
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            if (operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected designation receipt differs from its full payload and metadata.");
            uses.ObserveOwnerFact(fact);
            if (SourceUseFactKinds.Classify(fact) is { } kind)
            {
                var source = (ISourceUseFact)fact;
                var sourceRevision = fact switch
                {
                    FirmProfessionalDutyDesignationEngagementUseAuthorized value => value.Revision,
                    FirmProfessionalDutyDesignationEngagementUseSettled value => value.Revision,
                    _ => -1,
                };
                if (source.Owner != owner.Stream || kind.Purpose != SourceUsePurpose.EngagementPersonalDecision ||
                    sourceRevision != (long)revision || kind.Granted && (latest is null || pending is not null))
                    throw Invalid("A designation use has another owner, purpose or held business revision.");
                uses.Observe(kind.Purpose, kind.Granted, source);
                continue;
            }
            switch (fact)
            {
                case ProfessionalDutyChangeAccepted value:
                    if (pending is not null || uses.Active.Count != 0 || value.Owner != owner.Stream ||
                        value.Revision != (long)revision || value.Generation != ProfessionalDutyEffects.Generation ||
                        value.FirstResponse is null || operation.Actor.Kind != "platform_operator" ||
                        operation.CommandKind is not (nameof(RecordFirmProfessionalDutyDesignation) or nameof(RevokeFirmProfessionalDutyDesignation)) ||
                        !NativeOwnerHistory.IsOwnRoot(owner, operation, operation.CommandKind, revision) ||
                        !accepted.TryAdd(operation.RootOperationId, value))
                        throw Invalid("A designation accepts one exact new signed change at its unheld predecessor revision.");
                    pending = value;
                    break;
                case ProfessionalDutyChangeRejected value:
                    if (pending is null || value.Owner != owner.Stream || value.Revision != (long)revision ||
                        value.Generation != ProfessionalDutyEffects.Generation || value.Intent != pending.Intent ||
                        !Child(operation, pending, nameof(RejectProfessionalDutyChange)) ||
                        !terminal.TryAdd(operation.RootOperationId, value))
                        throw Invalid("A rejection must terminate its exact pending designation change once.");
                    pending = null;
                    break;
                case FirmProfessionalDutyDesignationRecorded value:
                    Complete(value, value.OperationId, value.Intent, value.Revision, false);
                    break;
                case FirmProfessionalDutyDesignationRevoked value:
                    Complete(value, value.OperationId, value.Intent, value.Revision, true);
                    break;
                default:
                    throw Invalid("The designation stream contains an unsupported native fact.");
            }
        }
        if (revision != owner.Revision || pending != owner.Pending || latest != (owner.Latest as DomainEvent) ||
            owner.AcceptedFor(owner.SelectedRootId) != accepted.GetValueOrDefault(owner.SelectedRootId) ||
            owner.TerminalFor(owner.SelectedRootId) != terminal.GetValueOrDefault(owner.SelectedRootId))
            throw Invalid("The designation prefix differs from its compact current/selected authority.");
        try
        {
            owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        }
        catch (InvalidOperationException error)
        {
            throw Invalid($"The designation source-use prefix cannot be verified: {error.Message}");
        }
        owner.ReplayCut.Confirm(owner, digest);
        return new ProfessionalDutyHistorySnapshot(facts, accepted, terminal);

        void Complete(DomainEvent fact, Uuid id, Proofs.ImmutableContentRef intent, ulong next, bool revoking)
        {
            var native = (INativeOperationFact)fact;
            if (pending is null || id != native.Operation.RootOperationId || intent != pending.Intent ||
                next != revision + 1 || uses.Active.Count != 0 || revoking && latest is null ||
                pending.Operation.CommandKind != (revoking ? nameof(RevokeFirmProfessionalDutyDesignation) : nameof(RecordFirmProfessionalDutyDesignation)) ||
                !Child(native.Operation, pending, nameof(ApplyProfessionalDutyChange)) || (fact switch
                {
                    FirmProfessionalDutyDesignationRecorded value => value.OperationResult,
                    FirmProfessionalDutyDesignationRevoked value => value.OperationResult,
                    _ => null,
                }) is null ||
                !terminal.TryAdd(id, fact))
                throw Invalid("A designation applies its exact pending signed change once at the next business revision.");
            latest = fact;
            revision++;
            pending = null;
        }
    }

    static bool Child(OperationProvenance child, ProfessionalDutyChangeAccepted accepted, string kind) =>
        child.RootOperationId == accepted.Operation.RootOperationId && child.CausationEventId == accepted.Metadata.EventId &&
        child.CommandKind == kind && (child.FirstOwner ?? child.Owner) == accepted.Operation.Owner &&
        child.AffectedOwner == child.Owner && child.ExpectedRevisionOwner == child.Owner &&
        child.Actor == accepted.Operation.Actor && child.Episode == accepted.Operation.Episode &&
        child.OriginalExpectedRevision == accepted.Operation.OriginalExpectedRevision &&
        child.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256;

    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
