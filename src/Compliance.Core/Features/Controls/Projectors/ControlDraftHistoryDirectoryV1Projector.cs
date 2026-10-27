using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Events;
using Bdgrz.Compliance.Features.Controls.Projections;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Projectors;

public sealed partial class ControlDraftHistoryDirectoryV1Projector(IControlDraftHistoryDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant(ControlDraft.Area), FitzControlDraftHistoryDirectoryV1.ProjectorName),
        IProjectorHandler<ControlDraftCreated>, IProjectorHandler<ControlDraftRevised>, IProjectorHandler<ControlDraftDiscarded>, IProjectorHandler<ControlReviewed>, IProjectorHandler<ControlApproved>, IProjectorHandler<ControlSuccessorProposed>, IProjectorHandler<ControlRetirementProposed>, IProjectorHandler<ControlRetired>, IProjectorHandler<ControlProposalWithdrawn>, IProjectorHandler<ControlOwnerPersonDesignated>, IProjectorHandler<ResponsibilityAssigned>, IProjectorHandler<ResponsibilityRevoked>
{
    public ValueTask HandleAsync(ControlDraftCreated value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlDraftRevised value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlDraftDiscarded value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlReviewed value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlApproved value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlSuccessorProposed value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlRetirementProposed value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlRetired value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlProposalWithdrawn value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ControlOwnerPersonDesignated value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ResponsibilityAssigned value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
    public ValueTask HandleAsync(ResponsibilityRevoked value, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(value, ct);
}
