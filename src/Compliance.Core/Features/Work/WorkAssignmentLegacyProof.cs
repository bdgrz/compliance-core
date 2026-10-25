using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Exact physical/native immutable cut of the read-only program-wide ledger, never a timestamp estimate.</summary>
public sealed record WorkAssignmentLegacyProof(EventStreamAddress Stream, ulong Version, ulong Through, string PrefixSha256);
