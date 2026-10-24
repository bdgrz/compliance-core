using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

/// <summary>Seeds corrective actions owned by one member, each due on the given dates and materialized.</summary>
static class WorkTestData
{
    public static async Task<FindingView> AddActionsAsync(OperationsFixture fixture,
        Uuid ownerMemberId, params DateOnly[] dueDates)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.LeadMemberId);
        foreach (var dueOn in dueDates)
            finding = await fixture.AddActionAsync(finding, ownerMemberId, dueOn, $"Fix item due {dueOn:yyyy-MM-dd}.");
        return finding;
    }
}
