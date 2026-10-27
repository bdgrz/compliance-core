using Bdgrz.Compliance.Features.Policies.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>Compact discovery identity at one native source cut, including a discarded identity tombstone.</summary>
public sealed record PolicyDirectoryRow(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    string Identifier, PolicyAuthorityHeader Header, long NativeSequence);
