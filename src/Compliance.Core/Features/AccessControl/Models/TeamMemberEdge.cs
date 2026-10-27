using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

sealed record TeamMemberEdge(Uuid TeamId, Uuid MemberId, Uuid MembershipEpisodeId = default);
