using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Verified explicit read composition; history and complete text are never retained by the command owner.</summary>
public sealed class AdvisorFeedbackThreadSnapshot(AdvisorFeedbackThread owner, IReadOnlyList<DomainEvent> facts,
    AdvisorFeedbackRecordedContent? recorded, IReadOnlyList<AdvisorFeedbackCorrectionView> corrections,
    IReadOnlyList<AdvisorFeedbackResponseView> responses)
{
    public AdvisorFeedbackThread Owner { get; } = owner;
    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public AdvisorFeedbackRecordedContent? Recorded { get; } = recorded;
    public IReadOnlyList<AdvisorFeedbackCorrectionView> Corrections { get; } = corrections;
    public IReadOnlyList<AdvisorFeedbackResponseView> Responses { get; } = responses;
    public AdvisorFeedbackResolved? Resolved => Owner.Resolved;
    public bool IsMaterialized => Owner.IsMaterialized;
    public Uuid Id => Owner.Id;
    public EventStreamAddress Stream => Owner.Stream;
    public ulong Version => Owner.Version;
    public Uuid TenantId => Owner.TenantId;
    public Uuid ProgramId => Owner.ProgramId;
    public Uuid HandoffId => Owner.HandoffId;
    public Uuid FeedbackId => Owner.FeedbackId;
    public long Revision => Owner.Revision;
}
