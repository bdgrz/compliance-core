using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

public interface ITenantInvitationDelivery
{
    ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress, string token,
        CancellationToken ct);
}

public sealed class TenantInvitationDeliveryException(string result, string failureCode)
    : Exception("Invitation delivery failed.")
{
    public string Result { get; } = result is TenantInvitationDeliveryAttempt.Rejected or
        TenantInvitationDeliveryAttempt.Unknown
        ? result
        : throw new ArgumentOutOfRangeException(nameof(result));

    public string FailureCode { get; } = string.IsNullOrWhiteSpace(failureCode)
        ? throw new ArgumentException("A delivery failure code is required.", nameof(failureCode))
        : failureCode;
}
