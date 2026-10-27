using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>
///     The applicability line of one (program, edition, criterion): at most one pending proposal, only an
///     independent accepted review excludes the criterion, and a withdrawal names the accepted decision.
/// </summary>
public sealed class CriterionApplicabilityTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = NativeFacts.Now;

    static CriterionApplicabilityRules Line(string criterion = "CC6.2") =>
        new(TenantId, ProgramId, EditionId, criterion);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId, "Member");

    static Result Propose(CriterionApplicabilityRules line, Uuid decisionId, long expected,
        string rationale = "No accounts are approved.") => line.Propose(
        MappingRuleFacts.Root(line, decisionId, "ProposeCriterionNotApplicable", expected, ProposerId), rationale, ProposerId,
        Actor(ProposerId), Now);

    static Result Review(CriterionApplicabilityRules line, Uuid decisionId, long expected, string outcome,
        Uuid? reviewer = null, SeparationOfDutiesWaiver? waiver = null) => line.Review(
        MappingRuleFacts.Root(line, Uuid.CreateVersion4(), "ReviewCriterionApplicability", expected, reviewer ?? ReviewerId),
        decisionId, outcome, "Supported.", reviewer ?? ReviewerId, Actor(reviewer ?? ReviewerId), Now.AddMinutes(1),
        waiver);

    static Result Withdraw(CriterionApplicabilityRules line, Uuid decisionId, long expected) => line.Withdraw(
        MappingRuleFacts.Root(line, Uuid.CreateVersion4(), "WithdrawCriterionNotApplicable", expected, ProposerId),
        decisionId, "Now applies.", Actor(ProposerId), Now.AddMinutes(2));

    [Fact]
    public void ShouldExcludeCriterionGivenAcceptedReviewThenRestoreGivenWithdrawal()
    {
        // Arrange
        var line = Line();
        var decisionId = Uuid.CreateVersion4();
        Assert.True(Propose(line, decisionId, 0).IsSuccess);

        // Act
        var review = Review(line, decisionId, 1, "accept");
        var accepted = line.View!;
        var withdrawal = Withdraw(line, decisionId, 2);

        // Assert
        Assert.True(review.IsSuccess);
        Assert.Equal("not_applicable", accepted.Status);
        Assert.Equal(1, accepted.ActiveVersionNumber);
        Assert.True(withdrawal.IsSuccess);
        Assert.Equal("withdrawn", line.View!.Status);
        Assert.Null(line.View.ActiveVersionNumber);
        Assert.Equal(Actor(ProposerId), line.View.Versions[0].WithdrawnBy);
        Assert.Equal(decisionId, line.View.Versions[0].DecisionId);
    }

    [Fact]
    public void ShouldRejectSelfReviewGivenProposerWithoutExactWaiver()
    {
        // Arrange
        var line = Line();
        var decisionId = Uuid.CreateVersion4();
        Assert.True(Propose(line, decisionId, 0).IsSuccess);
        var waiver = Waiver(new SeparationOfDutiesWaiverScope("criterion_applicability", decisionId, decisionId, 1,
            "review"));
        var wrong = Waiver(new SeparationOfDutiesWaiverScope("criterion_applicability", decisionId, decisionId, 9,
            "review"));

        // Act
        var self = Review(line, decisionId, 1, "accept", ProposerId);
        var wrongWaiver = Review(line, decisionId, 1, "accept", ProposerId, wrong);
        var waived = Review(line, decisionId, 1, "accept", ProposerId, waiver);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, self.Error!.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, wrongWaiver.Error!.Kind);
        Assert.True(waived.IsSuccess);
        Assert.Equal(waiver.Id, line.View!.Versions[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectProposalGivenPendingStaleOrAlreadyNotApplicable()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);

        // Act
        var pending = Propose(line, Uuid.CreateVersion4(), 1, "Other.");
        Assert.True(Review(line, first, 1, "accept").IsSuccess);
        var stale = Propose(line, Uuid.CreateVersion4(), 1, "Stale.");
        var duplicate = Propose(line, Uuid.CreateVersion4(), 2, "Again.");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, pending.Error!.Kind);
        Assert.Contains("pending", pending.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Contains("already not applicable", duplicate.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, line.Revision);
    }

    [Fact]
    public void ShouldRefuseOldReviewAndWithdrawalGivenAnotherDecisionIsPendingOrAccepted()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "reject").IsSuccess);
        Assert.True(Propose(line, second, 2).IsSuccess);

        // Act
        var oldReview = Review(line, first, 3, "accept");
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        var oldWithdrawal = Withdraw(line, first, 4);
        var withdrawal = Withdraw(line, second, 4);
        var afterWithdrawal = Review(line, second, 5, "accept");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, oldReview.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, oldWithdrawal.Error!.Kind);
        Assert.True(withdrawal.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, afterWithdrawal.Error!.Kind);
        Assert.Equal(["rejected", "withdrawn"], line.View!.Versions.Select(static version => version.Status));
    }

    [Fact]
    public void ShouldKeepCriteriaIndependentGivenDifferentIdentifiers()
    {
        // Arrange
        var accounts = Line("CC6.2");
        var access = Line("CC6.1");

        // Act
        var results = new[] { Propose(accounts, Uuid.CreateVersion4(), 0), Propose(access, Uuid.CreateVersion4(), 0) };

        // Assert
        Assert.All(results, static result => Assert.True(result.IsSuccess));
        Assert.NotEqual(accounts.Id, access.Id);
        Assert.Equal(CriterionApplicability.IdentityOf(TenantId, ProgramId, EditionId, "CC6.2"), accounts.Id);
    }

    [Fact]
    public void ShouldReplayAndProjectTheSameViewGivenReducerOverRaisedFacts()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "reject").IsSuccess);
        Assert.True(Propose(line, second, 2, "Second attempt.").IsSuccess);
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        var facts = MappingRuleFacts.Raised(line);

        // Act
        CriterionApplicabilityView? projected = null;
        foreach (var fact in facts)
            projected = CriterionApplicabilityReducer.Apply(projected, fact);
        var replayed = MappingRuleFacts.Replay(Line(), facts);

        // Assert
        Assert.Equal(line.View! with { Versions = [] }, projected! with { Versions = [] });
        Assert.Equal(line.View.Versions, projected.Versions);
        Assert.Equal(line.View with { Versions = [] }, replayed.View! with { Versions = [] });
        Assert.Equal(line.View.Versions, replayed.View.Versions);
        Assert.Equal(["rejected", "accepted"], projected.Versions.Select(static version => version.Status));
        Assert.Equal([first, second], projected.Versions.Select(static version => version.DecisionId));
    }

    static SeparationOfDutiesWaiver Waiver(SeparationOfDutiesWaiverScope scope)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, ProposerId, Uuid.CreateVersion4(), "Requester", "Sole qualified reviewer.", Now,
            Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
