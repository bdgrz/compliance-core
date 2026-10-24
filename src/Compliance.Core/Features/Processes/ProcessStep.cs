using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>One dispatch of a receiver for a record: the step's deterministic command id and optional target key.</summary>
public sealed record ProcessStep(ProcessRecord Record, string Stage, string Receiver, string? Target, Uuid CommandId);
