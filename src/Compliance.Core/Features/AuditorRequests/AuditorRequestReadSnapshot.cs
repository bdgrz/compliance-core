using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Complete verified read composition; historical collections never become aggregate command authority.</summary>
public sealed record AuditorRequestReadSnapshot(AuditorRequest Owner, AuditorRequestHistorySnapshot History,
    AuditorRequestView? View);
