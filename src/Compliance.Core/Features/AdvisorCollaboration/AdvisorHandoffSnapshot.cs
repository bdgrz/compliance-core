using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     A hydrated handoff with its current selection read from verified content. Readers use this instead of the
///     aggregate so no reader reconstructs the selection from the stream itself.
/// </summary>
public sealed class AdvisorHandoffSnapshot(AdvisorHandoff handoff, IReadOnlyList<AdvisorRecordVersion> selection,
    string? purpose, IReadOnlyList<DomainEvent> facts)
{
    /// <summary>The hydrated handoff.</summary>
    public AdvisorHandoff Handoff { get; } = handoff;

    /// <summary>The exact selected versions, in selection order.</summary>
    public IReadOnlyList<AdvisorRecordVersion> Selection { get; } = selection;

    public IReadOnlyList<DomainEvent> Facts { get; } = facts;
    public IReadOnlyList<AdvisorFeedbackAdmitted> Admitted { get; } = [.. facts.OfType<AdvisorFeedbackAdmitted>()];
    public Uuid Id => Handoff.Id;
    public Uuid TenantId => Handoff.TenantId;
    public EventStreamAddress Stream => Handoff.Stream;
    public ulong Version => Handoff.Version;
    public Bdgrz.Compliance.Features.Proofs.Models.ImmutableContentRef? SelectionManifest => Handoff.SelectionManifest;

    /// <summary>True once the handoff was created.</summary>
    public bool IsCreated => Handoff.IsCreated;

    /// <summary>The program the handoff belongs to.</summary>
    public Uuid ProgramId => Handoff.ProgramId;

    /// <summary>The current status.</summary>
    public string Status => Handoff.Status;

    /// <summary>The logical revision.</summary>
    public long Revision => Handoff.Revision;

    /// <summary>The selection generation.</summary>
    public long Generation => Handoff.Generation;

    /// <summary>The purpose stated at creation.</summary>
    public string? Purpose { get; } = purpose;

    /// <summary>The last day the handoff stays shared.</summary>
    public DateOnly ExpiresOn => Handoff.ExpiresOn;

    /// <summary>Who created the handoff.</summary>
    public ActorReference? CreatedBy => Handoff.CreatedBy;

    /// <summary>When the handoff was created.</summary>
    public DateTimeOffset? CreatedAt => Handoff.CreatedAt;

    /// <summary>When the latest fact was recorded.</summary>
    public DateTimeOffset? UpdatedAt => Handoff.UpdatedAt;

    /// <summary>The ended handoff this selection is compared with.</summary>
    public Uuid? ComparedToHandoffId => Handoff.ComparedToHandoffId;

    /// <summary>True once the handoff was opened for review at least once.</summary>
    public bool WasReadyForReview => Handoff.WasReadyForReview;
}
