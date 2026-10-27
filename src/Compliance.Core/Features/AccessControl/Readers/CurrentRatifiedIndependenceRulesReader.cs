using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Projections;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Readers;

sealed class CurrentRatifiedIndependenceRulesReader(IndependenceRuleCatalogReader rules,
    IndependenceRuleRatificationReader ratifications, ProfessionalDutyAuthorityReader duties)
{
    public async ValueTask<IndependenceRuleVersionView?> ReadActiveAsync(CancellationToken ct)
    {
        var current = await ratifications.ReadAsync(ct).ConfigureAwait(false);
        if (current.Active is not { } active)
            return null;
        var currentDuty = await duties.ReadCurrentAsync(active.UserId, FirmProfessionalDuty.RuleRatifier, null, ct)
            .ConfigureAwait(false);
        if (currentDuty is null || currentDuty.Staff.StaffMemberId != active.StaffMemberId ||
            currentDuty.Staff.Revision != active.DirectoryStaffRevision ||
            currentDuty.Designation.UserId != active.UserId ||
            currentDuty.Designation.Duty != FirmProfessionalDuty.RuleRatifier ||
            currentDuty.Designation.TenantId is not null ||
            currentDuty.Designation.DesignationId != active.DutyDesignationId ||
            active.DutyRevision < currentDuty.ContinuityStartRevision ||
            active.DutyRevision > currentDuty.Designation.Revision)
            return null;
        var draft = await rules.FindVersionAsync(active.RuleVersion, ct).ConfigureAwait(false);
        if (draft is null)
            return null;
        var projected = IndependenceRuleVersionProjection.ApplyRatification(draft, active);
        return projected.IsRatified ? projected : null;
    }
}
