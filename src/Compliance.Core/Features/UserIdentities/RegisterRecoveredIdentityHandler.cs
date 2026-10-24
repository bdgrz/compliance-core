using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Registers the replacement for the account, or returns the verified existing same-account registration without
///     appending so the process continues without waiting for a new event.
/// </summary>
public sealed class RegisterRecoveredIdentityHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<RegisterRecoveredIdentity, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<RegisterRecoveredIdentity> context,
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
        var stream = IdentityRecoveryReceiverSupport.IdentityStream(frozen.ReplacementIdentityId);
        if (request.Target != stream || request.AccountId != frozen.AccountId ||
            request.ReplacementIdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not match the frozen replacement.");
        var proofs = await support.VerifyProtectionAsync(request.ProtectionProofManifest, request, frozen, ct)
            .ConfigureAwait(false);
        if (!proofs.IsSuccess)
            return Result<ReceiverOutcome>.Failure(proofs.Error!);
        string? assertedEmail;
        try
        {
            assertedEmail = (await support.Content.ReadJsonAsync<IdentityRecoveryProofProvenance>(
                IdentityRecoveryReceiverSupport.Realm, frozen.ReplacementProofProvenance, ct)
                .ConfigureAwait(false)).AssertedEmail;
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                $"The replacement proof provenance does not verify: {exception.Message}");
        }

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request, nameof(RegisterRecoveredIdentity), stream,
            frozen, verified.Value!.EventId);
        return await support.ApplyAsync(
            () => new UserIdentity(frozen.ReplacementProvider, frozen.ReplacementSubject, request.CommandId,
                nameof(RegisterRecoveredIdentity)),
            identity => identity.Acceptance, AcceptedOperationBinding.Of(provenance),
            identity =>
            {
                if (!identity.IsRegistered)
                    return null;
                // An existing same-account registration is the authoritative proof; it needs no new event.
                return identity.UserId == request.AccountId && !identity.IsRevoked
                    ? Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(NativeFactReference.Of(stream,
                        identity.RegistrationFact!)))
                    : IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                        "The replacement identity is registered to another account or revoked.");
            },
            (UserIdentity identity) => identity.RegisterRecovered(provenance, request.Intent, request.ChallengeId,
                request.AccountId, assertedEmail), context, ct).ConfigureAwait(false);
    }
}
