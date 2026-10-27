using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Verified complete permanent commitments and assignments, transient and uncapped.</summary>
public sealed class PracticeCommitmentHistorySnapshot
{
    readonly PracticeIndependenceCommitment _owner;
    readonly IReadOnlyDictionary<SourceUseSlot, PracticeIndependenceCommitted> _bySlot;

    internal PracticeCommitmentHistorySnapshot(PracticeIndependenceCommitment owner,
        IReadOnlyList<PracticeIndependenceCommitted> commitments,
        IReadOnlyDictionary<SourceUseSlot, PracticeIndependenceCommitted> bySlot)
    {
        _owner = owner;
        Commitments = commitments;
        _bySlot = bySlot;
    }

    public IReadOnlyList<PracticeIndependenceCommitted> Commitments { get; }
    public PracticeIndependenceCommitted? CommitmentOf(ReservationKey key) =>
        _bySlot.GetValueOrDefault(SourceUseSlot.Of(_owner.Stream, key));
    public IReadOnlyList<EngagementAssignment> Assignments() => Commitments.Select(committed =>
        _owner.Kind == PracticeSubjectKind.Staff
            ? new EngagementAssignment(_owner.TenantId, committed.EngagementId, _owner.SubjectId, Uuid.Empty,
                ServiceEngagementRules.PracticeOf(committed.Practice))
            : new EngagementAssignment(_owner.TenantId, committed.EngagementId, Uuid.Empty, _owner.SubjectId,
                ServiceEngagementRules.PracticeOf(committed.Practice))).ToArray();
}
