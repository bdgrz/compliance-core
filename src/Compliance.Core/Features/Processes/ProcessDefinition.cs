using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>
/// Code-side definition of one catalog process: its root source fact kinds, ordered stages, abort receivers and
/// the factory that turns a (receiver, target) step of a record into the typed receiver command. The factory
/// receives the deterministic command id for that step; the driver chooses it and the receiver must use it.
/// </summary>
public sealed record ProcessDefinition(string Name, IReadOnlyList<string> RootSourceEvents, bool Ordered,
    IReadOnlyList<ProcessStageDefinition> Stages, IReadOnlyList<string> Abort,
    ProcessReceiverFactory CreateReceiver);
