using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Verifies full native request history outside bounded command authority.</summary>
public static class AuditorRequestHistory
{
    public static async Task<AuditorRequestHistorySnapshot> ReadAsync(IDomainEventReader events, AuditorRequest owner,
        CancellationToken ct = default)
    {
        var facts = await NativeOwnerHistory.ReadAsync<DomainEvent>(events, owner, ct).ConfigureAwait(false);
        var digest = AuditorRequestReplayDigest.Empty;
        var logicalIds = new HashSet<Uuid>();
        var responses = new HashSet<Uuid>();
        var comments = new HashSet<Uuid>();
        var selections = new Dictionary<Uuid, AuditorSampleSelectionAdmitted>();
        AuditorRequestResolutionAccepted? pending = null;
        AuditorRequestResolved? resolved = null;
        ImmutableContentRef? terms = null;
        long revision = 0;
        long generation = 0;
        foreach (var fact in facts)
        {
            if (fact is not INativeOperationFact native || native.Operation.Owner != owner.Stream ||
                native.Operation.LogicalId == Uuid.Empty || !logicalIds.Add(native.Operation.LogicalId) ||
                native.Operation.Actor is not { Kind: "member" } actor ||
                !Uuid.TryParse(actor.Id, CultureInfo.InvariantCulture, out var memberId) || memberId == Uuid.Empty)
                throw Invalid("A request history contains another owner or repeats a logical operation.");
            digest = AuditorRequestReplayDigest.Append(digest, fact);
            if (native.Operation.LogicalId == owner.Acceptance.SelectedLogicalId &&
                (owner.Acceptance.SelectedFact is not { } selected || selected.GetType() != fact.GetType() ||
                    !selected.Equals(fact) || selected.Metadata != fact.Metadata))
                throw Invalid("The selected request acceptance differs from its actual native payload and complete metadata.");
            var operation = native.Operation;
            var writable = revision > 0 && pending is null && resolved is null;
            bool valid;
            switch (fact)
            {
                case AuditorRequestRecorded recorded:
                    valid = revision == 0 && recorded.TenantId == owner.TenantId && recorded.SubjectId == owner.Id &&
                        recorded.Revision == 1 && operation.LogicalId == owner.Id &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RecordAuditorRequest), null);
                    generation = 1;
                    terms = recorded.Change;
                    break;
                case AuditorRequestRevised revised:
                    valid = writable && revised.TenantId == owner.TenantId && revised.SubjectId == owner.Id &&
                        revised.Revision == revision + 1 &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReviseAuditorRequest), (ulong)revision);
                    terms = revised.Change;
                    break;
                case AuditorRequestAssigned assigned:
                    valid = writable && assigned.TenantId == owner.TenantId && assigned.SubjectId == owner.Id &&
                        assigned.Revision == revision + 1 &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(AssignAuditorRequest), (ulong)revision);
                    terms = assigned.Change;
                    break;
                case AuditorResponseAdmitted response:
                    valid = writable && Admission(response.Owner, response.Revision, response.Generation) &&
                        responses.Add(operation.LogicalId) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(CreateAuditResponseDraft), (ulong)revision);
                    break;
                case AuditorCommentAdmitted comment:
                    valid = writable && Admission(comment.Owner, comment.Revision, comment.Generation) &&
                        comments.Add(operation.LogicalId) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(RecordAuditorRequestComment), (ulong)revision);
                    break;
                case AuditorSampleSelectionAdmitted selection:
                    valid = writable && Admission(selection.Owner, selection.Revision, selection.Generation) &&
                        selection.PopulationId != Uuid.Empty && selection.PopulationVersion > 0 &&
                        !selections.ContainsKey(operation.LogicalId) &&
                        (selection.SupersedesSelectionId is not { } prior || selections.TryGetValue(prior, out var old) &&
                            old.PopulationId == selection.PopulationId && old.PopulationVersion < selection.PopulationVersion) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, selection.SupersedesSelectionId is null
                            ? nameof(RecordSampleSelection) : nameof(AmendSampleSelection), (ulong)revision);
                    if (valid)
                        selections.Add(operation.LogicalId, selection);
                    break;
                case AuditorRequestResolutionAccepted accepted:
                    valid = writable && responses.Count != 0 && Admission(accepted.Owner, accepted.Revision, accepted.Generation) &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ResolveAuditorRequest), (ulong)revision);
                    pending = accepted;
                    break;
                case AuditorRequestResolved completed:
                    valid = pending is not null && completed.TenantId == owner.TenantId && completed.SubjectId == owner.Id &&
                        completed.Revision == revision + 1 && completed.RequestGeneration == generation &&
                        completed.Change == pending.Intent && Child(operation, pending, nameof(CompleteAuditorRequestResolution));
                    resolved = completed;
                    pending = null;
                    break;
                case AuditorRequestResolutionCanceled canceled:
                    valid = pending is not null && Admission(canceled.Owner, canceled.Revision, canceled.Generation) &&
                        canceled.Intent == pending.Intent && Child(operation, pending, nameof(AbortAuditorRequestResolution));
                    pending = null;
                    break;
                case AuditorRequestReopened reopened:
                    valid = resolved is not null && pending is null && reopened.TenantId == owner.TenantId &&
                        reopened.SubjectId == owner.Id && reopened.Revision == revision + 1 &&
                        NativeOwnerHistory.IsOwnRoot(owner, operation, nameof(ReopenAuditorRequest), (ulong)revision);
                    generation++;
                    resolved = null;
                    break;
                default:
                    valid = false;
                    break;
            }
            if (!valid)
                throw Invalid("A request history contains another scope, revision, generation, admission or root binding.");
            revision++;
        }
        if (revision != owner.Revision || generation != owner.Generation || terms != owner.Terms ||
            responses.Count != owner.ResponseCount || pending?.Metadata.EventId != owner.PendingResolution?.Metadata.EventId ||
            resolved?.Metadata.EventId != owner.Resolved?.Metadata.EventId ||
            (facts.Count == 0) != (owner.Latest is null) || facts.Count != 0 &&
            ContentDigest.Sha256(facts[^1], facts[^1].GetType()) != ContentDigest.Sha256(owner.Latest!, owner.Latest!.GetType()))
            throw Invalid("The verified request history differs from compact current authority.");
        owner.ReplayCut.Confirm(owner, digest);
        ct.ThrowIfCancellationRequested();
        return new AuditorRequestHistorySnapshot(owner, facts);

        bool Admission(EventStreamAddress stream, long next, long atGeneration) =>
            stream == owner.Stream && next == revision + 1 && atGeneration == generation;
    }

    static bool Child(OperationProvenance operation, AuditorRequestResolutionAccepted accepted, string kind) =>
        operation.RootOperationId == accepted.Operation.RootOperationId && operation.CommandKind == kind &&
        operation.FirstOwner == accepted.Operation.Owner && operation.Episode == accepted.Operation.Episode &&
        operation.AuthoredIntentSha256 == accepted.Operation.AuthoredIntentSha256 &&
        operation.Actor == accepted.Operation.Actor && operation.CausationEventId == accepted.Metadata.EventId;
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
