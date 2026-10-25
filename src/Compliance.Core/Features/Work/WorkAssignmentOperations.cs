using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>All four authored accountability operations at one exact native cut with original typed result replay.</summary>
public sealed class WorkAssignmentOperations(NativeOperationExecutor operations, IAggregateReader reader,
    WorkAssignmentReader history, WorkQueueReader queue, WorkAssignmentAuthorization authorization,
    IImmutableContentStore content, TimeProvider clock)
{
    public const string ResultSchema = "work-item-detail-view.v1";

    public ValueTask<Result<WorkItemDetailView>> AssignAsync(IRequestContext<AssignWorkItem> context, CancellationToken ct) =>
        RunAsync(context, context.Request, async (actor, snapshot, entry, token) =>
        {
            var request = context.Request;
            if (!snapshot.ActorManages)
                return Fail("Only a current program manager may assign or reassign work.", RequestErrorKind.Forbidden);
            if (!await queue.CanAssignAsync(request.TenantId, entry.Candidate, actor.MemberId, token).ConfigureAwait(false))
                return Fail("Actual Attest assignment history prevents authoring this management assignment.", RequestErrorKind.Forbidden);
            if (entry.Item.AssigneeMemberId == request.AssigneeMemberId)
                return Fail("The work item is already assigned to this member.");
            if (request.AssigneeMemberId == Uuid.Empty || !await queue.IsEligibleAsync(request.TenantId, entry.Candidate, request.AssigneeMemberId, token).ConfigureAwait(false))
                return Fail("The source workflow would not accept this member, including separation of duties.", RequestErrorKind.Validation);
            return Result<Change>.Success(new Change(entry.Item.AssigneeMemberId is null ? WorkItemAssignment.Assign : WorkItemAssignment.Reassign,
                request.AssigneeMemberId, entry.Item.AssigneeMemberId, request.Reason));
        }, ct);

    public ValueTask<Result<WorkItemDetailView>> ClaimAsync(IRequestContext<ClaimWorkItem> context, CancellationToken ct) =>
        RunAsync(context, context.Request, (actor, _, entry, _) =>
        {
            if (entry.Candidate.Responsible.Kind != OperatingAuthority.TeamHolder)
                return ValueTask.FromResult(Fail("Only team work can be claimed.", RequestErrorKind.Validation));
            if (entry.Item.AssigneeMemberId is not null)
                return ValueTask.FromResult(Fail("The work item is already assigned."));
            if (!entry.ActorEligible || !entry.ActorInTeam)
                return ValueTask.FromResult(Fail("Only an eligible current member of the holding team may claim work.", RequestErrorKind.Forbidden));
            return ValueTask.FromResult(Result<Change>.Success(new Change(WorkItemAssignment.Claim, actor.MemberId, null, null)));
        }, ct);

    public ValueTask<Result<WorkItemDetailView>> DelegateAsync(IRequestContext<DelegateWorkItem> context, CancellationToken ct) =>
        RunAsync(context, context.Request, async (actor, _, entry, token) =>
        {
            var request = context.Request;
            if (entry.Item.AssigneeMemberId != actor.MemberId)
                return Fail("Only the current assignee may delegate work.", RequestErrorKind.Forbidden);
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Fail("A delegation needs a reason.", RequestErrorKind.Validation);
            if (request.AssigneeMemberId == Uuid.Empty || request.AssigneeMemberId == actor.MemberId ||
                !await queue.IsEligibleAsync(request.TenantId, entry.Candidate, request.AssigneeMemberId, token).ConfigureAwait(false))
                return Fail("The source workflow would not accept this member, including separation of duties.", RequestErrorKind.Validation);
            return Result<Change>.Success(new Change(WorkItemAssignment.Delegate, request.AssigneeMemberId, actor.MemberId, request.Reason));
        }, ct);

    public ValueTask<Result<WorkItemDetailView>> EscalateAsync(IRequestContext<EscalateWorkItem> context, CancellationToken ct) =>
        RunAsync(context, context.Request, (actor, _, entry, _) =>
        {
            if (entry.Item.AssigneeMemberId != actor.MemberId)
                return ValueTask.FromResult(Fail("Only the current assignee may escalate work.", RequestErrorKind.Forbidden));
            if (entry.Item.Escalated)
                return ValueTask.FromResult(Fail("The work item is already escalated."));
            if (string.IsNullOrWhiteSpace(context.Request.Reason))
                return ValueTask.FromResult(Fail("An escalation needs a reason.", RequestErrorKind.Validation));
            return ValueTask.FromResult(Result<Change>.Success(new Change(WorkItemAssignment.Escalate, null, null, context.Request.Reason)));
        }, ct);

    async ValueTask<Result<WorkItemDetailView>> RunAsync<TRequest>(IRequestContext<TRequest> context,
        IWorkAssignmentCommand request,
        Func<OperationsActor, WorkQueueSnapshot, WorkQueueEntry, CancellationToken, ValueTask<Result<Change>>> prepare,
        CancellationToken ct) where TRequest : IRequestBase
    {
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.WorkItemId == Uuid.Empty || request.ExpectedRevision < 0)
            return Result<WorkItemDetailView>.Failure(new RequestError(RequestErrorKind.Validation, "A command identity and exact non-negative owning work revision are required."));
        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var currentActor = await queue.AuthorizeAssignmentActorAsync(request.TenantId, actor, ct).ConfigureAwait(false);
        if (!currentActor.IsSuccess)
            return Result<WorkItemDetailView>.Failure(currentActor.Error!);
        var now = clock.GetUtcNow();
        var stream = new WorkItemAssignment(request.TenantId, request.ProgramId, request.WorkItemId).Stream;
        var kind = typeof(TRequest).Name;
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null, kind,
            ActorReference.ForMember(actor.MemberId, actor.Display), stream, RootOperation.InitialEpisode,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(context.Request), now, stream, stream, FirstOwner: stream);
        var binding = AcceptedOperationBinding.Of(provenance);
        var selected = await reader.HydrateAsync(new WorkItemAssignment(request.TenantId, request.ProgramId, request.WorkItemId, request.CommandId), ct).ConfigureAwait(false);
        if (selected.Acceptance.SelectedFact is IWorkAssignmentFact original)
        {
            try
            {
                var captured = await history.ReadChangeAsync(selected, original, ct).ConfigureAwait(false);
                var permitted = await authorization.AuthorizeAsync(request.TenantId, request.ProgramId, actor, captured.Source, kind, ct).ConfigureAwait(false);
                if (!permitted.IsSuccess)
                    return Result<WorkItemDetailView>.Failure(permitted.Error!);
            }
            catch (ImmutableContentIntegrityException error)
            {
                return Result<WorkItemDetailView>.Failure(ContentFailure(error));
            }
        }
        WorkAssignmentEntryView? entryValue = null;
        ImmutableContentRef? changeReference = null;
        ImmutableContentRef? resultReference = null;
        WorkAssignmentLegacyHistoryImported? import = null;
        var result = await operations.ExecuteAsync(new NativeOperation<WorkItemAssignment>(binding,
            () => new WorkItemAssignment(request.TenantId, request.ProgramId, request.WorkItemId, request.CommandId), owner => owner.Acceptance,
            async (owner, token) =>
            {
                try
                {
                    var found = await queue.FindAsync(request.TenantId, request.ProgramId, actor, request.WorkItemId, token).ConfigureAwait(false);
                    if (!found.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(found.Error!);
                    var (snapshot, entry) = found.Value;
                    if (entry.NativeOwner is null || owner.ReplayCut.Digest != entry.NativeOwner.ReplayCut.Digest ||
                        owner.Version != entry.NativeOwner.Version || owner.CommittedStreamPosition != entry.NativeOwner.CommittedStreamPosition)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The exact work assignment prefix/revision changed; reload and retry.", true));
                    if (request.ExpectedRevision != entry.State.Revision)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "Reload the exact current work assignment revision before authoring a new operation."));
                    var prepared = await prepare(actor, snapshot, entry, token).ConfigureAwait(false);
                    if (!prepared.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                    var change = prepared.Value!;
                    if (change.Reason?.Trim().Length > 2000)
                        return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation, "A reason must be at most 2000 characters."));
                    entryValue = new WorkAssignmentEntryView(entry.State.Revision + 1, change.Action, change.Assignee, change.Previous,
                        string.IsNullOrWhiteSpace(change.Reason) ? null : change.Reason.Trim(), provenance.Actor, now);
                    var state = new WorkAssignmentState(entryValue.Revision,
                        change.Action == WorkItemAssignment.Escalate ? entry.State.AssigneeMemberId : change.Assignee,
                        entry.State.Escalated || change.Action == WorkItemAssignment.Escalate, entry.State.History.Append(entryValue).ToArray());
                    var effectiveAssignee = change.Action == WorkItemAssignment.Escalate ? entry.Item.AssigneeMemberId : change.Assignee;
                    var sourceIdentity = WorkAssignmentSourceIdentity.Capture(request.TenantId, request.ProgramId, entry.Candidate);
                    var sourceAuthority = await authorization.AuthorizeAsync(request.TenantId, request.ProgramId, actor, sourceIdentity, kind, token).ConfigureAwait(false);
                    if (!sourceAuthority.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(sourceAuthority.Error!);
                    var captured = new WorkAssignmentChangeContent(sourceIdentity,
                        entryValue, snapshot.Today, effectiveAssignee);
                    var realm = stream.Realm;
                    import = null;
                    if (owner.Revision == 0 && entry.LegacyState.Revision > 0)
                    {
                        var legacy = await history.ReadLegacyAsync(request.TenantId, request.ProgramId, request.WorkItemId, token).ConfigureAwait(false);
                        if (legacy.State.Revision != entry.LegacyState.Revision || !legacy.State.History.SequenceEqual(entry.LegacyState.History))
                            return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict, "The read-only legacy source changed during import preparation.", true));
                        var reference = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), WorkAssignmentLegacyContent.Schema, legacy, token).ConfigureAwait(false);
                        import = new WorkAssignmentLegacyHistoryImported(request.TenantId, request.ProgramId, request.WorkItemId, request.CommandId,
                            legacy.State.Revision, legacy.State.AssigneeMemberId, legacy.State.Escalated, reference);
                    }
                    changeReference = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), WorkAssignmentChangeContent.Schema, captured, token).ConfigureAwait(false);
                    var detail = new WorkItemDetailView(WorkQueueReader.Compose(entry.Candidate, state, effectiveAssignee, snapshot.Today), state.History);
                    var view = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), ResultSchema, detail, token).ConfigureAwait(false);
                    resultReference = await content.WriteJsonAsync(realm, Uuid.CreateVersion4(), "mutation-result-content.v1",
                        new MutationResultContent(ResultSchema, new StandardResponseBody(view)), token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, ResultSchema, new StandardResponseBody(view)));
                }
                catch (ImmutableContentIntegrityException error)
                {
                    return Result<FirstResponseDescriptor>.Failure(ContentFailure(error));
                }
            }, (WorkItemAssignment owner, ImmutableContentRef response) => owner.Record(provenance, entryValue!, changeReference!, response, resultReference!, import)), context, ct).ConfigureAwait(false);
        return await IndependenceContent.ReadOutcomeAsync<WorkItemDetailView>(content, stream.Realm, result, ct).ConfigureAwait(false);
    }

    static RequestError ContentFailure(ImmutableContentIntegrityException error) => new(RequestErrorKind.Conflict,
        $"The original work source cannot be verified: {error.Message}", error is ImmutableContentUnavailableException);
    static Result<Change> Fail(string message, RequestErrorKind kind = RequestErrorKind.Conflict) => Result<Change>.Failure(new RequestError(kind, message));
    sealed record Change(string Action, Uuid? Assignee, Uuid? Previous, string? Reason);
}
