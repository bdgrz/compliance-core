using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

/// <summary>Identity of one durable process record: one per process kind and root operation in a realm.</summary>
public sealed record ProcessKey(string ProcessKind, string Realm, Uuid RootOperationId);
