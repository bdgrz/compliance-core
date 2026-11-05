using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record PrimaryOrgAdminSeat(Uuid UserId, Uuid MembershipEpisodeId, Uuid DesignationId, bool IsDesignated);
