using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>One immutable resolver observation of the full grant and veto routing state.</summary>
sealed record ProgramAdmissionPath(Uuid TenantId, Uuid UserId, bool Primary, Uuid TeamId, Uuid RoleId,
    string RoutingGeneration, bool AlternativesResolved, IReadOnlyList<AccessAdmissionStableRead.Participant> Participants);
