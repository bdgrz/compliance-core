using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.Policies.Commands;
using Bdgrz.Compliance.Features.Policies.Events;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Policies.Rules;

public static class PolicyFactFactory
{
    public static DomainEvent Create(string kind, PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation) => kind switch
        {
            nameof(CreatePolicyDraft) => PolicyDraftCreated(header, sequence, operation, change, authority, first, result, mutation),
            nameof(RevisePolicyDraft) => PolicyDraftRevised(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ReviewPolicyDraft) => PolicyReviewed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ApprovePolicy) => PolicyApproved(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ConfirmPolicyReview) => PolicyPeriodicReviewConfirmed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ProposePolicySuccessor) => PolicyDraftRevised(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ProposePolicyRetirement) => PolicyRetirementProposed(header, sequence, operation, change, authority, first, result, mutation),
            nameof(ApprovePolicyRetirement) => PolicyRetired(header, sequence, operation, change, authority, first, result, mutation),
            nameof(DiscardPolicyDraft) => PolicyDraftDiscarded(header, sequence, operation, change, authority, first, result, mutation),
            _ => throw new InvalidOperationException("The Policy command kind is not selected."),
        };
    static PolicyDraftCreated PolicyDraftCreated(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyDraftCreatedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.CreateRequestId, data.Identifier, header.Content, data.ContentSha256, data.Actor, data.ActorMemberId, data.ChangedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyDraftRevised PolicyDraftRevised(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyDraftRevisedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, header.Content, data.ContentSha256, data.PredecessorVersion, data.Actor, data.ActorMemberId, data.ChangedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyReviewed PolicyReviewed(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyReviewedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, data.DecisionId, data.Outcome, data.Rationale, data.Actor, data.ActorMemberId, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyApproved PolicyApproved(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyApprovedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, data.Version, data.Major, data.DecisionId, data.AcceptedReviewDecisionId, data.ContentSha256, data.EffectiveFrom, data.PredecessorVersion, data.ImpactDigest, data.Rationale, data.Actor, data.ActorMemberId, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyPeriodicReviewConfirmed PolicyPeriodicReviewConfirmed(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyPeriodicReviewConfirmedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Version, data.DecisionId, data.ReviewedOn, data.Rationale, data.Actor, data.ActorMemberId, data.DecidedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyRetirementProposed PolicyRetirementProposed(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyRetirementProposedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, data.Version, data.EffectiveUntil, data.Rationale, data.Actor, data.ActorMemberId, data.ProposedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyRetired PolicyRetired(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyRetiredContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, data.Version, data.DecisionId, data.AcceptedReviewDecisionId, data.EffectiveUntil, data.Rationale, data.Actor, data.ActorMemberId, data.DecidedAt, data.SeparationOfDutiesWaiverId)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
    static PolicyDraftDiscarded PolicyDraftDiscarded(PolicyAuthorityHeader header, long sequence, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef first, ImmutableContentRef result,
        PolicyMutationContent mutation)
    {
        if (mutation.Change is not PolicyDraftDiscardedContent data)
            throw new InvalidOperationException("The Policy command has another exact mutation kind.");
        return new(data.TenantId, data.ProgramId, data.PolicyId, data.Revision, data.Rationale, data.Actor, data.DiscardedAt)
        {
            Operation = operation,
            Header = header,
            Change = change,
            Authority = authority,
            NativeSequence = sequence,
            FirstResponse = first,
            OperationResult = result,
        };
    }
}
