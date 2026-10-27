using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Processes;

/// <summary>Test-only receiver command dispatched by the process driver.</summary>
[Discriminator("test.process.receive", 1)]
sealed record ProbeReceive(Uuid CommandId, string Receiver, string? Target) : IRequest<ReceiverOutcome>,
    IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
