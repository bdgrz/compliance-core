using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;

namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class ProgramRiskTriggerTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordNoAffectedRisksGivenReasonedTriageAndPreserveExactRetry()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var owner = new RiskReassessmentTrigger(tenant, id);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var raise = new RaiseRiskReassessmentTrigger(id, tenant, program, id, "manual", "Assess scope change", null);
        var triage = new TriageRiskReassessmentTrigger(Uuid.CreateVersion4(), tenant, program, id, 1, [], "No current risk is affected.");

        // Act
        var raised = owner.Raise(raise, actor, Now);
        var empty = owner.Triage(triage with { ReasonWhenNone = " " }, actor, Now.AddMinutes(1));
        var accepted = owner.Triage(triage, actor, Now.AddMinutes(1));
        var retry = owner.Triage(triage, actor with { Display = "Changed display" }, Now.AddDays(1));
        var changed = owner.Triage(triage with { ReasonWhenNone = "Another reason" }, actor, Now.AddDays(1));

        // Assert
        Assert.Null(raised);
        Assert.NotNull(empty);
        Assert.Null(accepted);
        Assert.Null(retry);
        Assert.NotNull(changed);
        Assert.Equal(2, owner.Revision);
        Assert.Empty(owner.Triaged!.AffectedRiskIds);
        Assert.Equal(Now.AddMinutes(1), owner.Triaged.TriagedAt);
    }

    [Fact]
    public void ShouldRefuseSecondTriageGivenDifferentCommandOrDuplicateRisks()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var owner = new RiskReassessmentTrigger(tenant, id);
        Assert.Null(owner.Raise(new(id, tenant, program, id, "manual", "Review incident", null), actor, Now));
        var risk = Uuid.CreateVersion4();
        var request = new TriageRiskReassessmentTrigger(Uuid.CreateVersion4(), tenant, program, id, 1, [risk, risk], null);

        // Act
        var duplicate = owner.Triage(request, actor, Now.AddMinutes(1));
        var accepted = owner.Triage(request with { AffectedRiskIds = [risk] }, actor, Now.AddMinutes(1));
        var second = owner.Triage(request with { CommandId = Uuid.CreateVersion4(), AffectedRiskIds = [risk] }, actor, Now.AddMinutes(2));

        // Assert
        Assert.NotNull(duplicate);
        Assert.Null(accepted);
        Assert.NotNull(second);
        Assert.Single(owner.Triaged!.AffectedRiskIds);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldReplayTerminalTriageGivenCommittedHistory()
    {
        // Arrange
        var owner = new RiskReassessmentTrigger(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var tenant = owner.TenantId;
        var program = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        Assert.Null(owner.Raise(new(owner.Id, tenant, program, owner.Id, "manual", "Annual change", null), actor, Now));
        Assert.Null(owner.Triage(new(Uuid.CreateVersion4(), tenant, program, owner.Id, 1, [Uuid.CreateVersion4()], null), actor, Now.AddMinutes(1)));
        var facts = new AggregateScenario<RiskReassessmentTrigger>(owner).PendingEvents.ToArray();
        var replay = new RiskReassessmentTrigger(tenant, owner.Id);

        // Act
        replay.Load(facts);

        // Assert
        Assert.Equal(owner.Raised, replay.Raised);
        Assert.Equal(owner.Triaged, replay.Triaged);
        Assert.Equal(2, replay.Revision);
    }
}
