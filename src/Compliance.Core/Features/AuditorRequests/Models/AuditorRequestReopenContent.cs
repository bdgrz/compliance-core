using Cntryl.Portia;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>The attributable reason a resolved request was reopened.</summary>
public sealed record AuditorRequestReopenContent(string Reason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset At);
