using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Events;
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
