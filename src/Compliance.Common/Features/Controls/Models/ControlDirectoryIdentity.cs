using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Compact discovery metadata only; no draft body, history or authorization decision is projected.</summary>
public sealed record ControlDirectoryIdentity(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, ControlAuthorityHeader Header);
