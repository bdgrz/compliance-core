using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Projections;

static class IndependenceRuleVersionProjection
{
    public static IReadOnlyList<IndependenceRuleVersionView> ApplyRatifications(
        IReadOnlyList<IndependenceRuleVersionView> versions,
        IReadOnlyList<IndependenceRuleRatificationView> ratifications)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(ratifications);
        return Array.AsReadOnly(versions.Select(version => ApplyRatification(version,
            ratifications.SingleOrDefault(item => item.RuleVersion == version.Version))).ToArray());
    }

    public static IndependenceRuleVersionView ApplyRatification(IndependenceRuleVersionView version,
        IndependenceRuleRatificationView? ratification)
    {
        if (ratification is null || ratification.RuleContentDigest != IndependenceSourceDigest.RuleContent(version.Content))
            return version with { IsRatified = false, Ratification = null };
        return version with { IsRatified = true, Ratification = ratification };
    }
}
