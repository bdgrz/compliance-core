using Cntryl.Portia;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Projections;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Projectors;

public sealed partial class EmailAddressDirectoryProjector(IEmailAddressDirectoryProjection projection)
    : Projector(projection, EventStreamPattern.ForPattern("bdgrz", "email-addresses"), "EmailAddressDirectory"),
      IProjectorHandler<EmailAddressReserved>,
      IProjectorHandler<EmailAddressVerified>
{
    public ValueTask HandleAsync(EmailAddressReserved ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);

    public ValueTask HandleAsync(EmailAddressVerified ev, IProjectorContext context, CancellationToken ct) =>
        projection.ApplyAsync(ev, ct);
}
