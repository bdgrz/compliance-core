using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Acquires or re-pins the location name claim a pending change needs; another holder is a definite refusal.</summary>
public sealed class ClaimLocationNameHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ClaimLocationName, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ClaimLocationName> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = await receivers.VerifyAsync(request.Source, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ClaimAsync(LocationNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), request.NameKind, request.NormalizedName, verified.Value!,
            (LocationNameClaim claim, ClaimAttempt attempt) => claim.Claim(attempt.Operation, attempt.Holder,
                attempt.ExpectedRevision, attempt.Generation, attempt.Intent), context, ct).ConfigureAwait(false);
    }
}
