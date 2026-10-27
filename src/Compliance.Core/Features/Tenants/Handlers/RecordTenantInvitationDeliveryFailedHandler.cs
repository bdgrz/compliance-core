using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Records the failed outcome of the one attempt that was rejected. A callback of another attempt is stale and
///     changes nothing; an outcome that contradicts the stored attempt result is a permanent conflict.
/// </summary>
public sealed class RecordTenantInvitationDeliveryFailedHandler(TenantInvitationDeliverySupport support)
    : IRequestHandler<RecordTenantInvitationDeliveryFailed, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<RecordTenantInvitationDeliveryFailed> context, CancellationToken ct)
    {
        var request = context.Request;
        if (string.IsNullOrWhiteSpace(request.FailureCode) || request.FailureCode.Length > 100)
            return TenantInvitationDeliverySupport.Invalid(
                "A delivery failure code is required and must be at most 100 characters.");
        var verified = await support.VerifyAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var reference = TenantInvitationDeliverySupport.Reference(verified.Value!);
        var record = await support.Deliveries.ReadAsync(request.TenantId, request.InvitationId, ct).ConfigureAwait(false);
        var attempt = record?.Attempt(request.AttemptId);
        if (record is null || attempt is null)
            return TenantInvitationDeliverySupport.Superseded("stale delivery attempt");
        if (record.Status == TenantInvitationDeliveryRecord.Failed)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(reference));
        if (record.Status == TenantInvitationDeliveryRecord.Delivered ||
            attempt.Result != TenantInvitationDeliveryAttempt.Rejected)
            return TenantInvitationDeliverySupport.Invalid("The delivery attempt did not fail.");

        await support.UpdateAsync(request.TenantId, request.InvitationId,
            current => current.IsTerminal ? current : current with { Status = TenantInvitationDeliveryRecord.Failed },
            ct).ConfigureAwait(false);
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(reference));
    }
}
