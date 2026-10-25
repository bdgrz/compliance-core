using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

public static class SystemDescriptionFactFactory
{
    public static DomainEvent Create(FrozenSystemDescriptionMutation frozen, long sequence, OperationProvenance operation,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        if (!Uuid.TryParse(operation.Actor.Id, out var member))
            throw new InvalidOperationException("A description command requires its member actor.");
        var h = frozen.Header;
        return frozen.Mutation switch
        {
            SystemDescriptionApprovedBody => new SystemDescriptionApproved(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionBoundBody => new SystemDescriptionBound(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionDraftCreatedBody => new SystemDescriptionDraftCreated(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionDraftRevisedBody => new SystemDescriptionDraftRevised(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionNoChangeRecordedBody => new SystemDescriptionNoChangeRecorded(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionReviewedBody => new SystemDescriptionReviewed(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionSourcesBoundBody => new SystemDescriptionSourcesBound(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            SystemDescriptionSuccessorProposedBody => new SystemDescriptionSuccessorProposed(h.TenantId, h.ProgramId, h.SystemDescriptionId, h.Revision, frozen.Payload, operation.AcceptedAt, operation, first, result, h, frozen.CurrentVersion, sequence, frozen.Changed ? 1 : 0, member) { CurrentAuthorityContent = frozen.AuthorityContent, SourceSnapshotContent = frozen.SourceSnapshotContent },
            _ => throw new InvalidOperationException("The description mutation has another business kind."),
        };
    }
}
