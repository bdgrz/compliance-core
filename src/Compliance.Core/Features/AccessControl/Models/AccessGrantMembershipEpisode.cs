using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

sealed record AccessGrantMembershipEpisode(Uuid GrantId, Uuid? MembershipEpisodeId);
