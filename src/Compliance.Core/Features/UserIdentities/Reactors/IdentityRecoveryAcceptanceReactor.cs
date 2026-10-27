using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

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
