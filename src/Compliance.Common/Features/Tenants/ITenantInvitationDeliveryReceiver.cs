using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

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
