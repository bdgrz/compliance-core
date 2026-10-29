using Bdgrz.Compliance.Tests.Testing;
using System.Security.Claims;
using Cntryl.Fitz.Extensions;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.History;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class FitzRiskDraftHistoryDirectoryV1Tests
{
    [Fact]
    public async Task ShouldPreserveImmutableRowsAndPagePerDraftGivenReplayedRiskEvents()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var riskId = RiskDraft.IdFor(tenantId, programId, "R-01");
        var otherRiskId = RiskDraft.IdFor(tenantId, programId, "R-02");
        var authorId = Uuid.CreateVersion4();
        var editorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        await using var native = new RiskDraftSourceFixture();
        var firstRules = new RiskDraftRules(tenantId, riskId);
        Assert.True(firstRules.Create(programId, riskId, "R-01", Content("Initial"), authorId, "Author", now).IsSuccess);
        Assert.Null(firstRules.Revise(programId, 1, Content("Second"), editorId, "Editor", now.AddMinutes(1)));
        Assert.Null(firstRules.Revise(programId, 2, Content("Third"), editorId, "Editor", now.AddMinutes(2)));
        var firstFacts = await native.PublishAsync(firstRules);
        var otherRules = new RiskDraftRules(tenantId, otherRiskId);
        Assert.True(otherRules.Create(programId, otherRiskId, "R-02", Content("Other"), authorId, "Author", now).IsSuccess);
        var otherFacts = await native.PublishAsync(otherRules);
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, tenantId, firstFacts.Concat(otherFacts).ToArray());

        // Act
        var first = await directory.ListRevisionsAsync(tenantId, riskId, 2, null);
        var secondPage = await directory.ListRevisionsAsync(tenantId, riskId, 2,
            first.NextCursor);
        var otherPage = await directory.ListRevisionsAsync(tenantId, otherRiskId, 2, null);

        // Assert
        Assert.Equal([1L, 2L], first.Items.Select(item => item.Revision));
        Assert.Equal([3L], secondPage.Items.Select(item => item.Revision));
        Assert.Equal("Initial", first.Items[0].Content.Title);
        Assert.Equal(authorId, first.Items[0].ChangedByMemberId);
        Assert.Equal("Second", first.Items[1].Content.Title);
        Assert.Equal("Third", secondPage.Items[0].Content.Title);
        Assert.Equal("R-01", first.Items[1].Identifier);
        Assert.Equal("R-01", secondPage.Items[0].Identifier);
        Assert.Equal(["Other"], otherPage.Items.Select(item => item.Content.Title));
    }

    [Fact]
    public async Task ShouldRejectMalformedAndCrossRecordCursorsGivenRiskDraftHistoryList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        await using var native = new RiskDraftSourceFixture();
        var firstRules = CreateSource(tenantId, programId, "R-01");
        var secondRules = CreateSource(tenantId, programId, "R-02");
        Assert.Null(firstRules.Revise(programId, 1, Content("Second"), Uuid.CreateVersion4(), "Editor", DateTimeOffset.UtcNow));
        var facts = (await native.PublishAsync(firstRules)).Concat(await native.PublishAsync(secondRules)).ToArray();
        var firstSource = await native.Reader.HydrateAsync(new RiskDraft(tenantId, firstRules.Id));
        var secondSource = await native.Reader.HydrateAsync(new RiskDraft(tenantId, secondRules.Id));
        var directory = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), native.Content, native.Drafts);
        await ProjectAsync(directory, tenantId, facts);
        var consistency = new RiskDraftHistoryReadConsistency(directory, new SourceReader(firstSource, secondSource));
        var handler = new ListRiskDraftRevisionsHandler(directory, consistency);
        var firstPage = await directory.ListRevisionsAsync(tenantId, firstSource.Id, 1, null);

        // Act
        var malformed = await handler.HandleAsync(Context(new ListRiskDraftRevisions(
            tenantId, programId, firstSource.Id, Cursor: "not-a-cursor")), CancellationToken.None);
        var crossRecord = await handler.HandleAsync(Context(new ListRiskDraftRevisions(
            tenantId, programId, secondSource.Id, Cursor: firstPage.NextCursor)),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(malformed.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(crossRecord.Error).Kind);
    }

    [Fact]
    public async Task ShouldReportLagAndHideOtherProgramOrTenantGivenRiskDraftHistoryList()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        await using var native = new RiskDraftSourceFixture();
        var rules = CreateSource(tenantId, programId, "R-01");
        Assert.Null(rules.Revise(programId, 1, Content("Revised"), Uuid.CreateVersion4(), "Editor", DateTimeOffset.UtcNow));
        var facts = await native.PublishAsync(rules);
        var risk = await native.Reader.HydrateAsync(new RiskDraft(tenantId, rules.Id));
        var projected = new FitzRiskDraftHistoryDirectoryV1(new InMemoryKvClient(), native.Content, native.Drafts);
        var directory = new TrackingDirectory(projected);
        var consistency = new RiskDraftHistoryReadConsistency(directory,
            new SourceReader(risk));
        var handler = new ListRiskDraftRevisionsHandler(directory, consistency);

        // Act
        var lagged = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            programId, risk.Id, MinimumRiskRevision: 2)), CancellationToken.None);
        await ProjectAsync(projected, tenantId, facts.ToArray());
        var recovered = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            programId, risk.Id, MinimumRiskRevision: 2)), CancellationToken.None);
        var readsAfterRecovery = directory.RevisionReads;
        var otherProgram = await handler.HandleAsync(Context(new ListRiskDraftRevisions(tenantId,
            Uuid.CreateVersion4(), risk.Id)), CancellationToken.None);
        var otherTenant = await handler.HandleAsync(Context(new ListRiskDraftRevisions(otherTenantId,
            programId, risk.Id)), CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal([1L, 2L], recovered.Value.Items.Select(item => item.Revision));
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(otherProgram.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(otherTenant.Error).Kind);
        Assert.Equal(1, directory.ListCalls);
        Assert.Equal(readsAfterRecovery, directory.RevisionReads);
    }

    static RequestContext<T> Context<T>(T request) where T : IRequestBase =>
        new(request, new ClaimsPrincipal());

    static RiskDraftContent Content(string title) => new(title, $"{title} scenario",
        $"{title} effect", "Management observation");

    static RiskDraftRules CreateSource(Uuid tenantId, Uuid programId, string identifier)
    {
        var source = new RiskDraftRules(tenantId, RiskDraft.IdFor(tenantId, programId, identifier));
        Assert.True(source.Create(programId, Uuid.CreateVersion4(), identifier, Content(identifier),
            Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow).IsSuccess);
        return source;
    }

    static CheckpointIdentity Identity(Uuid tenantId) => new(FitzRiskDraftHistoryDirectoryV1.ProjectorName,
        EventStreamPattern.ForPattern(tenantId.ToString(), "risks"));

    static async Task ProjectAsync(FitzRiskDraftHistoryDirectoryV1 directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            Identity(tenantId), ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(params RiskDraft[] sources) : IAggregateReader
    {
        readonly Dictionary<(string Realm, Uuid RiskId), RiskDraft> _sources = sources
            .ToDictionary(source => (source.Stream.Realm, source.Id));

        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is RiskDraft requested && _sources.TryGetValue(
                    (requested.Stream.Realm, requested.Id), out var source))
                return ValueTask.FromResult((TAggregate)(Aggregate)source);
            return ValueTask.FromResult(aggregate);
        }
    }

    sealed class TrackingDirectory(IRiskDraftHistoryDirectoryReader inner)
        : IRiskDraftHistoryDirectoryReader
    {
        public int ListCalls { get; private set; }
        public int RevisionReads { get; private set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default)
        {
            RevisionReads++;
            return inner.GetRevisionAsync(tenantId, riskId, revision, ct);
        }

        public ValueTask<Page<RiskDraftRevisionView>> ListRevisionsAsync(Uuid tenantId,
            Uuid riskId, int limit, string? cursor, CancellationToken ct = default)
        {
            ListCalls++;
            return inner.ListRevisionsAsync(tenantId, riskId, limit, cursor, ct);
        }
    }
}
