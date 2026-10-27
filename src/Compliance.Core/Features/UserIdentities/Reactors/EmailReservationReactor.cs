using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities.Reactors;

public sealed partial class EmailReservationReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "user-identities")),
      IReactorHandler<UserIdentityRegistered>
{
    public async ValueTask HandleAsync(IReactorContext<UserIdentityRegistered> context, CancellationToken ct)
    {
        if (context.Trigger.EmailAddress is not { } address)
            return;

        // The command id is derived from the registration it reacts to, so a replayed reaction is the same command;
        // the reactor reserves as the system and does not constrain the address revision.
        var commandId = EffectIdentity.CommandId(context.Trigger.Metadata.EventId, nameof(ReserveEmail), address, null);
        var result = await bus.SendAsync(new ReserveEmail(commandId, context.Trigger.UserId, address, -1), context, ct)
            .ConfigureAwait(false);
        // A second provider identity may assert an address already owned by another user. That
        // conflict is terminal for this event; retrying it would block all later registrations.
        if (!result.IsSuccess && result.Error.Kind != RequestErrorKind.Conflict)
            throw new ReactionCommandFailedException(result.Error);
    }
}
