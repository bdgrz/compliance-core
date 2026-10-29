using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Tenants.Contracts;

/// <summary>An internal receiver command of the invitation delivery process; only the system principal may send it.</summary>
public interface ITenantInvitationDeliveryReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }

    /// <summary>The tenant the invitation belongs to.</summary>
    Uuid TenantId { get; }

    /// <summary>The invitation the delivery concerns.</summary>
    Uuid InvitationId { get; }
}
