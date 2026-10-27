using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Projectors;

/// <summary>Projects approved Control versions and lifecycle changes into impact references.</summary>
public sealed partial class ApplicationControlVersionReferenceProjector(
    IApplicationControlVersionReferenceProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("controls"),
        FitzApplicationControlVersionReferenceDirectory.ProjectorName),
      IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftDiscarded>,
      IProjectorHandler<ControlSuccessorProposed>,
      IProjectorHandler<ControlRetirementProposed>,
      IProjectorHandler<ControlProposalWithdrawn>, IProjectorHandler<ControlApproved>,
      IProjectorHandler<ControlRetired>
{
    public ValueTask HandleAsync(ControlDraftCreated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlDraftDiscarded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlSuccessorProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlRetirementProposed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlProposalWithdrawn ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlApproved ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(ControlRetired ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
}
