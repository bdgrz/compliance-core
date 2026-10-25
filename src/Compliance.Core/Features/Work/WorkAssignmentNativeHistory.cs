using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Verifies compact authority and all native receipts without reading unrelated historical body blobs.</summary>
public static class WorkAssignmentNativeHistory
{
    public static async Task<IReadOnlyList<DomainEvent>> ReadAsync(IDomainEventReader events,
        WorkItemAssignment owner, CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var logical = new HashSet<Uuid>();
        var digest = FirmIndependenceReplayDigest.Empty;
        long revision = 0;
        Uuid? assignee = null;
        var escalated = false;
        WorkAssignmentLegacyHistoryImported? imported = null;
        IWorkAssignmentFact? latest = null;
        foreach (var fact in facts)
        {
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            if (fact is WorkAssignmentLegacyHistoryImported legacy)
            {
                if (revision != 0 || imported is not null || legacy.TenantId != owner.TenantId || legacy.ProgramId != owner.ProgramId ||
                    legacy.WorkItemId != owner.WorkItemId || legacy.Revision <= 0 || legacy.ImportedByCommandId == Uuid.Empty ||
                    legacy.History is null || legacy.History.Schema != WorkAssignmentLegacyContent.Schema)
                    throw Invalid("Legacy import differs from its single empty native owner and exact schema.");
                imported = legacy;
                (revision, assignee, escalated) = (legacy.Revision, legacy.AssigneeMemberId, legacy.Escalated);
                continue;
            }
            if (fact is not IWorkAssignmentFact native || native.TenantId != owner.TenantId || native.ProgramId != owner.ProgramId ||
                native.WorkItemId != owner.WorkItemId || native.Revision != revision + 1 ||
                native.FirstResponse is null || native.OperationResult is null || native.Change is null || native.Change.Schema != WorkAssignmentChangeContent.Schema ||
                !NativeOwnerHistory.IsOwnRoot(owner, native.Operation, native.Operation.CommandKind, (ulong)revision) ||
                !logical.Add(native.Operation.LogicalId) ||
                native.Operation.CommandKind is not (nameof(AssignWorkItem) or nameof(ClaimWorkItem) or nameof(DelegateWorkItem) or nameof(EscalateWorkItem)) ||
                native is WorkItemAssignmentEscalated && (native.Operation.CommandKind != nameof(EscalateWorkItem) || escalated || !native.Escalated || native.AssigneeMemberId != assignee) ||
                native is WorkItemAssignmentRecorded && (native.Operation.CommandKind == nameof(EscalateWorkItem) || native.AssigneeMemberId is null || native.Escalated != escalated) ||
                latest is null && imported is not null && imported.ImportedByCommandId != native.Operation.LogicalId)
                throw Invalid("The native work assignment history differs from its exact owner, original revision, command or receipt.");
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected assignment receipt differs from exact payload and complete metadata.");
            (revision, assignee, escalated, latest) = (native.Revision, native.AssigneeMemberId, native.Escalated, native);
        }
        if (revision != owner.Revision || assignee != owner.AssigneeMemberId || escalated != owner.Escalated ||
            imported != owner.LegacyImport || !Equals(latest, owner.Latest) || imported is not null && latest is null)
            throw Invalid("The complete native history differs from compact current assignment authority.");
        owner.ReplayCut.Confirm(owner, digest);
        return facts;
    }
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
