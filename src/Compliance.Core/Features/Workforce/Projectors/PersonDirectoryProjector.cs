using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Projections;
namespace Bdgrz.Compliance.Features.Workforce.Projectors;

public sealed partial class PersonDirectoryProjector(IPersonDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForTenant("people"), "PersonDirectoryV2"),
      IProjectorHandler<PersonRecorded>, IProjectorHandler<PersonRevised>,
      IProjectorHandler<PersonMembershipCorrelated>, IProjectorHandler<PersonRegistrationRecorded>,
      IProjectorHandler<PersonDetailsRevised>, IProjectorHandler<PersonMembershipCorrelationRecorded>
{
    public ValueTask HandleAsync(PersonRecorded ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PersonRevised ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(PersonMembershipCorrelated ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, ct);
    public ValueTask HandleAsync(PersonRegistrationRecorded ev, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
    public ValueTask HandleAsync(PersonDetailsRevised ev, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);
    public ValueTask HandleAsync(PersonMembershipCorrelationRecorded ev, IProjectorContext context, CancellationToken ct) => projection.ApplyAsync(ev, ct);

}
