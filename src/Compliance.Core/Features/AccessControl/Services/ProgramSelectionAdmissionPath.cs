using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>One immutable, complete selection-route observation for the exact program.</summary>
sealed record ProgramSelectionAdmissionPath(Uuid TenantId, Uuid ProgramId, Uuid UserId, bool Primary,
    Uuid TeamId, Uuid RoleId, AccessGrantScope? Scope, string RoutingGeneration, bool AlternativesResolved,
    IReadOnlyList<AccessAdmissionStableRead.Participant> Participants);
