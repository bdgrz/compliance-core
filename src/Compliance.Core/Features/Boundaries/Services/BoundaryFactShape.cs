using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Events;

namespace Bdgrz.Compliance.Features.Boundaries.Services;

/// <summary>Maps native named facts to the retained local transition shape; execution metadata stays on the original fact.</summary>
static class BoundaryFactShape
{
    public static DomainEvent Normalize(DomainEvent value) => value switch
    {
        BoundaryDrafted item when item.Header.Revision == 1 => new BoundaryDraftCreated(item.TenantId, item.ProgramId, item.ProgramId, item.DraftVersionId, item.Content, item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt)
        {
            Operation = item.Operation,
            FirstResponse = item.FirstResponse,
            OperationResult = item.OperationResult,
            Header = item.Header,
            Change = item.Change,
            Authority = item.Authority,
            NativeSequence = item.NativeSequence,
            ChangeCount = item.ChangeCount,
        },
        BoundaryDrafted item => new BoundarySuccessorProposed(item.TenantId, item.ProgramId, item.DraftVersionId, item.PredecessorVersionId ?? Uuid.Empty, item.Content, item.AuthorMemberId, item.AuthorDisplay, item.ChangedAt)
        {
            Operation = item.Operation,
            FirstResponse = item.FirstResponse,
            OperationResult = item.OperationResult,
            Header = item.Header,
            Change = item.Change,
            Authority = item.Authority,
            NativeSequence = item.NativeSequence,
            ChangeCount = item.ChangeCount,
        },
        BoundaryChangesRequested item => new BoundaryReviewed(item.TenantId, item.ProgramId, item.DraftVersionId, item.Revision, item.DecisionId, "request_changes", item.ActorMemberId, item.ActorDisplay, item.DecidedAt, item.SeparationOfDutiesWaiverId)
        {
            Operation = item.Operation,
            FirstResponse = item.FirstResponse,
            OperationResult = item.OperationResult,
            Header = item.Header,
            Change = item.Change,
            Authority = item.Authority,
            NativeSequence = item.NativeSequence,
            ChangeCount = item.ChangeCount,
        },
        BoundaryVersionApproved item => new BoundaryApproved(item.TenantId, item.ProgramId, item.DraftVersionId, item.Revision, item.DecisionId, item.DecisionId, item.ActorMemberId, item.ActorDisplay, item.EffectiveFrom, item.DecidedAt, item.SeparationOfDutiesWaiverId)
        {
            Operation = item.Operation,
            FirstResponse = item.FirstResponse,
            OperationResult = item.OperationResult,
            Header = item.Header,
            Change = item.Change,
            Authority = item.Authority,
            NativeSequence = item.NativeSequence,
            ChangeCount = item.ChangeCount,
        },
        _ => value,
    };
}
