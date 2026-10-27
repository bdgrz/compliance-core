using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Creates the single compact native fact for one already prepared owner-local business operation.</summary>
public static class ControlFactFactory
{
    public static DomainEvent Create(string commandKind, ControlAuthorityHeader header, long nativeSequence,
        OperationProvenance operation, ImmutableContentRef change, ImmutableContentRef authority,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult, ControlMutationContent mutation) => commandKind switch
        {
            nameof(CreateControlDraft) => ControlDraftCreated(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(ReviseControlDraft) => ControlDraftRevised(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(DiscardControlDraft) => ControlDraftDiscarded(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(ReviewControl) => ControlReviewed(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(ApproveControl) => ControlApproved(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(ProposeControlSuccessor) => ControlSuccessorProposed(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(ProposeControlRetirement) => ControlRetirementProposed(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(RetireControl) => ControlRetired(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(WithdrawControlProposal) => ControlProposalWithdrawn(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(DesignateControlOwnerPerson) => ControlOwnerPersonDesignated(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(AssignResponsibility) => ResponsibilityAssigned(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            nameof(RevokeResponsibility) => ResponsibilityRevoked(header, nativeSequence, operation, change, authority, firstResponse, operationResult, mutation),
            _ => throw new InvalidOperationException("The Control operation kind is not selected."),
        };

    static ControlDraftCreated ControlDraftCreated(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlDraftCreatedContent ?? mutation.Changes.OfType<ControlDraftCreatedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared CreateControlDraft has no original receipt content.");
        return new ControlDraftCreated(data.TenantId, data.ProgramId, data.ControlId, data.CreateRequestId,
            data.Identifier, header.Content, data.ActorMemberId, data.ActorDisplay,
            data.ChangedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlDraftRevised ControlDraftRevised(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlDraftRevisedContent ?? mutation.Changes.OfType<ControlDraftRevisedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared ReviseControlDraft has no original receipt content.");
        return new ControlDraftRevised(data.TenantId, data.ProgramId, data.ControlId, data.Revision,
            header.Content, data.ActorMemberId, data.ActorDisplay, data.ChangedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlDraftDiscarded ControlDraftDiscarded(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlDraftDiscardedContent ?? mutation.Changes.OfType<ControlDraftDiscardedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared DiscardControlDraft has no original receipt content.");
        return new ControlDraftDiscarded(data.TenantId, data.ProgramId, data.ControlId, data.Revision,
            data.ActorMemberId, data.ActorDisplay, data.Rationale, data.DiscardedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlReviewed ControlReviewed(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlReviewedContent ?? mutation.Changes.OfType<ControlReviewedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared ReviewControl has no original receipt content.");
        return new ControlReviewed(data.TenantId, data.ProgramId, data.ControlId, data.VersionId,
            data.Revision, data.DecisionId, data.Outcome, data.ActorMemberId,
            data.ActorDisplay, data.Rationale, data.DecidedAt, data.SupersedesDecisionId,
            data.SeparationOfDutiesWaiverId)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlApproved ControlApproved(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlApprovedContent ?? mutation.Changes.OfType<ControlApprovedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared ApproveControl has no original receipt content.");
        return new ControlApproved(data.TenantId, data.ProgramId, data.ControlId, data.VersionId,
            data.Revision, data.ApprovalDecisionId, data.AcceptedReviewDecisionId, header.Content,
            data.OwnerAssignmentId, data.OwnerMemberId, data.ActorMemberId, data.ActorDisplay,
            data.Rationale, data.EffectiveFrom, data.DecidedAt, data.SeparationOfDutiesWaiverId,
            data.PredecessorVersionId, data.ImpactDigest, data.OwnerPersonId)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlSuccessorProposed ControlSuccessorProposed(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlSuccessorProposedContent ?? mutation.Changes.OfType<ControlSuccessorProposedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared ProposeControlSuccessor has no original receipt content.");
        return new ControlSuccessorProposed(data.TenantId, data.ProgramId, data.ControlId, data.VersionId,
            data.PredecessorVersionId, data.Revision, data.ActorMemberId, data.ActorDisplay,
            data.ChangedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
            Content = header.Content,
        };
    }

    static ControlRetirementProposed ControlRetirementProposed(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlRetirementProposedContent ?? mutation.Changes.OfType<ControlRetirementProposedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared ProposeControlRetirement has no original receipt content.");
        return new ControlRetirementProposed(data.TenantId, data.ProgramId, data.ControlId, data.RetirementId,
            data.VersionId, data.Revision, data.EffectiveUntil, data.Rationale,
            data.ActorMemberId, data.ActorDisplay, data.ChangedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlRetired ControlRetired(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlRetiredContent ?? mutation.Changes.OfType<ControlRetiredContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared RetireControl has no original receipt content.");
        return new ControlRetired(data.TenantId, data.ProgramId, data.ControlId, data.RetirementId,
            data.VersionId, data.Revision, data.DecisionId, data.AcceptedReviewDecisionId,
            data.EffectiveUntil, data.ImpactDigest, data.ActorMemberId, data.ActorDisplay,
            data.Rationale, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ControlProposalWithdrawn ControlProposalWithdrawn(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlProposalWithdrawnContent ?? mutation.Changes.OfType<ControlProposalWithdrawnContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared WithdrawControlProposal has no original receipt content.");
        return new ControlProposalWithdrawn(data.TenantId, data.ProgramId, data.ControlId, data.DecisionId,
            data.Kind, data.TargetId, data.CurrentVersionId, data.Revision,
            data.Rationale, data.ActorMemberId, data.Actor, data.WithdrawnAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            Content = header.Content,
        };
    }

    static ControlOwnerPersonDesignated ControlOwnerPersonDesignated(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ControlOwnerPersonDesignatedContent ?? mutation.Changes.OfType<ControlOwnerPersonDesignatedContent>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared DesignateControlOwnerPerson has no original receipt content.");
        return new ControlOwnerPersonDesignated(data.TenantId, data.ProgramId, data.ControlId, data.VersionId,
            data.Revision, data.DesignationId, data.PersonId, data.CorrelatedMemberId,
            data.Rationale, data.ActorMemberId, data.Actor, data.DesignatedAt)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
        };
    }

    static ResponsibilityAssigned ResponsibilityAssigned(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ResponsibilityAssigned ?? mutation.Changes.OfType<ResponsibilityAssigned>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared AssignResponsibility has no original receipt content.");
        return new ResponsibilityAssigned(data.TenantId, data.AssignmentId, data.MemberId, data.Type,
            data.Scope, data.AssignedAt, data.AssignedByMemberId, data.EffectiveFrom,
            data.EffectiveUntil, data.SeparationOfDutiesWaiverIds, data.AssignedByDisplay)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

    static ResponsibilityRevoked ResponsibilityRevoked(ControlAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first,
        ImmutableContentRef result, ControlMutationContent mutation)
    {
        var data = mutation.Receipt as ResponsibilityRevoked ?? mutation.Changes.OfType<ResponsibilityRevoked>().FirstOrDefault()
            ?? throw new InvalidOperationException("The prepared RevokeResponsibility has no original receipt content.");
        return new ResponsibilityRevoked(data.TenantId, data.AssignmentId, data.Scope, data.RevokedByMemberId,
            data.RevokedAt, data.Reason, data.RevokedByDisplay)
        {
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            Operation = operation,
            FirstResponse = first,
            OperationResult = result,
            StoredActor = data.StoredActor,
        };
    }

}
