using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public static class BoundaryFactFactory
{
    public static DomainEvent Create(string kind, BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        BoundaryMutationContent mutation) => kind switch
        {
            nameof(CreateBoundary) => BoundaryDraftCreated(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ReviseBoundaryDraft) => BoundaryDraftRevised(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ReviewBoundary) => BoundaryReviewed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ApproveBoundary) => BoundaryApproved(header, sequence, operation, change, authority, first, result, mutation),
            nameof(DiscardBoundaryDraft) => BoundaryDraftDiscarded(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ProposeBoundarySuccessor) => BoundarySuccessorProposed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(AssignResponsibility) => ResponsibilityAssigned(header, sequence, operation, change, authority, first, result, mutation),
            nameof(RevokeResponsibility) => ResponsibilityRevoked(header, sequence, operation, change, authority, first, result, mutation),
            _ => throw new InvalidOperationException("The boundary command kind is not selected."),
        };
    static T Selected<T>(BoundaryMutationContent mutation) where T : class, IBoundaryMutationContent =>
        (mutation.Changes.Count == 0 ? mutation.Receipt as T : mutation.Changes.OfType<T>().SingleOrDefault())
        ?? throw new InvalidOperationException("The boundary command has another exact prepared content kind.");
    static BoundaryDraftCreated BoundaryDraftCreated(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundaryDraftCreatedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.ProgramId, data.DraftVersionId, header.Content, data.AuthorMemberId, data.AuthorDisplay, data.ChangedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static BoundaryDraftRevised BoundaryDraftRevised(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundaryDraftRevisedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.DraftVersionId, data.Revision, header.Content, data.AuthorMemberId, data.AuthorDisplay, data.ChangedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static BoundarySuccessorProposed BoundarySuccessorProposed(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundarySuccessorProposedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.DraftVersionId, data.PredecessorVersionId, header.Content, data.AuthorMemberId, data.AuthorDisplay, data.ChangedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static BoundaryReviewed BoundaryReviewed(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundaryReviewedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.DraftVersionId, data.Revision, data.DecisionId, data.Outcome, data.ActorMemberId, data.ActorDisplay, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }
    static BoundaryApproved BoundaryApproved(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundaryApprovedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.DraftVersionId, data.Revision, data.ApprovalDecisionId, data.AcceptedReviewDecisionId, data.ActorMemberId, data.ActorDisplay, data.EffectiveFrom, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }
    static BoundaryDraftDiscarded BoundaryDraftDiscarded(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<BoundaryDraftDiscardedContent>(mutation);
        return new(data.TenantId, data.BoundaryId, data.DraftVersionId, data.Revision, data.ActorMemberId, data.ActorDisplay, data.DiscardedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }
    static ResponsibilityAssigned ResponsibilityAssigned(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<ResponsibilityAssigned>(mutation);
        return data with
        {
            Operation = operation,
            BoundaryHeader = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result
        };
    }
    static ResponsibilityRevoked ResponsibilityRevoked(BoundaryAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result, BoundaryMutationContent mutation)
    {
        var data = Selected<ResponsibilityRevoked>(mutation);
        return data with
        {
            Operation = operation,
            BoundaryHeader = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result
        };
    }
}
