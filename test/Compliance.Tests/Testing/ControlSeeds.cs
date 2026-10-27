using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit native fixture publication of validated transient rules; no production compatibility path exists.</summary>
static class ControlSeeds
{
    public static Task WriteAsync(IAggregateWriter writer, IImmutableContentStore content, ControlRules rules,
        CancellationToken ct = default) => SaveAsync(null, writer, content, rules, ct);
    public static Task AppendAsync(IAggregateReader reader, IAggregateWriter writer, IImmutableContentStore content,
        ControlRules rules, CancellationToken ct = default) => SaveAsync(reader, writer, content, rules, ct);

    public static async Task<DomainEvent> FreezeAsync(IImmutableContentStore content, ControlRules after,
        IReadOnlyList<IControlMutationContent> explicitChanges, OperationProvenance operation, long nativeSequence,
        CancellationToken ct = default)
    {
        var mutation = new ControlMutationContent(explicitChanges);
        var frozen = await ControlContent.FreezeAsync(content, after, mutation, ct);
        var response = await ControlResponses.FreezeAsync(content, after, operation.CommandKind, ct);
        var first = await FirstResponses.FreezeAsync(content, after.TenantId.ToString(), response, ct);
        var result = await content.WriteJsonAsync(after.TenantId.ToString(), Uuid.CreateVersion4(),
            "mutation-result-content.v1", new MutationResultContent(response.ResultSchema, response.Body), ct);
        return ControlFactFactory.Create(operation.CommandKind, frozen.Header, nativeSequence, operation,
            frozen.Change, frozen.Authority, first, result, mutation);
    }

    static async Task SaveAsync(IAggregateReader? reader, IAggregateWriter writer, IImmutableContentStore content,
        ControlRules rules, CancellationToken ct)
    {
        if (rules.PendingChanges.Count == 0)
            return;
        var changes = rules.PendingChanges.ToArray();
        var original = rules.AppliedChanges.Take(rules.AppliedChanges.Count - changes.Length).ToArray();
        var current = new ControlRules(rules.TenantId, rules.Id);
        foreach (var change in original)
            current.Apply(change);
        current.ClearPendingChanges();
        var owner = new ControlDraft(rules.TenantId, rules.Id);
        if (reader is not null)
            owner = await reader.HydrateAsync(owner, ct);
        if (owner.Header is { } prior && current.Header(prior.Content) != prior ||
            owner.Header is null && current.IsCreated)
            throw new InvalidOperationException("The Control fixture's preparation does not match the native source cut.");
        for (var index = 0; index < changes.Length; index++)
        {
            var group = new List<IControlMutationContent> { changes[index] };
            if (changes[index] is ControlSuccessorProposedContent or ControlProposalWithdrawnContent)
            {
                if (++index >= changes.Length || changes[index] is not ControlDraftRevisedContent)
                    throw new InvalidOperationException("An atomic proposal needs its exact following content revision.");
                group.Add(changes[index]);
            }
            var metadata = Metadata(group[0]);
            var logical = metadata.LogicalId ?? Uuid.CreateVersion5(owner.Id, "fixture:" + metadata.Kind + ":" +
                AuthoredIntentDigest.Compute(new ControlMutationContent(group)));
            if (group[0] is ControlDraftCreatedContent)
                logical = owner.Id;
            var operation = OwnerProvenance.For(owner, logical, metadata.Kind,
                ActorReference.ForMember(metadata.Member, metadata.Member.ToString()), owner.Id.ToString(),
                metadata.ExpectedRevision is { } expected ? (ulong)expected : null,
                Authored(group[0], logical, current, group), metadata.At);
            var beforeAuthority = owner.Header is { } before ? current.Authority(before) : null;
            foreach (var change in group)
                current.Apply(change);
            var fact = await FreezeAsync(content, current, group.AsReadOnly(), operation, owner.NativeSequence + 1, ct);
            var result = owner.Append(fact, owner.NativeSequence, owner.CommittedStreamPosition,
                new ControlMutationContent(group.AsReadOnly()), beforeAuthority, current.Authority(((IControlFact)fact).Header));
            Assert.True(result.IsSuccess, result.Error?.Message);
            await writer.SaveAsync(owner, new RequestDispatchContext(RequestActor.System), ct);
        }
        if (ContentDigest.OfJson(current.CurrentContent!) != ContentDigest.OfJson(rules.CurrentContent!) ||
            current.Revision != rules.Revision)
            throw new InvalidOperationException("The fixture did not publish the complete prepared native change sequence.");
        rules.ClearPendingChanges();
        rules.SourcePhysicalPosition = owner.CommittedStreamPosition;
    }

