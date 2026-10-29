using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>An attributable lifecycle transition retained with the request.</summary>
public sealed record AuditorRequestHistoryEntry(
    long Revision,
    string Action,
    string Detail,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset OccurredAt);
