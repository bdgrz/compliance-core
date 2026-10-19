using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

public sealed partial class AdvisorHandoffDirectoryProjector(IAdvisorHandoffDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("advisor-handoffs"),
        FitzAdvisorHandoffDirectory.ProjectorName),
      IProjectorHandler<AdvisorHandoffCreated>,
      IProjectorHandler<AdvisorHandoffSelectionRevised>,
      IProjectorHandler<AdvisorHandoffReadiedForReview>,
      IProjectorHandler<AdvisorHandoffClosed>,
      IProjectorHandler<AdvisorHandoffRevoked>,
      IProjectorHandler<AdvisorFeedbackRecorded>,
      IProjectorHandler<AdvisorFeedbackCorrected>,
      IProjectorHandler<AdvisorFeedbackResponded>,
      IProjectorHandler<AdvisorFeedbackResolved>
{
    public ValueTask HandleAsync(AdvisorHandoffCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorHandoffSelectionRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorHandoffReadiedForReview ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorHandoffClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorHandoffRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorFeedbackRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorFeedbackCorrected ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorFeedbackResponded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(AdvisorFeedbackResolved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
