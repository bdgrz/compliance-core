using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

static class CommitmentDirectoryFacts
{
    public static ICommitmentFact Require(DomainEvent value)
    {
        if (value is not ICommitmentFact fact || fact.Header is not { } header || fact.Change is null ||
            fact.Authority is null || fact.Operation is null || fact.NativeSequence <= 0 || fact.ChangeCount != 1 ||
            header.TenantId == Uuid.Empty || header.ProgramId == Uuid.Empty || header.DraftId == Uuid.Empty ||
            fact.TenantId != header.TenantId || header.Revision < 1 || !header.IsCreated ||
            header.DraftId != CommitmentDraft.IdFor(header.TenantId, header.ProgramId, header.Kind, header.Identifier) ||
            fact.Operation.Owner != new EventStreamAddress(header.TenantId.ToString(), CommitmentDraft.Area, header.DraftId.ToString()))
            throw new InvalidOperationException("A commitment directory requires one exact native source fact.");
        var scope = value switch
        {
            CommitmentDraftCreated created => (created.ProgramId, created.DraftId, 1L),
            CommitmentDraftRevised revised => (revised.ProgramId, revised.DraftId, revised.Revision),
            CommitmentReviewed reviewed => (reviewed.ProgramId, reviewed.DraftId, reviewed.Revision),
            CommitmentApproved approved => (approved.ProgramId, approved.DraftId, approved.Revision),
            CommitmentRetired retired => (retired.ProgramId, retired.SubjectId, retired.Revision),
            _ => throw new InvalidOperationException("The commitment directory received another source kind."),
        };
        if (scope != (header.ProgramId, header.DraftId, header.Revision))
            throw new InvalidOperationException("The commitment directory source has another exact owner scope.");
        return fact;
    }

    public static void RequireSuccessor(CommitmentDirectoryIdentity? current, ICommitmentFact fact)
    {
        var header = fact.Header;
        if (current is null || current.Header.TenantId != header.TenantId || current.Header.ProgramId != header.ProgramId ||
            current.Header.DraftId != header.DraftId || current.Header.Identifier != header.Identifier ||
            current.Header.Kind != header.Kind || current.Header.ServiceId != header.ServiceId || current.Header.ProviderId != header.ProviderId ||
            header.Revision < current.Header.Revision || header.Revision > current.Header.Revision + 1 ||
            fact.NativeSequence <= current.NativeSequence)
            throw new InvalidOperationException("A commitment mutation must project its exact scoped predecessor first.");
    }
}
