using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

public sealed record PrimaryOrgAdminsView(Uuid TenantId, IReadOnlyList<PrimaryOrgAdminSeat> Seats)
{
    public int DesignatedCount => Seats.Count(static seat => seat.IsDesignated);
    public int PendingCount => Seats.Count - DesignatedCount;
}
