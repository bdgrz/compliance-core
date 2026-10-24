using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The attributable reason a resolved request was reopened.</summary>
public sealed record AuditorRequestReopenContent(string Reason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset At);
