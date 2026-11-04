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
    public async ValueTask<Result> HandleAsync(IProjectorContext<EmailAddressReserved> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }

    public async ValueTask<Result> HandleAsync(IProjectorContext<EmailAddressVerified> context, CancellationToken ct)
    {
        var ev = context.Trigger;
        await projection.ApplyAsync(ev, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
