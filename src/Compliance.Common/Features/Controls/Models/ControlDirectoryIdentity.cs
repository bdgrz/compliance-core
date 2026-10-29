using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>Compact discovery metadata only; no draft body, history or authorization decision is projected.</summary>
public sealed record ControlDirectoryIdentity(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, ControlAuthorityHeader Header);
