using System.Text.Json;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.TypeIIPlan;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class TypeIIPlanTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid PlanId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly Uuid ControlOwnerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequireEveryControlOwnerCadenceAndAcknowledgedOpeningGapGivenProposedPlan()
    {
        // Arrange
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);

        // Act
        var missingOwner = plan.Propose(Content() with
        {
            Controls = [Control() with { OwnerMemberId = null }],
        }, ProposerId, "Compliance lead", Now);
        var missingGapAcknowledgement = plan.Propose(Content() with
        {
            AcknowledgedGaps = [],
        }, ProposerId, "Compliance lead", Now);
        var proposed = plan.Propose(Content(), ProposerId, "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingOwner).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(missingGapAcknowledgement).Code);
        Assert.Null(proposed);
        Assert.Equal("proposed", plan.Current!.Status);
        Assert.Single(plan.Current.Current!.Content.Controls);
    }

    [Fact]
    public void ShouldRejectDueWindowOutsideSupportedRangeGivenProposedPlan()
    {
        // Arrange
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);

        // Act
        var failure = plan.Propose(Content() with
        {
            Controls = [Control() with { DueWithinDays =
                Bdgrz.Compliance.Features.Operations.Models.ControlCadence.MaximumDueWithinDays + 1 }],
        }, ProposerId, "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, plan.Revision);
    }

    [Fact]
    public void ShouldRequireExpectedEvidenceOnEveryControlGivenProposedPlan()
    {
        // Arrange
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
        var content = Content() with
        {
            Controls = [Control() with { ExpectedEvidenceDescriptions = null }],
        };

        // Act
        var failure = plan.Propose(content, ProposerId, "Compliance lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, plan.Revision);
    }

    [Fact]
    public void ShouldOmitUnsetOptionalFieldsFromPlanContentSerializationGivenLegacyPlan()
    {
        // Arrange
        var content = Content();

        // Act
        var controlJson = JsonSerializer.Serialize(content.Controls[0] with
        {
            ExpectedEvidenceDescriptions = null,
        });
        var gapJson = JsonSerializer.Serialize(content.CarriedForwardGaps[0]);

        // Assert
        Assert.DoesNotContain("\"DueWithinDays\"", controlJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ExpectedEvidenceDescriptions\"", controlJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OwnerMemberId\"", gapJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRequireReviewBeforeApprovalAndRetainExactPlanVersionGivenPlan()
    {
        // Arrange
        var plan = NewPlan();

        // Act
        var prematureApproval = plan.Approve(plan.Revision, 1,
            "Approved controlled start", ApproverId, "CISO", "Executive", Now.AddDays(1));
        var reviewed = plan.Review(plan.Revision, 1, "approve", "All control owners and expected work are present",
            ReviewerId, "Reviewer", Now.AddDays(1));
        var approved = plan.Approve(plan.Revision, 1, "Approved controlled start",
            ApproverId, "CISO", "Executive", Now.AddDays(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(prematureApproval).Code);
        Assert.Null(reviewed);
        Assert.Null(approved);
        Assert.Equal("approved", plan.Current!.Status);
        Assert.Equal(1, plan.Current.Current!.Version);
        Assert.Equal(ProposerId, plan.Current.Current.ProposedByMemberId);
        Assert.Equal(ApproverId, Assert.Single(plan.Current!.Approvals).ApprovedByMemberId);
    }

    [Fact]
    public void ShouldTreatRepeatedExecutiveDecisionAsIdempotentGivenLaterRetryTime()
    {
        // Arrange
        var plan = NewPlan();
        Assert.Null(plan.Review(plan.Revision, 1, "approve",
            "All owners and opening gaps are assigned", ReviewerId, "Reviewer", Now.AddDays(1)));
        Assert.Null(plan.Approve(plan.Revision, 1, "Approved controlled start",
            ApproverId, "CISO", "Executive", Now.AddDays(2)));
        var revision = plan.Revision;

        // Act
        var retry = plan.Approve(plan.Revision, 1, "Approved controlled start",
            ApproverId, "ciso", "Updated executive display", Now.AddDays(3));

        // Assert
        Assert.Null(retry);
        Assert.Equal(revision, plan.Revision);
        Assert.Single(plan.Current!.Approvals);
        Assert.Equal(Now.AddDays(2), plan.Current.Approvals[0].ApprovedAt);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(plan.Approve(plan.Revision, 1,
                "Changed the approval rationale", ApproverId, "CISO", "Executive",
                Now.AddDays(4))).Code);
        Assert.Equal(revision, plan.Revision);
    }

    [Fact]
    public void ShouldPreserveFirstProposalTimeGivenSameIntentRetriedLater()
    {
        // Arrange
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
        var content = Content();
        Assert.Null(plan.Propose(content, ProposerId, "Compliance lead", Now));
        var revision = plan.Revision;

        // Act
        var retry = plan.Propose(content, ProposerId, "Compliance lead", Now.AddDays(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(revision, plan.Revision);
        Assert.Equal(Now, plan.Current!.Current!.ProposedAt);
        Assert.Single(plan.Current.Versions);
    }

    static TypeIIPlanAggregate NewPlan()
    {
        var plan = new TypeIIPlanAggregate(TenantId, ProgramId, PlanId);
        Assert.Null(plan.Propose(Content(), ProposerId, "Compliance lead", Now));
        return plan;
    }

    static TypeIIPlanContent Content()
    {
        var gapId = Uuid.CreateVersion4();
        return new TypeIIPlanContent(ExaminationId, Uuid.CreateVersion4(),
            new string('a', 64), Uuid.CreateVersion4(), new DateOnly(2026, 10, 1),
            new DateOnly(2027, 9, 30), "Audit Firm", ["security", "availability"],
            Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            [Reference("policy"), Reference("risk"), Reference("vendor"),
                Reference("finding"), Reference("change")], [Control()],
            [new("risk", gapId, Uuid.CreateVersion4(), 1, new string('b', 64), "carry")],
            [new(gapId, "Known opening gap", "acknowledged", "Owner and due date recorded")],
            [new(Uuid.CreateVersion4(), "Enable the new access review control", new DateOnly(2026, 12, 1))]);
    }

    static TypeIIControlPlanEntry Control() => new(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
        "Quarterly access review", ControlOwnerId, "quarterly", 4, new DateOnly(2026, 10, 1), null,
        ExpectedEvidenceDescriptions: ["Dated review record"]);

    static ExaminationContentReference Reference(string sourceType) =>
        new(sourceType, Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('c', 64));

    [Fact]
    public void ShouldReplayProposedReviewedAndApprovedPlanHistoryGivenEvents()
    {
        // Arrange
        var plan = NewPlan();
        Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed all owners and gaps",
            ReviewerId, "Reviewer", Now.AddDays(1)));
        Assert.Null(plan.Approve(plan.Revision, 1, "Approved controlled start",
            ApproverId, "CISO", "Executive", Now.AddDays(2)));
        var events = new AggregateScenario<TypeIIPlanAggregate>(plan).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<TypeIIPlanAggregate>(
            new TypeIIPlanAggregate(TenantId, ProgramId, PlanId)).Given(events).Aggregate;

        // Assert
        Assert.Equal("approved", replay.Current!.Status);
        Assert.Equal(1, replay.Current.Current!.Version);
        Assert.Equal(ReviewerId, Assert.Single(replay.Current.Reviews).ReviewerMemberId);
        Assert.Equal(ApproverId, Assert.Single(replay.Current.Approvals).ApprovedByMemberId);
        Assert.Equal(plan.Current!.Current!.ContentSha256, replay.Current.Current.ContentSha256);
    }
}
