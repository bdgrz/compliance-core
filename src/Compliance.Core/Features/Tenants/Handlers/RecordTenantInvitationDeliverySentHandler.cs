using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Records the delivered outcome of the one attempt that sent. A callback of another attempt is stale and
///     changes nothing; an outcome that contradicts the stored attempt result is a permanent conflict.
/// </summary>
public sealed class RecordTenantInvitationDeliverySentHandler(TenantInvitationDeliverySupport support)
    : IRequestHandler<RecordTenantInvitationDeliverySent, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<RecordTenantInvitationDeliverySent> context, CancellationToken ct)
    {
        var request = context.Request;
        var verified = await support.VerifyAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var reference = TenantInvitationDeliverySupport.Reference(verified.Value!);
        var record = await support.Deliveries.ReadAsync(request.TenantId, request.InvitationId, ct).ConfigureAwait(false);
        var attempt = record?.Attempt(request.AttemptId);
        if (record is null || attempt is null)
            return TenantInvitationDeliverySupport.Superseded("stale delivery attempt");
        if (record.Status == TenantInvitationDeliveryRecord.Delivered)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(reference));
        if (record.Status == TenantInvitationDeliveryRecord.Failed ||
            attempt.Result != TenantInvitationDeliveryAttempt.Accepted)
            return TenantInvitationDeliverySupport.Invalid("The delivery attempt did not succeed.");

        await support.UpdateAsync(request.TenantId, request.InvitationId,
            current => current.IsTerminal ? current : current with { Status = TenantInvitationDeliveryRecord.Delivered },
            ct).ConfigureAwait(false);
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(reference));
    }
}
