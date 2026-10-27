using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
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
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

/// <summary>
///     The per-tuple mapping protocol over the real handlers: proposals are identified operations that return their
///     original registration, different tuples never conflict, competing proposals on one tuple admit exactly one,
///     and review or retirement name the exact proposal.
/// </summary>
public sealed class ControlCriterionMappingProtocolTests
{
    static async Task<Result<ControlCriterionMappingRegistration>> DispatchAsync(
        ControlCriterionMappingHandlerTests.Fixture fixture, ProposeControlCriterionMapping request)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            PersonalMappingDecisionTransportTests.Context(fixture.AuthorUserId, "direct"), CancellationToken.None);
    }

    static async Task<long> FactsAsync(ControlCriterionMappingHandlerTests.Fixture fixture, Uuid mappingId) =>
        (await ReadLineAsync(fixture, mappingId)).Revision;

    static async Task<ControlCriterionMapping> ReadLineAsync(ControlCriterionMappingHandlerTests.Fixture fixture,
        Uuid mappingId)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var mappings = scope.ServiceProvider.GetRequiredService<ControlMappingReader>();
        var tuple = await mappings.LocateAsync(fixture.TenantId, fixture.ProgramId, mappingId);
        return await mappings.ReadLineAsync(fixture.TenantId, fixture.ProgramId, tuple!.ControlId, tuple.EditionId,
            tuple.CriterionIdentifier);
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationAndConflictOnChangedIntentGivenProposalIdReuse()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var request = fixture.Propose("CC6.1");
        var first = await fixture.ProposeAsync(request);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(first.MappingId, first.Revision, "accept"));

        // Act
        var retried = await fixture.ProposeAsync(request);

        // Assert
        Assert.Equal(first, retried);
        Assert.Equal(new ControlCriterionMappingRegistration(request.MappingId, 1, 1), first);
        await fixture.Scenario(fixture.AuthorUserId).When(request with { Rationale = "A different rationale." })
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId).When(request with { ApplicabilityExplanation = "Different." })
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId).When(request with { ExpectedRevision = 1 })
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.ReviewerUserId).When(request).ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(2, await FactsAsync(fixture, request.MappingId));
    }

    [Fact]
    public async Task ShouldKeepTuplesIndependentGivenDifferentCriteriaAtTheSameExpectedRevision()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();

        // Act
        var access = await fixture.ProposeAsync("CC6.1");
        var accounts = await fixture.ProposeAsync("CC6.2");
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(access.MappingId, access.Revision, "accept"));
        var accountsReview = await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider,
            fixture.ReviewerUserId, fixture.Review(accounts.MappingId, accounts.Revision, "reject"));

        // Assert
        Assert.True(accountsReview.IsSuccess);
        Assert.Equal(1, access.Revision);
        Assert.Equal(1, accounts.Revision);
        Assert.Equal("active", (await fixture.GetAsync(access.MappingId)).Status);
        Assert.Equal("rejected", (await fixture.GetAsync(accounts.MappingId)).Status);
        Assert.Equal(2, await FactsAsync(fixture, access.MappingId));
        Assert.Equal(2, await FactsAsync(fixture, accounts.MappingId));
    }

    [Fact]
    public async Task ShouldAdmitExactlyOneProposalGivenTwoCompetingProposalsOnOneTuple()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var left = fixture.Propose("CC6.1", rationale: "Left proposal.");
        var right = fixture.Propose("CC6.1", rationale: "Right proposal.");

        // Act
        var results = await Task.WhenAll(
            DispatchAsync(fixture, left), DispatchAsync(fixture, right));

        // Assert
        Assert.Single(results, static result => result.IsSuccess);
        Assert.Single(results, static result => result.Error?.Kind == RequestErrorKind.Conflict);
        var winner = results[0].IsSuccess ? left : right;
        var loser = results[0].IsSuccess ? right : left;
        Assert.Equal(1, (await ReadLineAsync(fixture, winner.MappingId)).Revision);
        await using var scope = fixture.Provider.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<ControlMappingReader>()
            .LocateAsync(fixture.TenantId, fixture.ProgramId, loser.MappingId));
    }

    [Fact]
    public async Task ShouldRefuseReviewAndRetirementGivenUnknownProposalOrStaleRevision()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");

        // Act
        // Assert
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(Uuid.CreateVersion4(), 1, "accept"), RequestErrorKind.NotFound);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(first.MappingId, 0, "accept"), RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.ReviewerUserId).When(new RetireControlCriterionMapping(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), 1, "Gone."))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.ReviewerUserId).When(new RetireControlCriterionMapping(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, first.MappingId, 1, "Not reviewed yet."))
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(1, await FactsAsync(fixture, first.MappingId));
    }

    [Fact]
    public async Task ShouldRefuseOldRetirementAndReviewGivenNewerAcceptedSuccessor()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(first.MappingId, 1, "accept"));
        var second = await fixture.ProposeAsync("CC6.1", 2, "Narrowed rationale");
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(second.MappingId, second.Revision, "accept"));

        // Act
        // Assert
        await fixture.Scenario(fixture.ReviewerUserId).When(new RetireControlCriterionMapping(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, first.MappingId, 4, "Old retirement."))
            .ExpectFailure(RequestErrorKind.Conflict);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            fixture.Review(first.MappingId, 4, "accept"), RequestErrorKind.Conflict);
        var line = await fixture.GetAsync(second.MappingId);
        Assert.Equal("active", line.Status);
        Assert.Equal(second.MappingId, line.Versions[line.ActiveVersionNumber!.Value - 1].MappingId);
        Assert.Equal(4, line.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalReviewAndRetirementGivenExactRetriesAfterLaterChanges()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");
        var review = fixture.Review(first.MappingId, 1, "accept");
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId, review);
        var retire = new RetireControlCriterionMapping(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            first.MappingId, 2, "The control no longer addresses access provisioning.");
        await fixture.Scenario(fixture.ReviewerUserId).When(retire).ExpectSuccess();
        var second = await fixture.ProposeAsync("CC6.1", 3, "A fresh rationale.");

        // Act
        var reviewAgain = await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            review);
        var retireAgain = await fixture.Scenario(fixture.ReviewerUserId).When(retire).ExpectSuccess();

        // Assert
        Assert.True(reviewAgain.IsSuccess);
        Assert.True(retireAgain.IsSuccess);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            review with { Rationale = "Changed." }, RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.ReviewerUserId).When(retire with { Rationale = "Changed." })
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(4, (await ReadLineAsync(fixture, first.MappingId)).Revision);
        Assert.Equal(first.MappingId, (await fixture.GetAsync(second.MappingId)).MappingId);
    }

    [Fact]
    public async Task ShouldRequireExactWaiverGivenProposerReviewingTheirOwnMapping()
    {
        // Arrange
        var fixture = await ControlCriterionMappingHandlerTests.Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");
        var authorMember = RbacIds.Member(fixture.TenantId, fixture.AuthorUserId);
        var wrong = await ApprovedWaiverAsync(fixture, first.MappingId, 7, authorMember);
        var exact = await ApprovedWaiverAsync(fixture, first.MappingId, 1, authorMember);

        // Act
        // Assert
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.Review(first.MappingId, 1, "accept"), RequestErrorKind.Forbidden);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.Review(first.MappingId, 1, "accept") with { SeparationOfDutiesWaiverId = wrong },
            RequestErrorKind.Forbidden);
        await PersonalMappingDecisionTransportTests.HttpAsync(fixture.Provider, fixture.AuthorUserId,
            fixture.Review(first.MappingId, 1, "accept") with { SeparationOfDutiesWaiverId = exact });
        Assert.Equal(exact, (await fixture.GetAsync(first.MappingId)).Versions[0].SeparationOfDutiesWaiverId);
    }

    static async Task<Uuid> ApprovedWaiverAsync(ControlCriterionMappingHandlerTests.Fixture fixture, Uuid mappingId,
        long revision, Uuid beneficiary)
    {
        var waiverId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.TenantId,
            waiverId), waiver =>
        {
            Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.ControlCriterionMapping, mappingId, mappingId, revision,
                SeparationOfDutiesActions.Review), beneficiary, Uuid.CreateVersion4(), "Requester",
                "No other qualified reviewer.", now, now.AddDays(30)));
            return waiver.Approve(Uuid.CreateVersion4(), "Approver", now) is null
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The waiver was not approved."));
        });
        return waiverId;
    }
}
