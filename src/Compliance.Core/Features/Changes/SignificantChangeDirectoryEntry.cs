using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Bounded discovery authority; current details are read from the exact native source.</summary>
public sealed record SignificantChangeDirectoryEntry(Uuid TenantId, Uuid ProgramId, Uuid SignificantChangeId,
    long NativeSequence, SignificantChangeAuthorityHeader Header);
