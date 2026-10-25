using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>
///     Settles the protection of one identity slot. A completed swap releases its exact matching protection; a
///     rejected preparation fences the slot even when it was never protected, so a delayed protection finds it settled.
/// </summary>
public sealed class SettleRecoveryIdentityProtectionHandler(IdentityRecoveryReceiverSupport support)
    : IRequestHandler<SettleRecoveryIdentityProtection, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<SettleRecoveryIdentityProtection> context, CancellationToken ct)
    {
        var request = context.Request;
        var source = request.Source switch
        {
            SettleRecoveryIdentityProtectionSourceProof.CompletedProof completed =>
                await VerifyAsync(completed.Fact, request, ct).ConfigureAwait(false),
            SettleRecoveryIdentityProtectionSourceProof.RejectedProof rejected =>
                await VerifyAsync(rejected.Fact, request, ct).ConfigureAwait(false),
            _ => Result<Uuid>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The source proof is not a recovery settlement fact.", false)),
        };
        if (!source.IsSuccess)
            return Result<ReceiverOutcome>.Failure(source.Error!);
        var intent = await support.ReadIntentAsync(request, ct).ConfigureAwait(false);
        if (!intent.IsSuccess)
            return Result<ReceiverOutcome>.Failure(intent.Error!);
        var frozen = intent.Value!;
        var stream = IdentityRecoveryReceiverSupport.IdentityStream(request.IdentityId);
        if (request.Target != stream || request.IdentityId != frozen.OldIdentityId &&
            request.IdentityId != frozen.ReplacementIdentityId)
            return IdentityRecoveryReceiverSupport.Invalid<ReceiverOutcome>(
                "The command does not name a slot of this recovery.");

        var provenance = IdentityRecoveryReceiverSupport.Provenance(request,
            nameof(SettleRecoveryIdentityProtection), stream, frozen, source.Value);
        return await support.ApplyAsync(
            () => new UserIdentity(request.IdentityId, request.CommandId, nameof(SettleRecoveryIdentityProtection), request.ChallengeId),
            identity => identity.Acceptance, AcceptedOperationBinding.Of(provenance),
            identity => identity.IsSettledFor(request.ChallengeId)
                ? IdentityRecoveryReceiverSupport.Superseded("The slot protection was already settled.")
                : null,
            (UserIdentity identity) => identity.Settle(provenance, request.Intent, request.Source.Proof.RequiredFacts,
                request.ChallengeId, request.Generation), context, ct).ConfigureAwait(false);
    }

    async Task<Result<Uuid>> VerifyAsync<TFact>(SourceFactProof<TFact> proof, SettleRecoveryIdentityProtection request,
        CancellationToken ct) where TFact : DomainEvent, IIdentityRecoveryFact
    {
        var verified = await support.VerifyRecoveryFactAsync(proof, request, ct).ConfigureAwait(false);
        return verified.IsSuccess
            ? Result<Uuid>.Success(verified.Value!.EventId)
            : Result<Uuid>.Failure(verified.Error!);
    }
}
