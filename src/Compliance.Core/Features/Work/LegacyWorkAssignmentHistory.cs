using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads legacy v1 program ledger only; it never grants new authority or writes that ledger.</summary>
public sealed class LegacyWorkAssignmentHistory(IAggregateReader reader, IDomainEventReader events)
{
    public async Task<WorkAssignmentLegacyContent> ReadAsync(Uuid tenantId, Uuid programId, Uuid workItemId, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new LegacyWorkAssignmentLedger(tenantId, programId), ct).ConfigureAwait(false);
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var (state, digest) = Compose(facts, tenantId, programId, workItemId);
        return new WorkAssignmentLegacyContent(state, new WorkAssignmentLegacyProof(owner.Stream, owner.Version,
            owner.CommittedStreamPosition, digest));
    }

    public async Task VerifyAsync(Uuid tenantId, Uuid programId, Uuid workItemId, WorkAssignmentLegacyContent value, CancellationToken ct)
    {
        var expected = new LegacyWorkAssignmentLedger(tenantId, programId);
        if (value.Proof.Stream != expected.Stream)
            throw new ImmutableContentIntegrityException("Imported legacy history names another program ledger.");
        var facts = new List<DomainEvent>();
        var ids = new HashSet<Uuid>();
        ulong offset = 0;
        ulong version = 0;
        await foreach (var record in events.ReadAsync(expected.Stream, 0, ct).ConfigureAwait(false))
        {
            if (offset == value.Proof.Through)
                break;
            if (record.Stream != expected.Stream || record.ResourceOffset != offset || record.Event.Metadata.AggregateId != expected.Id ||
                record.Event.Metadata.EventId == Uuid.Empty || !ids.Add(record.Event.Metadata.EventId))
                throw new ImmutableContentIntegrityException("The imported legacy source differs from its exact physical tuple.");
            offset++;
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event.Metadata.AggregateVersion != ++version)
                throw new ImmutableContentIntegrityException("The imported legacy source differs from its exact native sequence.");
            facts.Add(record.Event);
            if (offset == value.Proof.Through)
                break;
        }
        if (offset != value.Proof.Through || version != value.Proof.Version)
            throw new ImmutableContentUnavailableException("The exact imported legacy history cut is not visible yet.");
        var (state, digest) = Compose(facts, tenantId, programId, workItemId);
        if (digest != value.Proof.PrefixSha256 || state.Revision != value.State.Revision || state.AssigneeMemberId != value.State.AssigneeMemberId ||
            state.Escalated != value.State.Escalated || !state.History.SequenceEqual(value.State.History))
            throw new ImmutableContentIntegrityException("The imported immutable history differs from its actual legacy source prefix.");
    }

    static (WorkAssignmentState State, string Digest) Compose(IReadOnlyList<DomainEvent> facts, Uuid tenant, Uuid program, Uuid item)
    {
        var history = new List<WorkAssignmentEntryView>();
        var revisions = new Dictionary<Uuid, long>();
        var digest = FirmIndependenceReplayDigest.Empty;
        Uuid? assignee = null;
        var escalated = false;
        foreach (var fact in facts)
        {
            digest = FirmIndependenceReplayDigest.Append(digest, fact);
            var (t, p, id, entry) = fact switch
            {
                WorkItemAssigned assigned => (assigned.TenantId, assigned.ProgramId, assigned.WorkItemId,
                    new WorkAssignmentEntryView(assigned.Revision, assigned.Action, assigned.AssigneeMemberId,
                        assigned.PreviousAssigneeMemberId, assigned.Reason, assigned.AssignedBy, assigned.AssignedAt)),
                WorkItemEscalated raised => (raised.TenantId, raised.ProgramId, raised.WorkItemId,
                    new WorkAssignmentEntryView(raised.Revision, WorkItemAssignment.Escalate, null, null, raised.Reason, raised.EscalatedBy, raised.EscalatedAt)),
                _ => throw new ImmutableContentIntegrityException("The legacy ledger contains an unsupported event shape."),
            };
            if (t != tenant || p != program || entry.Revision != revisions.GetValueOrDefault(id) + 1)
                throw new ImmutableContentIntegrityException("Legacy work assignment history has another scope or revision.");
            revisions[id] = entry.Revision;
            if (id != item)
                continue;
            history.Add(entry);
            if (entry.Action == WorkItemAssignment.Escalate)
                escalated = true;
            else
                assignee = entry.AssigneeMemberId;
        }
        return (new WorkAssignmentState(history.Count, assignee, escalated, history), digest);
    }
}
