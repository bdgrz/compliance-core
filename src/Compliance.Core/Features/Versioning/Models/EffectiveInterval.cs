using Bdgrz.Compliance.Features.AccessControl.Projections;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Versioning.Models;

/// <summary>A half-open effective interval for one immutable approved version.</summary>
public readonly record struct EffectiveInterval
{
    public DateOnly From { get; }
    public DateOnly? UntilExclusive { get; }

    public EffectiveInterval(DateOnly from, DateOnly? untilExclusive)
    {
        if (untilExclusive is { } until && !CanFollow(from, until))
            throw new ArgumentOutOfRangeException(nameof(untilExclusive),
                "An effective interval must end after it begins.");
        From = from;
        UntilExclusive = untilExclusive;
    }

    public bool Contains(DateOnly date) => date >= From &&
        (UntilExclusive is null || date < UntilExclusive);

    public static bool CanFollow(DateOnly previousFrom, DateOnly nextFrom) =>
        nextFrom > previousFrom;
}
