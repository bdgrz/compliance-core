using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>A retained designation; authority additionally requires current active staff status.</summary>
public sealed record PartnerDesignationView(Uuid UserId, string Duty, long Revision, bool IsDesignated,
    string Reason, Uuid ActorUserId, DateTimeOffset RecordedAt);
