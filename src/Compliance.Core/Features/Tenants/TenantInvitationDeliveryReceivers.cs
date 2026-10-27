using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>The receiver names of the invitation delivery process, as the process catalog spells them.</summary>
public static class TenantInvitationDeliveryReceivers
{
    public const string SendTenantInvitation = nameof(SendTenantInvitation);
    public const string RecordTenantInvitationDeliverySent = nameof(RecordTenantInvitationDeliverySent);
    public const string RecordTenantInvitationDeliveryFailed = nameof(RecordTenantInvitationDeliveryFailed);
}
