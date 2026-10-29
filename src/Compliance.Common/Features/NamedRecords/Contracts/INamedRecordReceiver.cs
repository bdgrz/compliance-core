using Cntryl.Portia;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.NamedRecords.Contracts;

/// <summary>An internal receiver command of a name-change process; only the system principal may send it.</summary>
public interface INamedRecordReceiver : IRequest<ReceiverOutcome>, IIdentifiedRequest
{
    /// <summary>The deterministic command id the process driver chose for this receiver step.</summary>
    Uuid CommandId { get; }
}
