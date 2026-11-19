using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>Records that the provider may have accepted the message; the attempt cannot be resent automatically.</summary>
public sealed class RecordTenantInvitationDeliveryUnknownHandler(TenantInvitationDeliverySupport support)
    : IRequestHandler<RecordTenantInvitationDeliveryUnknown, ReceiverOutcome>
{
    public async ValueTask<Result<ReceiverOutcome>> HandleAsync(
        IRequestContext<RecordTenantInvitationDeliveryUnknown> context, CancellationToken ct)
    {
        var request = context.Request;
        if (string.IsNullOrWhiteSpace(request.FailureCode) || request.FailureCode.Length > 100)
            return TenantInvitationDeliverySupport.Invalid(
                "An unknown delivery code is required and must be at most 100 characters.");
        var verified = await support.VerifyAsync(request.Source, request, ct).ConfigureAwait(false);
        if (!verified.IsSuccess)
            return Result<ReceiverOutcome>.Failure(verified.Error!);
        var reference = TenantInvitationDeliverySupport.Reference(verified.Value!);
        var record = await support.Deliveries.ReadAsync(request.TenantId, request.InvitationId, ct)
            .ConfigureAwait(false);
        var attempt = record?.Attempt(request.AttemptId);
        if (record is null || attempt is null)
            return TenantInvitationDeliverySupport.Superseded("stale delivery attempt");
        if (record.Status == TenantInvitationDeliveryRecord.Unknown)
            return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Replayed(reference));
        if (record.Status is TenantInvitationDeliveryRecord.Delivered or TenantInvitationDeliveryRecord.Failed ||
            attempt.Result != TenantInvitationDeliveryAttempt.Unknown)
            return TenantInvitationDeliverySupport.Invalid("The delivery attempt is not awaiting an unknown outcome.");

        await support.UpdateAsync(request.TenantId, request.InvitationId,
            current => current.IsTerminal ? current : current with { Status = TenantInvitationDeliveryRecord.Unknown },
            ct).ConfigureAwait(false);
        return Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(reference));
    }
}
