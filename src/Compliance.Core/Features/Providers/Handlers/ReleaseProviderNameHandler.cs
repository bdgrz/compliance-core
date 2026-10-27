using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers.Handlers;

/// <summary>
///     Releases a provider name claim only for the exact holder and generation, after the record's own fact proves the old
///     name inactive or the change canceled; any other claim state is a terminal no-op.
/// </summary>
public sealed class ReleaseProviderNameHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ReleaseProviderName, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseProviderName> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = request.Source switch
        {
            ReleaseProviderNameSourceProof.RecordedProof recorded =>
                await receivers.VerifyAsync(recorded.Fact, ct).ConfigureAwait(false),
            ReleaseProviderNameSourceProof.RevisedProof revised =>
                await receivers.VerifyAsync(revised.Fact, ct).ConfigureAwait(false),
            ReleaseProviderNameSourceProof.CanceledProof canceled =>
                await receivers.VerifyAsync(canceled.Fact, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown release source proof."),
        };
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ReleaseAsync(ProviderNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), request.NormalizedName, verified.Value!,
            (ProviderNameClaim claim, ClaimAttempt attempt) => claim.Release(attempt.Operation, attempt.Holder,
                attempt.ExpectedRevision, attempt.Generation, attempt.Intent), context, ct).ConfigureAwait(false);
    }
}
