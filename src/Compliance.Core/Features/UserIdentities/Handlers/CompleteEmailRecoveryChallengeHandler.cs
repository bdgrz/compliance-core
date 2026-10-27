using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Completes the original email challenge of a finished recovery. Only that challenge is completed, so a newer
///     challenge slot on the address is left exactly as it is.
/// </summary>
public sealed class CompleteEmailRecoveryChallengeHandler(IdentityRecoveryReceiverSupport support, TimeProvider clock)
    : IRequestHandler<CompleteEmailRecoveryChallenge, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<CompleteEmailRecoveryChallenge> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyRecoveryFactAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        if (request.Target.Realm != IdentityRecoveryReceiverSupport.Realm || request.Target.Area != "email-addresses" ||
            !Uuid.TryParse(request.Target.Resource, System.Globalization.CultureInfo.InvariantCulture, out var addressId))
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The command does not target an email address.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(CompleteEmailRecoveryChallenge),
            request.Target, frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => new EmailAddress(addressId, request.CommandId, nameof(CompleteEmailRecoveryChallenge), request.ChallengeId),
            address => address.Acceptance, AcceptedOperationBinding.Of(provenance),
            address => address.ClaimOf(request.ChallengeId) is { } claim && claim.OldIdentityId == frozen.OldIdentityId &&
                       claim.ReplacementIdentityId == frozen.ReplacementIdentityId && claim.Fact.UserId == frozen.AccountId
                ? null
                : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>("The challenge was not claimed for this recovery."),
            (EmailAddress address) => address.CompleteIdentityRecovery(provenance, request.Intent, frozen.AccountId,
                request.ChallengeId, frozen.OldIdentityId, frozen.ReplacementIdentityId, clock.GetUtcNow()),
            context, ct).ConfigureAwait(false);
    }
}
