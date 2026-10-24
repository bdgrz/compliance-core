using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     A live team assignment proven by native facts: the member, the membership episode it was made for, the
///     assignment fact itself, the assignment generation and the owner revision after its last fact.
/// </summary>
/// <param name="MemberId">The assigned member.</param>
/// <param name="MembershipEpisodeId">The membership episode the assignment was made for.</param>
/// <param name="AssignmentEventId">The assignment fact that opened this episode.</param>
/// <param name="Generation">The number of assignments ever made on the owner, including this one.</param>
/// <param name="Revision">The owner revision after its last fact.</param>
sealed record TeamAssignmentSource(Uuid MemberId, Uuid MembershipEpisodeId, Uuid AssignmentEventId, ulong Generation,
    ulong Revision);
