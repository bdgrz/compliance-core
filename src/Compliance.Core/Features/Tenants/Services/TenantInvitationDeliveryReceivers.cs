using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>The receiver names of the invitation delivery process, as the process catalog spells them.</summary>
public static class TenantInvitationDeliveryReceivers
{
    public const string SendTenantInvitation = nameof(SendTenantInvitation);
    public const string RecordTenantInvitationDeliverySent = nameof(RecordTenantInvitationDeliverySent);
    public const string RecordTenantInvitationDeliveryFailed = nameof(RecordTenantInvitationDeliveryFailed);
}