    static string Authored(IControlMutationContent value, Uuid logical, ControlRules before,
        IReadOnlyList<IControlMutationContent> group) => value switch
        {
            ControlDraftCreatedContent change => AuthoredIntentDigest.Compute(new CreateControlDraft(change.TenantId,
                change.ProgramId, change.Identifier, change.Content)),
            ControlDraftRevisedContent change => AuthoredIntentDigest.Compute(new ReviseControlDraft(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.Revision - 1, change.Content)),
            ControlDraftDiscardedContent change => AuthoredIntentDigest.Compute(new DiscardControlDraft(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.Revision, change.Rationale)),
            ControlReviewedContent change => AuthoredIntentDigest.Compute(new ReviewControl(logical, change.TenantId,
                change.ProgramId, change.ControlId, change.Revision, change.Outcome, change.Rationale, change.SeparationOfDutiesWaiverId)),
            ControlApprovedContent change => AuthoredIntentDigest.Compute(new ApproveControl(logical, change.TenantId,
                change.ProgramId, change.ControlId, change.Revision, change.AcceptedReviewDecisionId, change.EffectiveFrom,
                change.Rationale, change.SeparationOfDutiesWaiverId, change.ImpactDigest)),
            ControlSuccessorProposedContent change => AuthoredIntentDigest.Compute(new ProposeControlSuccessor(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.PredecessorVersionId,
                group.OfType<ControlDraftRevisedContent>().Single().Content)),
            ControlRetirementProposedContent change => AuthoredIntentDigest.Compute(new ProposeControlRetirement(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.VersionId, change.EffectiveUntil, change.Rationale)),
            ControlRetiredContent change => AuthoredIntentDigest.Compute(new RetireControl(logical, change.TenantId,
                change.ProgramId, change.ControlId, change.Revision, change.AcceptedReviewDecisionId, change.ImpactDigest,
                change.Rationale, change.SeparationOfDutiesWaiverId)),
            ControlProposalWithdrawnContent change => AuthoredIntentDigest.Compute(new WithdrawControlProposal(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.Revision, change.Rationale)),
            ControlOwnerPersonDesignatedContent change => AuthoredIntentDigest.Compute(new DesignateControlOwnerPerson(logical,
                change.TenantId, change.ProgramId, change.ControlId, change.Revision, change.PersonId, change.Rationale)),
            _ => AuthoredIntentDigest.Compute(new ControlMutationContent(group)),
        };

    static SeedMetadata Metadata(IControlMutationContent value) => value switch
    {
        ControlDraftCreatedContent change => new(nameof(CreateControlDraft), change.ActorMemberId, change.ChangedAt, null, change.ControlId),
        ControlDraftRevisedContent change => new(nameof(ReviseControlDraft), change.ActorMemberId, change.ChangedAt, change.Revision - 1, null),
        ControlDraftDiscardedContent change => new(nameof(DiscardControlDraft), change.ActorMemberId, change.DiscardedAt, change.Revision, null),
        ControlReviewedContent change => new(nameof(ReviewControl), change.ActorMemberId, change.DecidedAt, change.Revision, change.DecisionId),
        ControlApprovedContent change => new(nameof(ApproveControl), change.ActorMemberId, change.DecidedAt, change.Revision, change.ApprovalDecisionId),
        ControlSuccessorProposedContent change => new(nameof(ProposeControlSuccessor), change.ActorMemberId, change.ChangedAt, null, null),
        ControlRetirementProposedContent change => new(nameof(ProposeControlRetirement), change.ActorMemberId, change.ChangedAt, null, null),
        ControlRetiredContent change => new(nameof(RetireControl), change.ActorMemberId, change.DecidedAt, change.Revision, change.DecisionId),
        ControlProposalWithdrawnContent change => new(nameof(WithdrawControlProposal), change.ActorMemberId, change.WithdrawnAt, change.Revision, change.DecisionId),
        ControlOwnerPersonDesignatedContent change => new(nameof(DesignateControlOwnerPerson), change.ActorMemberId, change.DesignatedAt, change.Revision, change.DesignationId),
        ResponsibilityAssigned change => new(nameof(AssignResponsibility), change.AssignedByMemberId, change.AssignedAt, change.Scope.Revision, change.AssignmentId),
        ResponsibilityRevoked change => new(nameof(RevokeResponsibility), change.RevokedByMemberId, change.RevokedAt, change.Scope.Revision, null),
        _ => throw new InvalidOperationException("The Control fixture has another business mutation kind."),
    };
    sealed record SeedMetadata(string Kind, Uuid Member, DateTimeOffset At, long? ExpectedRevision, Uuid? LogicalId);
}
