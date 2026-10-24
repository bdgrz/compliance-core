using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

static class CommitmentSeeds
{
    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, CommitmentRules rules,
        CancellationToken ct = default) => SaveAsync(null, writer, content, rules, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        CommitmentRules rules, CancellationToken ct = default) => SaveAsync(reader, writer, content, rules, ct);
    public static Task<DomainEvent> FreezeAsync(IImmutableContentStore content, CommitmentRules after,
        IReadOnlyList<ICommitmentMutationContent> explicitChanges, OperationProvenance operation, long nativeSequence,
        CancellationToken ct = default) => FreezeAsync(content, after, new CommitmentMutationContent(explicitChanges), operation, nativeSequence, ct);
    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, CommitmentRules after,
        CommitmentMutationContent mutation, OperationProvenance operation, long nativeSequence, CancellationToken ct = default)
    {
        var frozen = await CommitmentContent.FreezeAsync(content, after, mutation, ct);
        var response = await CommitmentResponses.FreezeAsync(content, after, operation.CommandKind, ct);
        var first = await FirstResponses.FreezeAsync(content, after.TenantId.ToString(), response, ct);
        var result = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(), "mutation-result-content.v1",
            new MutationResultContent(response.ResultSchema, response.Body), ct);
        return CommitmentFactFactory.Create(operation.CommandKind, frozen.Header, nativeSequence, operation,
            frozen.Change, frozen.Authority, first, result, mutation);
    }
    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content,
        CommitmentRules rules, CancellationToken ct)
    {
        if (rules.PendingChanges.Count == 0)
            return;
        var changes = rules.PendingChanges.ToArray();
        var current = new CommitmentRules(rules.TenantId, rules.Id);
        foreach (var change in rules.AppliedChanges.Take(rules.AppliedChanges.Count - changes.Length))
            current.Apply(change);
        current.ClearPendingChanges();
        var owner = new CommitmentDraft(rules.TenantId, rules.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner, ct);
        if (owner.Header is { } prior && current.Header(prior.Content) != prior || owner.Header is null && current.IsCreated)
            throw new InvalidOperationException("The commitment fixture preparation has another native source cut.");
        foreach (var change in changes)
        {
            var metadata = Metadata(change);
            var logical = metadata.Id ?? Uuid.CreateVersion5(owner.Id, "fixture:" + metadata.Kind + ":" + AuthoredIntentDigest.Compute(change));
            var operation = OwnerProvenance.For(owner, logical, metadata.Kind, ActorReference.ForMember(metadata.Member, metadata.Display),
                owner.Id.ToString(), metadata.Gate is { } gate ? unchecked((ulong)gate) : null, Authored(change, logical), metadata.At);
            var before = owner.Header is { } header ? current.Authority(header) : null;
            current.Apply(change);
            var mutation = new CommitmentMutationContent([change], rules.WaiversFor(change));
            var fact = await FreezeAsync(content, current, mutation, operation, owner.NativeSequence + 1, ct);
            var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
                mutation, before, current.Authority(((ICommitmentFact)fact).Header));
            Assert.True(result.IsSuccess, result.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        if (ContentDigest.OfJson(current.CurrentContent!) != ContentDigest.OfJson(rules.CurrentContent!) || current.Revision != rules.Revision)
            throw new InvalidOperationException("The commitment fixture did not publish all prepared native changes.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
    }
    static SeedMetadata Metadata(ICommitmentMutationContent change) => change switch
    {
        CommitmentDraftCreatedContent item => new(nameof(CreateCommitmentDraft), item.ActorMemberId, item.ActorDisplay, item.ChangedAt, null, item.DraftId),
        CommitmentDraftRevisedContent item => new(nameof(ReviseCommitmentDraft), item.ActorMemberId, item.ActorDisplay, item.ChangedAt, item.Revision - 1, null),
        CommitmentReviewedContent item => new(nameof(ReviewCommitmentDraft), item.ActorMemberId, item.ActorDisplay, item.DecidedAt, item.Revision, item.DecisionId),
        CommitmentApprovedContent item => new(nameof(ApproveCommitmentDraft), item.ActorMemberId, item.ActorDisplay, item.DecidedAt, item.Revision, item.DecisionId),
        CommitmentRetiredContent item => new(nameof(RetireCommitment), item.ActorMemberId ?? Uuid.Empty, item.ActorDisplay, item.OccurredAt, item.Revision, null),
        ResponsibilityAssigned item => new(nameof(AssignResponsibility), item.AssignedByMemberId, item.AssignedByDisplay, item.AssignedAt, item.Scope.Revision, item.AssignmentId),
        ResponsibilityRevoked item => new(nameof(RevokeResponsibility), item.RevokedByMemberId, item.RevokedByDisplay, item.RevokedAt, item.Scope.Revision, null),
        _ => throw new InvalidOperationException("The commitment fixture has another mutation kind."),
    };
    static string Authored(ICommitmentMutationContent change, Uuid command) => change switch
    {
        CommitmentDraftCreatedContent item => AuthoredIntentDigest.Compute(new CreateCommitmentDraft(item.DraftId, item.TenantId, item.ProgramId,
            item.ServiceId, item.Kind, item.Identifier, item.Statement, item.Context, item.SourceReference, item.ProviderId)),
        CommitmentDraftRevisedContent item => AuthoredIntentDigest.Compute(new ReviseCommitmentDraft(command, item.TenantId, item.ProgramId,
            item.DraftId, item.Revision - 1, item.Statement, item.Context, item.SourceReference)),
        CommitmentReviewedContent item => AuthoredIntentDigest.Compute(new ReviewCommitmentDraft(command, item.TenantId, item.ProgramId,
            item.DraftId, item.Revision, item.Outcome, item.Rationale, item.OwnerReference, item.Applicability, item.Interpretation,
            item.InterpretationNote, item.SourceVerifiedReference, item.SourceEvidence, item.SeparationOfDutiesWaiverId)),
        CommitmentApprovedContent item => AuthoredIntentDigest.Compute(new ApproveCommitmentDraft(command, item.TenantId, item.ProgramId,
            item.DraftId, item.Revision, item.AcceptedReviewDecisionId, item.EffectiveFrom, item.Rationale, item.ImpactDigest, item.SeparationOfDutiesWaiverId)),
        CommitmentRetiredContent item => AuthoredIntentDigest.Compute(new RetireCommitment(command, item.TenantId, item.ProgramId,
            item.SubjectId, item.Revision, item.EffectiveOn, item.Rationale)),
        _ => AuthoredIntentDigest.Compute(new CommitmentMutationContent([change])),
    };
    sealed record SeedMetadata(string Kind, Uuid Member, string Display, DateTimeOffset At, long? Gate, Uuid? Id);
}
