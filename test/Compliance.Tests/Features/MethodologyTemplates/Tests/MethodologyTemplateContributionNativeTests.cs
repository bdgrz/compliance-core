using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
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
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class MethodologyTemplateContributionNativeTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseContributorAndReviewerAcceptanceGivenSameUserInAnotherRole(bool reviewerAccepts)
    {
        // Arrange
        await using var scenario = new MethodologyTemplateContributionScenario(Now);
        var tenant = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var proposal = Proposal(tenant);
        Assert.True((await scenario.ProposeAsync(proposal, author)).IsSuccess);
        var review = new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant, proposal.ContributionId,
            "approved", "No identifying material remains");
        Assert.True((await scenario.ReviewAsync(review, reviewer)).IsSuccess);

        // Act
        var accepted = await scenario.AcceptAsync(new AcceptTemplateContribution(Uuid.CreateVersion4(), tenant,
            proposal.ContributionId, "Accept"), reviewerAccepts ? reviewer : author);
        var owner = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant, proposal.ContributionId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, accepted.Error?.Kind);
        Assert.Equal(2, owner.Revision);
        Assert.Null(await scenario.Views.ShareAsync(tenant, proposal.ContributionId));
    }

    [Fact]
    public async Task ShouldReplayOriginalResultsWithoutCurrentContentGivenLaterAcceptedContribution()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateContributionScenario(Now);
        var tenant = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var acceptor = Uuid.CreateVersion4();
        var proposal = Proposal(tenant);
        var review = new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant, proposal.ContributionId, "approved", "Reviewed");
        var accept = new AcceptTemplateContribution(Uuid.CreateVersion4(), tenant, proposal.ContributionId, "Accepted");
        Assert.True((await scenario.ProposeAsync(proposal, author)).IsSuccess);
        Assert.True((await scenario.ReviewAsync(review, reviewer)).IsSuccess);
        Assert.True((await scenario.AcceptAsync(accept, acceptor)).IsSuccess);
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant, proposal.ContributionId));
        var missing = new MissingCurrentContent(scenario.Content);
        await Assert.ThrowsAsync<ImmutableContentUnavailableException>(() =>
            new MethodologyTemplateContributionReader(scenario.Reader, missing).ReadAsync(tenant,
                proposal.ContributionId).AsTask());
        scenario.Clock.Now = Now.AddHours(1);
        var operations = scenario.Operations(missing);

        // Act
        var proposalReplay = await new ProposeDeidentifiedTemplateContributionHandler(operations).HandleAsync(
            MethodologyTemplateContributionScenario.Context(proposal, author), CancellationToken.None);
        var reviewReplay = await new ReviewTemplateContributionHandler(operations).HandleAsync(
            MethodologyTemplateContributionScenario.Context(review, reviewer), CancellationToken.None);
        var acceptReplay = await new AcceptTemplateContributionHandler(operations).HandleAsync(
            MethodologyTemplateContributionScenario.Context(accept, acceptor), CancellationToken.None);
        var changed = await new ReviewTemplateContributionHandler(operations).HandleAsync(
            MethodologyTemplateContributionScenario.Context(review with { ReviewNotes = "Changed" }, reviewer), CancellationToken.None);
        var after = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant, proposal.ContributionId));

        // Assert
        Assert.True(proposalReplay.IsSuccess, proposalReplay.Error?.Message);
        Assert.True(reviewReplay.IsSuccess, reviewReplay.Error?.Message);
        Assert.True(acceptReplay.IsSuccess, acceptReplay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(1, missing.CurrentReads);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldKeepOnlyCompactNativeContentReferencesGivenAcceptedContributionReplay()
    {
        // Arrange
        await using var scenario = new MethodologyTemplateContributionScenario(Now);
        var tenant = Uuid.CreateVersion4();
        var proposal = Proposal(tenant);
        Assert.True((await scenario.ProposeAsync(proposal, Uuid.CreateVersion4())).IsSuccess);
        Assert.True((await scenario.ReviewAsync(new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant,
            proposal.ContributionId, "approved", "Reviewed"), Uuid.CreateVersion4())).IsSuccess);
        Assert.True((await scenario.AcceptAsync(new AcceptTemplateContribution(Uuid.CreateVersion4(), tenant,
            proposal.ContributionId, "Accepted"), Uuid.CreateVersion4())).IsSuccess);
        var facts = await scenario.FactsAsync(tenant);

        // Act
        var replay = new AggregateScenario<MethodologyTemplateContribution>(
            new MethodologyTemplateContribution(tenant, proposal.ContributionId)).Given(facts.ToArray()).Aggregate;
        var current = await scenario.Views.ReadAsync(tenant, proposal.ContributionId);
        var share = await scenario.Views.ShareAsync(tenant, proposal.ContributionId);

        // Assert
        Assert.Equal(3, facts.Count);
        Assert.Equal("accepted", replay.Status);
        Assert.NotNull(replay.Content);
        foreach (var fact in facts)
        {
            Assert.IsAssignableFrom<INativeOperationFact>(fact);
            var result = fact switch
            {
                MethodologyTemplateContributionProposed proposed => proposed.OperationResult,
                MethodologyTemplateContributionReviewed reviewed => reviewed.OperationResult,
                MethodologyTemplateContributionAccepted accepted => accepted.OperationResult,
                _ => throw new InvalidOperationException(),
            };
            Assert.NotNull(result);
            Assert.Equal("mutation-result-content.v1", result.Schema);
            var original = await scenario.Content.ReadJsonAsync<MutationResultContent>(tenant.ToString(), result);
            Assert.Equal(UnitResult.Schema, original.Schema);
            var body = Assert.IsType<StandardResponseBody>(original.Value);
            _ = await scenario.Content.ReadJsonAsync<UnitResult>(tenant.ToString(), body.Content);
            var json = JsonSerializer.SerializeToElement(fact, fact.GetType());
            Assert.False(json.TryGetProperty("State", out _));
            Assert.DoesNotContain(proposal.SourceRecordId.ToString(), json.GetRawText());
            Assert.DoesNotContain(proposal.ProposedContent.Summary, json.GetRawText());
        }
        Assert.Equal(proposal.SourceRecordId, current!.SourceRecordId);
        Assert.DoesNotContain(tenant.ToString(), JsonSerializer.Serialize(share));
        Assert.DoesNotContain(proposal.SourceRecordId.ToString(), JsonSerializer.Serialize(share));
        Assert.Equal("actor@example.test", current.ProposedByDisplay);
    }

    [Theory]
    [InlineData("current", true)]
    [InlineData("current", false)]
    [InlineData("response", true)]
    [InlineData("response", false)]
    [InlineData("result", true)]
    [InlineData("result", false)]
    public async Task ShouldRetainAcceptedContributionAndRecoverGivenUnavailableOrCorruptContent(string stage,
        bool unavailable)
    {
        // Arrange
        await using var scenario = new MethodologyTemplateContributionScenario(Now);
        var tenant = Uuid.CreateVersion4();
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var proposal = Proposal(tenant);
        Assert.True((await scenario.ProposeAsync(proposal, author)).IsSuccess);
        var review = new ReviewTemplateContribution(Uuid.CreateVersion4(), tenant, proposal.ContributionId,
            "approved", "Reviewed");
        var before = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant, proposal.ContributionId));
        var schema = stage switch
        {
            "current" => MethodologyTemplateContributionReader.Schema,
            "response" => FirstResponses.Schema,
            _ => UnitResult.Schema,
        };
        var operations = scenario.Operations(new UnreadableSchema(scenario.Content, schema, unavailable));

        // Act
        var result = stage == "current"
            ? await new ReviewTemplateContributionHandler(operations).HandleAsync(
                MethodologyTemplateContributionScenario.Context(review, reviewer), CancellationToken.None)
            : await new ProposeDeidentifiedTemplateContributionHandler(operations).HandleAsync(
                MethodologyTemplateContributionScenario.Context(proposal, author), CancellationToken.None);
        var afterFailure = await scenario.Reader.HydrateAsync(new MethodologyTemplateContribution(tenant,
            proposal.ContributionId));
        var recovered = stage == "current"
            ? await scenario.ReviewAsync(review, reviewer)
            : await scenario.ProposeAsync(proposal, author);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, afterFailure.CommittedStreamPosition);
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
    }

    static ProposeDeidentifiedTemplateContribution Proposal(Uuid tenant) => new(tenant, Uuid.CreateVersion4(),
        Uuid.CreateVersion4(), 1, new MethodologyTemplateRecordContent("review-access", "control", "Review access",
            "Review privileged access quarterly", []), "Removed client identity and evidence");

    sealed class UnreadableSchema(IImmutableContentStore inner, string schema, bool unavailable)
        : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string contentSchema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, contentSchema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.Schema != schema
            ? inner.ReadVerifiedAsync(realm, reference, ct) : throw (unavailable
                ? new ImmutableContentUnavailableException("The immutable contribution content is temporarily absent.")
                : new ImmutableContentIntegrityException("The immutable contribution content digest differs."));
    }

    sealed class MissingCurrentContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int CurrentReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != MethodologyTemplateContributionReader.Schema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            CurrentReads++;
            return Task.FromException<ReadOnlyMemory<byte>>(new ImmutableContentUnavailableException("Missing current view"));
        }
    }
}
