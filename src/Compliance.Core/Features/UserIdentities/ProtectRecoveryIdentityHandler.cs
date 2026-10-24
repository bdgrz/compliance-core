using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Protects one identity slot of a prepared recovery. A competing association that is already definite rejects
///     the preparation; another recovery of the same account holding the slot is a wait; a slot whose protection for
///     this challenge was already settled, and a rejected preparation, make a delayed protection a terminal no-op.
/// </summary>
public sealed class ProtectRecoveryIdentityHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<ProtectRecoveryIdentity, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ProtectRecoveryIdentity> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var slot = request.IdentityRole == RecoveryIdentityRole.Old ? frozen.OldIdentityId : frozen.ReplacementIdentityId;
        var stream = IdentityRecoveryReceiverSupport.IdentityStream(request.IdentityId);
        if (request.IdentityId != slot || request.Target != stream || request.AccountId != frozen.AccountId ||
            request.OldIdentityId != frozen.OldIdentityId || request.ReplacementIdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not match the frozen candidate and identity role.");

        var recovery = await support.Reader.HydrateAsync(IdentityRecoveryReceiverSupport.RecoveryOf(request.ChallengeId),
            ct).ConfigureAwait(false);
        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(ProtectRecoveryIdentity), stream,
            frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => new UserIdentity(request.IdentityId, request.CommandId, nameof(ProtectRecoveryIdentity)),
            identity => identity.Acceptance, AcceptedOperationBinding.Of(provenance),
            identity =>
            {
                if (recovery.State == IdentityRecovery.Stage.Rejected)
                    return IdentityRecoveryReceiverSupport.Superseded("The preparation was rejected.");
                var decision = identity.DecideProtection(request.ChallengeId, request.AccountId, request.IdentityRole,
                    frozen.ReplacementIdentityId);
                return decision.Outcome switch
                {
                    UserIdentity.ProtectionOutcome.Proceed => null,
                    UserIdentity.ProtectionOutcome.Wait => IdentityRecoveryReceiverSupport.Wait(decision.Reason),
                    UserIdentity.ProtectionOutcome.Fenced => IdentityRecoveryReceiverSupport.Superseded(decision.Reason),
                    _ => recovery.State == IdentityRecovery.Stage.Prepared
                        ? IdentityRecoveryReceiverSupport.Rejected(decision.Reason)
                        : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                            "An accepted swap cannot be rejected: " + decision.Reason),
                };
            },
            (UserIdentity identity) => identity.Protect(provenance, request.Intent, request.Source.RequiredFacts,
                request.ChallengeId, request.AccountId, request.IdentityRole, frozen.ReplacementIdentityId,
                request.Generation), context, ct).ConfigureAwait(false);
    }
}
