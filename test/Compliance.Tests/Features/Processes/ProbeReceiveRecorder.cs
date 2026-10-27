using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Processes;

/// <summary>Records every dispatched receiver command and scripts its outcome.</summary>
sealed class ProbeReceiveRecorder
{
    readonly List<ProbeReceive> _calls = [];
    readonly Dictionary<string, Func<ProbeReceive, Result<ReceiverOutcome>>> _scripts = new(StringComparer.Ordinal);

    public IReadOnlyList<ProbeReceive> Calls => _calls;

    public void Script(string receiver, Func<ProbeReceive, Result<ReceiverOutcome>> outcome) =>
        _scripts[receiver] = outcome;

    public Result<ReceiverOutcome> Handle(ProbeReceive command)
    {
        _calls.Add(command);
        return _scripts.TryGetValue(command.Receiver, out var script)
            ? script(command)
            : Result<ReceiverOutcome>.Success(new ReceiverOutcome.Applied(new(
                new EventStreamAddress("tenant-a", "targets", command.Target ?? command.Receiver), command.CommandId, 1)));
    }
}
