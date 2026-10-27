using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work.Readers;

/// <summary>Composes full read history; native original-receipt decisions never read unrelated successor bodies.</summary>
public sealed class WorkAssignmentReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content, LegacyWorkAssignmentHistory legacy)
{
    public async Task<WorkAssignmentReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid workItemId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new WorkItemAssignment(tenantId, programId, workItemId), ct).ConfigureAwait(false);
        return await ReadAsync(owner, ct).ConfigureAwait(false);
    }

    public async Task<WorkAssignmentReadSnapshot> ReadAsync(WorkItemAssignment owner, CancellationToken ct = default)
    {
        var facts = await WorkAssignmentNativeHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        var history = new List<WorkAssignmentEntryView>();
        var changes = new List<WorkAssignmentChangeContent>();
        Uuid? assignee = null;
        var escalated = false;
        foreach (var fact in facts)
        {
            if (fact is WorkAssignmentLegacyHistoryImported imported)
            {
                var frozen = await content.ReadJsonAsync<WorkAssignmentLegacyContent>(owner.Stream.Realm, imported.History, ct).ConfigureAwait(false);
                if (frozen.State is null || frozen.Proof is null || frozen.State.History is null)
                    throw Invalid("The immutable legacy history is missing required state or source proof.");
                await legacy.VerifyAsync(owner.TenantId, owner.ProgramId, owner.WorkItemId, frozen, ct).ConfigureAwait(false);
                if (frozen.State.Revision != imported.Revision || frozen.State.AssigneeMemberId != imported.AssigneeMemberId ||
                    frozen.State.Escalated != imported.Escalated)
                    throw Invalid("The immutable legacy history differs from its compact imported current authority.");
                history.AddRange(frozen.State.History);
                (assignee, escalated) = (frozen.State.AssigneeMemberId, frozen.State.Escalated);
                continue;
            }
            var native = (IWorkAssignmentFact)fact;
            var change = await ReadChangeAsync(owner, native, ct).ConfigureAwait(false);
            var entry = change.Entry;
            if (entry.Action == WorkItemAssignment.Escalate)
            {
                if (entry.AssigneeMemberId is not null || entry.PreviousAssigneeMemberId is not null || escalated)
                    throw Invalid("An escalation preserves assignment and happens only once.");
                escalated = true;
            }
            else
            {
                if (entry.AssigneeMemberId != native.AssigneeMemberId || assignee is not null && entry.PreviousAssigneeMemberId != assignee)
                    throw Invalid("The attributed assignment differs from its recorded predecessor or current assignee.");
                assignee = entry.AssigneeMemberId;
            }
            history.Add(entry);
            changes.Add(change);
            var state = new WorkAssignmentState(entry.Revision, assignee, escalated, history.ToArray());
            var envelope = await content.ReadJsonAsync<MutationResultContent>(owner.Stream.Realm, native.OperationResult!, ct).ConfigureAwait(false);
            if (envelope.Schema != WorkAssignmentOperations.ResultSchema || envelope.Value is not StandardResponseBody body)
                throw Invalid("The original assignment result must retain its exact typed standard envelope.");
            var first = await FirstResponses.ReplayAsync(content, owner.Stream.Realm, native.FirstResponse!, ct).ConfigureAwait(false);
            if (first.HttpStatus != 200 || first.ResultSchema != WorkAssignmentOperations.ResultSchema ||
                first.Body is not StandardResponseBody firstBody || firstBody.Content != body.Content)
                throw Invalid("The first response descriptor differs from its exact original result body.");
            var original = await content.ReadJsonAsync<WorkItemDetailView>(owner.Stream.Realm, body.Content, ct).ConfigureAwait(false);
            var expected = WorkQueueReader.Compose(change.Source.Describe(), state, change.EffectiveAssigneeMemberId, change.SelectionDate);
            if (original.Item != expected || !original.History.SequenceEqual(history))
                throw Invalid("The original result differs from its exact captured source, attribution and complete history prefix.");
        }
        if (history.Count != owner.Revision || assignee != owner.AssigneeMemberId || escalated != owner.Escalated)
            throw Invalid("The complete external history differs from current native accountability.");
        return new WorkAssignmentReadSnapshot(owner, new WorkAssignmentState(owner.Revision, assignee, escalated, history), changes);
    }

    public async Task<WorkAssignmentChangeContent> ReadChangeAsync(WorkItemAssignment owner, IWorkAssignmentFact fact, CancellationToken ct = default)
    {
        if (fact.Change.Schema != WorkAssignmentChangeContent.Schema)
            throw Invalid("The original assignment change has another immutable schema.");
        var value = await content.ReadJsonAsync<WorkAssignmentChangeContent>(owner.Stream.Realm, fact.Change, ct).ConfigureAwait(false);
        if (value.Source is null || value.Entry is null || value.Source.TenantId != owner.TenantId || value.Source.ProgramId != owner.ProgramId ||
            value.Source.WorkItemId != owner.WorkItemId || value.Source.SourceId == Uuid.Empty || value.Source.Responsible is null ||
            value.Source.Excluded is null || string.IsNullOrWhiteSpace(value.Source.Kind) || value.Entry.Revision != fact.Revision || value.Entry.By != fact.Operation.Actor ||
            value.Entry.At != fact.Operation.AcceptedAt || !ActionMatches(fact.Operation.CommandKind, value.Entry.Action))
            throw Invalid("The captured assignment source and attributed change differ from their actual native receipt.");
        return value;
    }

    public Task<WorkAssignmentLegacyContent> ReadLegacyAsync(Uuid tenantId, Uuid programId, Uuid workItemId, CancellationToken ct) =>
        legacy.ReadAsync(tenantId, programId, workItemId, ct);

    internal static bool ActionMatches(string command, string action) => command switch
    {
        nameof(ClaimWorkItem) => action == WorkItemAssignment.Claim,
        nameof(AssignWorkItem) => action is WorkItemAssignment.Assign or WorkItemAssignment.Reassign,
        nameof(DelegateWorkItem) => action == WorkItemAssignment.Delegate,
        nameof(EscalateWorkItem) => action == WorkItemAssignment.Escalate,
        _ => false,
    };
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
