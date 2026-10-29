using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.History;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.History;

/// <summary>Verifies complete practice reservation/fence/commitment chronology at an exact hydrated cut.</summary>
public static class PracticeCommitmentHistory
{
    public static async Task<PracticeCommitmentHistorySnapshot> ReadAsync(IDomainEventReader events,
        PracticeIndependenceCommitment owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var commitments = new Dictionary<SourceUseSlot, PracticeIndependenceCommitted>();
        var ordered = new List<PracticeIndependenceCommitted>();
        var uses = new SourceUseHistoryValidation(owner.Stream);
        var logical = new HashSet<Uuid>();
        var digest = FirmIndependenceReplayDigest.Empty;
        string? practice = null;
        long revision = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logical.Add(native.Operation.LogicalId))
                throw Invalid("A practice history contains another owner or repeated logical receiver operation.");
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected practice receipt differs from its exact native payload and complete metadata.");
            uses.ObserveOwnerFact(fact);
            switch (fact)
            {
                case PracticeIndependenceReserved value:
                    if (value.Owner != owner.Stream || value.Revision != revision || value.EngagementId == Uuid.Empty ||
                        value.Practice is not (PracticeIndependenceCommitment.Advisory or PracticeIndependenceCommitment.Attest) ||
                        practice is not null && practice != value.Practice ||
                        uses.Active.Values.Select(entry => entry.GrantFact).OfType<PracticeIndependenceReserved>()
                            .Any(other => other.Practice != value.Practice))
                        throw Invalid("A reservation contradicts its exact subject, business revision or permanent/live practice.");
                    uses.Observe(SourceUsePurpose.ActualStaffAssignment, true, value);
                    break;
                case PracticeIndependenceCommitted value:
                    var slot = SourceUseSlot.Of(owner.Stream, SourceUsePurpose.ActualStaffAssignment, value.Operation, value.Generation);
                    if (value.Owner != owner.Stream || value.Revision != revision + 1 ||
                        !uses.Active.TryGetValue(slot, out var held) || held.GrantFact is not PracticeIndependenceReserved grant ||
                        value.Practice != grant.Practice || value.EngagementId != grant.EngagementId ||
                        practice is not null && practice != value.Practice || !SameRoot(value.Operation, grant.Operation) ||
                        !commitments.TryAdd(slot, value))
                        throw Invalid("A permanent commitment must follow its exact live full-key reservation once.");
                    practice = value.Practice;
                    revision++;
                    ordered.Add(value);
                    break;
                case PracticeIndependenceReservationSettled value:
                    if (value.Owner != owner.Stream || value.Revision != revision)
                        throw Invalid("A practice settlement belongs to its exact subject and current business revision.");
                    uses.Observe(SourceUsePurpose.ActualStaffAssignment, false, value);
                    break;
                default:
                    throw Invalid("The practice stream contains an unsupported native fact.");
            }
        }
        var live = commitments.Where(pair => uses.Active.ContainsKey(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
        if ((ulong)revision != owner.Revision || practice != owner.Practice || ordered.FirstOrDefault() != owner.FirstCommitment ||
            ordered.LastOrDefault() != owner.LatestCommitment || live.Count != owner.LiveCommitted.Count ||
            live.Any(pair => !owner.LiveCommitted.TryGetValue(pair.Key, out var value) || value != pair.Value) ||
            owner.SelectedReservation is { } key && owner.CommitmentOf(key) != commitments.GetValueOrDefault(SourceUseSlot.Of(owner.Stream, key)))
            throw Invalid("The complete practice prefix differs from its compact permanent/live/selected authority.");
        try
        {
            owner.Uses.Confirm(uses, owner.Version, owner.CommittedStreamPosition);
        }
        catch (InvalidOperationException error)
        {
            throw Invalid($"The exact practice source-use history cannot be verified: {error.Message}");
        }
        owner.ReplayCut.Confirm(owner, digest);
        return new PracticeCommitmentHistorySnapshot(owner, ordered, commitments);
    }

    static bool SameRoot(OperationProvenance terminal, OperationProvenance source) =>
        terminal.RootOperationId == source.RootOperationId && (terminal.FirstOwner ?? terminal.Owner) == (source.FirstOwner ?? source.Owner) &&
        terminal.Actor == source.Actor && terminal.Episode == source.Episode && terminal.OriginalExpectedRevision == source.OriginalExpectedRevision &&
        terminal.AuthoredIntentSha256 == source.AuthoredIntentSha256 && terminal.AffectedOwner == source.AffectedOwner &&
        terminal.ExpectedRevisionOwner == source.ExpectedRevisionOwner;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
