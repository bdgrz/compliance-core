using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

/// <summary>Test-only receiver command dispatched by the process driver.</summary>
[Discriminator("test.process.receive", 1)]
sealed record ProbeReceive(Uuid CommandId, string Receiver, string? Target) : IRequest<ReceiverOutcome>,
    IIdentifiedRequest
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
