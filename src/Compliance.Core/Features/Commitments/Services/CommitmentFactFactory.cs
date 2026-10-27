using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
namespace Bdgrz.Compliance.Features.Commitments.Services;

public static class CommitmentFactFactory
{
    public static DomainEvent Create(string kind, CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation) => kind switch
        {
            nameof(CreateCommitmentDraft) => CommitmentDraftCreated(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ReviseCommitmentDraft) => CommitmentDraftRevised(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ReviewCommitmentDraft) => CommitmentReviewed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ApproveCommitmentDraft) => CommitmentApproved(header, sequence, operation, change, authority, first, result, mutation),
            nameof(RetireCommitment) => CommitmentRetired(header, sequence, operation, change, authority, first, result, mutation),
            nameof(AssignResponsibility) => ResponsibilityAssigned(header, sequence, operation, change, authority, first, result, mutation),
            nameof(RevokeResponsibility) => ResponsibilityRevoked(header, sequence, operation, change, authority, first, result, mutation),
            _ => throw new InvalidOperationException("The commitment command kind is not selected."),
        };
    static T Selected<T>(CommitmentMutationContent mutation) where T : class, ICommitmentMutationContent =>
        (mutation.Changes.Count == 0 ? mutation.Receipt as T : mutation.Changes.OfType<T>().SingleOrDefault())
        ?? throw new InvalidOperationException("The commitment command has another exact prepared content kind.");
    static CommitmentDraftCreated CommitmentDraftCreated(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<CommitmentDraftCreatedContent>(mutation);
        return new(data.TenantId, data.ProgramId, data.DraftId, data.CreateRequestId, data.ServiceId, data.Kind, data.Identifier, header.Content, data.SourceReference, data.ActorMemberId, data.ActorDisplay, data.ChangedAt)
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
            ProviderId = data.ProviderId,
        };
    }
    static CommitmentDraftRevised CommitmentDraftRevised(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<CommitmentDraftRevisedContent>(mutation);
        return new(data.TenantId, data.ProgramId, data.DraftId, data.Revision, header.Content, data.SourceReference, data.ActorMemberId, data.ActorDisplay, data.ChangedAt)
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
    static CommitmentReviewed CommitmentReviewed(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<CommitmentReviewedContent>(mutation);
        return new(data.TenantId, data.ProgramId, data.DraftId, data.Revision, data.DecisionId, data.Outcome, data.Version, data.EffectiveFrom, data.ActorMemberId, data.ActorDisplay, data.DecidedAt, data.SeparationOfDutiesWaiverId, data.SourceVerification)
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
    static CommitmentApproved CommitmentApproved(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<CommitmentApprovedContent>(mutation);
        return new(data.TenantId, data.ProgramId, data.DraftId, data.Revision, data.DecisionId, data.AcceptedReviewDecisionId, data.Version, data.EffectiveFrom, data.ActorMemberId, data.ActorDisplay, data.DecidedAt, data.SeparationOfDutiesWaiverId)
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
    static CommitmentRetired CommitmentRetired(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<CommitmentRetiredContent>(mutation);
        return new(data.TenantId, data.ProgramId, data.SubjectId, data.Revision, data.ActorMemberId, data.ActorDisplay, data.EffectiveOn, data.OccurredAt)
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
    static ResponsibilityAssigned ResponsibilityAssigned(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<ResponsibilityAssigned>(mutation);
        return data with
        {
            Operation = operation,
            CommitmentHeader = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result
        };
    }
    static ResponsibilityRevoked ResponsibilityRevoked(CommitmentAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        CommitmentMutationContent mutation)
    {
        var data = Selected<ResponsibilityRevoked>(mutation);
        return data with
        {
            Operation = operation,
            CommitmentHeader = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            ChangeCount = mutation.Changes.Count,
            FirstResponse = first,
            OperationResult = result
        };
    }
}
