using Bdgrz.Compliance.Features.AdvisorCollaboration;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Projectors;

/// <summary>
///     Projects each handoff's lifecycle and feedback admissions into the directory read model. The selection of a
///     creation or revision is read from verified content before it is projected; feedback threads are their own
///     owners and are not projected here.
/// </summary>
public sealed partial class AdvisorHandoffDirectoryProjector(IAdvisorHandoffDirectoryProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(AdvisorHandoff.Area),
        FitzAdvisorHandoffDirectory.ProjectorName),
      IProjectorHandler<AdvisorHandoffCreated>,
      IProjectorHandler<AdvisorHandoffSelectionRevised>,
      IProjectorHandler<AdvisorHandoffReadiedForReview>,
      IProjectorHandler<AdvisorHandoffClosed>,
      IProjectorHandler<AdvisorHandoffRevoked>,
      IProjectorHandler<AdvisorFeedbackAdmitted>
{
    public async ValueTask HandleAsync(AdvisorHandoffCreated ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await AdvisorContent
            .ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, ev.TenantId, ev.RecordVersionsManifest, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);

    public async ValueTask HandleAsync(AdvisorHandoffSelectionRevised ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await AdvisorContent
            .ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, ev.TenantId, ev.RecordVersionsManifest, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);

    public ValueTask HandleAsync(AdvisorHandoffReadiedForReview ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorHandoffClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorHandoffRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorFeedbackAdmitted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);
}
