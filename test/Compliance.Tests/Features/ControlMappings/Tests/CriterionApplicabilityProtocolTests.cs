using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
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
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>
///     The per-criterion applicability protocol over the real handlers: proposals, independent reviews and withdrawals
///     are identified operations on one criterion's line; different criteria never conflict and an old decision can
///     no longer be reviewed or withdrawn once another decision is on the line.
/// </summary>
public sealed class CriterionApplicabilityProtocolTests
{
    static readonly Uuid Edition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-current");

    static ProposeCriterionNotApplicable Propose(ControlCriterionMappingHandlerTests.Fixture fixture,
        string criterion = "CC6.2", long expected = 0, string rationale = "No user accounts are approved.") => new(
        Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, Edition, criterion, expected, rationale);

    static async Task<CriterionApplicabilityRegistration> ProposeAsync(
        ControlCriterionMappingHandlerTests.Fixture fixture, ProposeCriterionNotApplicable request) =>
        (await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId, request)).Value;

    static async Task<long> FactsAsync(ControlCriterionMappingHandlerTests.Fixture fixture, Uuid decisionId)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<ApplicabilityReader>();
        var criterion = await reader.LocateAsync(fixture.TenantId, fixture.ProgramId, decisionId);
        return (await reader.ReadLineAsync(fixture.TenantId, fixture.ProgramId, criterion!.EditionId,
            criterion.CriterionIdentifier)).Revision;
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationAndConflictOnChangedRationaleGivenSameDecisionId()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var request = Propose(fixture);
        var first = await ProposeAsync(fixture, request);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept"));

        // Act
        var retried = await ProposeAsync(fixture, request);

        // Assert
        Assert.Equal(first, retried);
        Assert.Equal(new CriterionApplicabilityRegistration(request.DecisionId, 1, 1), first);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            request with { Rationale = "A different rationale." }, RequestErrorKind.Conflict);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            request with { ExpectedRevision = 1 }, RequestErrorKind.Conflict);
        Assert.Equal(2, await FactsAsync(fixture, request.DecisionId));
    }

    [Fact]
    public async Task ShouldRejectSecondActiveProposalGivenPendingOrAcceptedDecision()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await ProposeAsync(fixture, Propose(fixture));

        // Act
        // Assert
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            Propose(fixture, expected: 1, rationale: "Competing."), RequestErrorKind.Conflict);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept"));
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            Propose(fixture, expected: 2, rationale: "Again."), RequestErrorKind.Conflict);
        Assert.Equal(2, await FactsAsync(fixture, first.DecisionId));
    }

    [Fact]
    public async Task ShouldKeepCriteriaIndependentGivenTwoDecisionsAtTheSameExpectedRevision()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();

        // Act
        var accounts = await ProposeAsync(fixture, Propose(fixture, "CC6.2"));
        var access = await ProposeAsync(fixture, Propose(fixture, "CC6.1"));
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.ReviewApplicability(accounts.DecisionId, 1, "reject"));

        // Assert
        Assert.Equal(1, accounts.Revision);
        Assert.Equal(1, access.Revision);
        Assert.Equal(2, await FactsAsync(fixture, accounts.DecisionId));
        Assert.Equal(1, await FactsAsync(fixture, access.DecisionId));
    }

    [Fact]
    public async Task ShouldRefuseOldReviewAndWithdrawalGivenAnotherDecisionOnTheLine()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await ProposeAsync(fixture, Propose(fixture));
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept"));
        var withdrawal = new WithdrawCriterionNotApplicable(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            first.DecisionId, 2, "The service now approves accounts.");
        await fixture.Scenario(fixture.AuthorUserId).When(withdrawal).ExpectSuccess();
        var second = await ProposeAsync(fixture, Propose(fixture, expected: 3, rationale: "Not applicable again."));

        // Act
        // Assert
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.ReviewApplicability(first.DecisionId, 4, "accept"), RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId).When(new WithdrawCriterionNotApplicable(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, first.DecisionId, 4, "Old withdrawal."))
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId).When(withdrawal).ExpectSuccess();
        await fixture.Scenario(fixture.AuthorUserId).When(withdrawal with { Rationale = "Changed." })
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(4, await FactsAsync(fixture, second.DecisionId));
    }

    [Fact]
    public async Task ShouldRequireExactWaiverGivenProposerReviewingTheirOwnDecision()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await ProposeAsync(fixture, Propose(fixture));
        var author = RbacIds.Member(fixture.TenantId, fixture.AuthorUserId);
        var exact = await WaiverAsync(fixture, first.DecisionId, 1, author);
        var wrong = await WaiverAsync(fixture, first.DecisionId, 5, author);

        // Act
        // Assert
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept"), RequestErrorKind.Forbidden);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept") with { SeparationOfDutiesWaiverId = wrong },
            RequestErrorKind.Forbidden);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.ReviewApplicability(first.DecisionId, 1, "accept") with { SeparationOfDutiesWaiverId = exact });
        Assert.Equal(2, await FactsAsync(fixture, first.DecisionId));
    }

    static async Task<Uuid> WaiverAsync(ControlCriterionMappingHandlerTests.Fixture fixture, Uuid decisionId,
        long revision, Uuid beneficiary)
    {
        var waiverId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId,
            waiverId), waiver =>
        {
            Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.CriterionApplicability, decisionId, decisionId, revision,
                SeparationOfDutiesActions.Review), beneficiary, Uuid.CreateVersion4(), "Requester",
                "No other qualified reviewer.", now, now.AddDays(30)));
            return waiver.Approve(Uuid.CreateVersion4(), "Approver", now) is null
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The waiver was not approved."));
        });
        return waiverId;
    }
}
