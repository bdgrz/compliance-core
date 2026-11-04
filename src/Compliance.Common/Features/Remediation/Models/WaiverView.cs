using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

public sealed record WaiverView(Uuid TenantId, Uuid WaiverId, long Revision, string Rule,
    IReadOnlyList<WaiverScopeRecord> Scope, string Rationale, string CompensatingAction,
    DateTimeOffset ExpiresAt, ActorReference RequestedBy, DateTimeOffset RequestedAt,
    string State, bool IsEffective, ActorReference? ReviewedBy, DateTimeOffset? ReviewedAt,
    string? ReviewComments, ActorReference? RevokedBy, DateTimeOffset? RevokedAt, string? RevocationReason);
