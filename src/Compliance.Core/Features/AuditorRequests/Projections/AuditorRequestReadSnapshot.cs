using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Projections;

/// <summary>Complete verified read composition; historical collections never become aggregate command authority.</summary>
public sealed record AuditorRequestReadSnapshot(AuditorRequest Owner, AuditorRequestHistorySnapshot History,
    AuditorRequestView? View);
