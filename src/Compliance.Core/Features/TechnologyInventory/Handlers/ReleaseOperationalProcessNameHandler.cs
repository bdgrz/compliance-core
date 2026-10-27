using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Commands;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.NamedRecords;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory.Handlers;

/// <summary>
///     Releases a operational process name claim only for the exact holder and generation, after the record's own fact proves the old
///     name inactive or the change canceled; any other claim state is a terminal no-op.
/// </summary>
public sealed class ReleaseOperationalProcessNameHandler(NamedRecordReceivers receivers)
    : IRequestHandler<ReleaseOperationalProcessName, ReceiverOutcome>
{
    /// <inheritdoc />
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(IRequestContext<ReleaseOperationalProcessName> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var verified = request.Source switch
        {
            ReleaseOperationalProcessNameSourceProof.AppliedProof applied =>
                await receivers.VerifyAsync(applied.Fact, ct).ConfigureAwait(false),
            ReleaseOperationalProcessNameSourceProof.CanceledProof canceled =>
                await receivers.VerifyAsync(canceled.Fact, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown release source proof."),
        };
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        return await receivers.ReleaseAsync(OperationalProcessNames.Descriptor, new NameChangeStep(request.CommandId,
            request.Target, request.OperationId, request.Generation, request.ExpectedRevision, request.Intent,
            request.RecordId), request.NormalizedName, verified.Value!,
            (OperationalProcessNameClaim claim, ClaimAttempt attempt) => claim.Release(attempt.Operation, attempt.Holder,
                attempt.ExpectedRevision, attempt.Generation, attempt.Intent), context, ct).ConfigureAwait(false);
    }
}
