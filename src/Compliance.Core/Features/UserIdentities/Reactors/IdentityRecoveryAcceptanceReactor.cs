using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Events;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Authorizers;
namespace Bdgrz.Compliance.Features.UserIdentities.Reactors;

/// <summary>
///     Starts the identity recovery process when a challenge is claimed on an email address. The record exists, with
///     the frozen intent and both identity slots as its canonical targets, before the source checkpoint advances.
/// </summary>
public sealed partial class IdentityRecoveryAcceptanceReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process)
    : Reactor(checkpoints, EventStreamPattern.ForPattern("bdgrz", "email-addresses"), "IdentityRecoveryAcceptanceV1"),
      IReactorHandler<IdentityRecoveryChallengeClaimed>
{
    public async ValueTask HandleAsync(IReactorContext<IdentityRecoveryChallengeClaimed> context, CancellationToken ct)
    {
        var claimed = context.Trigger;
        await process.StartAsync(IdentityRecoveryProcess.Name, context, claimed.Intent,
            [claimed.OldIdentityId.ToString(), claimed.ReplacementIdentityId.ToString()],
            IdentityRecoveryReactors.Acceptance, ct).ConfigureAwait(false);
    }
}
