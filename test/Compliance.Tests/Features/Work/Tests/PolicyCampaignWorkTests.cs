using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Rules;
using Bdgrz.Compliance.Features.Workforce.Rules;
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
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class PolicyCampaignWorkTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OwnerMemberId = Uuid.CreateVersion4();
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
    static readonly Uuid AdaMemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    static readonly ActorReference Owner = ActorReference.ForMember(OwnerMemberId, "Owner");

    [Fact]
    public async Task ShouldAssignPendingAcknowledgementToThePersonsMemberGivenALinkedMember()
    {
        // Arrange
        await using var harness = new Harness();
        var campaign = await harness.LaunchAsync(Ada, Bob);

        // Act
        var work = PolicyCampaignWork.Candidates(await harness.StateAsync(campaign), Today,
            new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var ada = Assert.Single(work, item => item.Summary.StartsWith("Acknowledge", StringComparison.Ordinal));
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, AdaMemberId), ada.Responsible);
        Assert.Equal(PolicyCampaignWork.Acknowledgement, ada.Kind);
        Assert.Equal("acknowledge", ada.NextAction);
        Assert.Equal(Today.AddDays(30), ada.DueOn);
        Assert.EndsWith($"/campaigns/{campaign.Id}/acknowledgements", ada.ActionPath, StringComparison.Ordinal);
        Assert.Contains("POL-AC", ada.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAssignNonMemberAcknowledgementToTheCampaignOwnerGivenNoLinkedMember()
    {
        // Arrange
        await using var harness = new Harness();
        var campaign = await harness.LaunchAsync(Ada, Bob);
        var state = await harness.StateAsync(campaign);

        // Act
        var work = PolicyCampaignWork.Candidates(state, Today, new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var bob = Assert.Single(work, item => item.Summary.StartsWith("Collect", StringComparison.Ordinal));
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, OwnerMemberId), bob.Responsible);
        Assert.Equal("record_acknowledgement", bob.NextAction);
        Assert.Contains(state.DisplayNameOf(Bob)!, bob.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldGiveEachPersonADistinctStableWorkItemGivenOneCampaign()
    {
        // Arrange
        await using var harness = new Harness();
        var state = await harness.StateAsync(await harness.LaunchAsync(Ada, Bob));

        // Act
        var first = PolicyCampaignWork.Candidates(state, Today, new Dictionary<Uuid, Uuid>());
        var again = PolicyCampaignWork.Candidates(state, Today, new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.Equal(2, first.Select(static item => item.WorkItemId).Distinct().Count());
        Assert.Equal(first.Select(static item => item.WorkItemId), again.Select(static item => item.WorkItemId));
    }

    [Fact]
    public async Task ShouldDropSatisfiedAndExceptedPeopleGivenAcknowledgementAndWaiver()
    {
        // Arrange
        await using var harness = new Harness();
        var campaign = await harness.LaunchAsync(Ada, Bob);
        var acknowledged = await CampaignSeed.AcknowledgeAsync(harness.Provider, TenantId, ProgramId, campaign.Id, Ada,
            2, "sha-v2", Owner, new ActorReference("workforce_person", Ada.ToString(), "Ada"), true, Now.AddDays(1));
        var waived = await CampaignSeed.ApproveWaiverAsync(harness.Provider, TenantId, ProgramId, campaign.Id, Bob,
            "On leave", Today.AddMonths(3), Owner, Now.AddDays(1));

        // Act
        var work = PolicyCampaignWork.Candidates(await harness.StateAsync(campaign), Today.AddDays(2),
            new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.True(acknowledged.IsSuccess, acknowledged.Error?.Message);
        Assert.True(waived.IsSuccess, waived.Error?.Message);
        Assert.Empty(work);
    }

    [Fact]
    public async Task ShouldYieldNoWorkGivenAClosedCampaign()
    {
        // Arrange
        await using var harness = new Harness();
        var campaign = await harness.LaunchAsync(Ada);
        await CampaignSeed.CloseAsync(harness.Provider, TenantId, campaign.Id, "Period ended", Owner,
            Now.AddDays(40));

        // Act
        var work = PolicyCampaignWork.Candidates(await harness.StateAsync(campaign), Today.AddDays(41),
            new Dictionary<Uuid, Uuid>());

        // Assert
        Assert.Empty(work);
    }

    [Fact]
    public async Task ShouldAskForCompletionGivenATrainingCampaign()
    {
        // Arrange
        await using var harness = new Harness();
        var campaign = await harness.LaunchAsync([Ada], new CampaignSubject("training", Uuid.CreateVersion4(),
            "TR-SEC", "Security awareness", 1, "sha-t1"));

        // Act
        var work = PolicyCampaignWork.Candidates(await harness.StateAsync(campaign), Today,
            new Dictionary<Uuid, Uuid> { [Ada] = AdaMemberId });

        // Assert
        var item = Assert.Single(work);
        Assert.Equal(PolicyCampaignWork.TrainingCompletion, item.Kind);
        Assert.EndsWith($"/campaigns/{campaign.Id}/completions", item.ActionPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLoadOpenCampaignWorkWithLinkedMembersGivenProgramCampaigns()
    {
        // Arrange
        await using var harness = new Harness();
        await using var scope = harness.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var dispatch = new RequestDispatchContext(RequestActor.System);
        var userId = Uuid.CreateVersion4();
        var person = new PersonRules(TenantId, Ada);
        Assert.True(person.Record("Ada", null, Owner, Now).IsSuccess);
        Assert.Null(person.CorrelateMembership(1, userId, Owner, Now));
        await PersonSeeds.AppendAsync(scope.ServiceProvider, person);
        var open = await harness.LaunchAsync(Ada, Bob);
        var closed = await harness.LaunchAsync(Ada);
        await CampaignSeed.CloseAsync(harness.Provider, TenantId, closed.Id, "Done", Owner, Now.AddDays(1));
        var checkpoint = await CheckpointAfterAsync(harness.Events);
        var directory = new Campaigns(checkpoint, Summary(open, "open"), Summary(closed, "closed"));

        // Act
        var work = await PolicyCampaignWork.LoadAsync(reader, Harness.Source(scope), directory,
            new CampaignDirectoryReadConsistency(directory, harness.Events), TenantId, ProgramId, Today,
            CancellationToken.None);

        // Assert
        Assert.True(work.IsSuccess, work.Error?.Message);
        Assert.Equal(2, work.Value.Count);
        Assert.Contains(work.Value, item => item.Responsible ==
            new OperatingHolder(OperatingAuthority.MemberHolder, RbacIds.Member(TenantId, userId)));
        Assert.Contains(work.Value, item => item.Responsible ==
            new OperatingHolder(OperatingAuthority.MemberHolder, OwnerMemberId));
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCampaignDirectoryBehindSource()
    {
        // Arrange
        await using var harness = new Harness();
        await using var scope = harness.Provider.CreateAsyncScope();
        await harness.LaunchAsync(Ada);
        var directory = new Campaigns(ProjectionCheckpoint.Start);

        // Act
        var result = await PolicyCampaignWork.LoadAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            Harness.Source(scope), directory, new CampaignDirectoryReadConsistency(directory, harness.Events),
            TenantId, ProgramId, Today, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCampaignSourceChangesDuringRead()
    {
        // Arrange
        await using var harness = new Harness();
        await using var scope = harness.Provider.CreateAsyncScope();
        var existing = await harness.LaunchAsync(Ada);
        var checkpoint = await CheckpointAfterAsync(harness.Events);
        var directory = new Campaigns(checkpoint, Summary(existing, "open"))
        {
            OnList = async _ =>
            {
                await harness.LaunchAsync(Bob);
            },
        };

        // Act
        var result = await PolicyCampaignWork.LoadAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            Harness.Source(scope), directory, new CampaignDirectoryReadConsistency(directory, harness.Events),
            TenantId, ProgramId, Today, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    static CampaignSummaryView Summary(PolicyDistributionCampaign campaign, string status) =>
        new(TenantId, ProgramId, campaign.Id, "policy", Uuid.CreateVersion4(), "POL-AC", 2,
            "Access Control Policy", Today.AddDays(30), status, 1, Now);

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           TenantId.ToString(), "policy-distribution-campaigns"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class Campaigns(ProjectionCheckpoint checkpoint, params CampaignSummaryView[] items)
        : ICampaignDirectoryReader
    {
        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;
        public Func<CancellationToken, ValueTask>? OnList { get; set; }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public async ValueTask<Page<CampaignSummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            if (OnList is not null)
                await OnList(ct);
            return new Page<CampaignSummaryView>(items, null);
        }

        public ValueTask<IReadOnlyList<Uuid>> ListForSubjectAsync(Uuid tenantId, Uuid programId,
            Uuid subjectId, long version, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<Uuid>>([]);
    }

    sealed class Harness : IAsyncDisposable
    {
        readonly InMemoryEventStore _events = new();

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(_events);
            services.AddSingleton<IDomainEventReader>(_events);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IImmutableContentStore>(new FitzKvImmutableContentStore(
                new PersistenceKvClient(new InMemoryKvClient())));
            services.AddScoped<NativeOperationExecutor>();
            services.AddPortia();
            services.AddVerifiedPolicyCampaignReader();
            Provider = services.BuildServiceProvider();
        }

        public ServiceProvider Provider { get; }

        public InMemoryEventStore Events => _events;

        public static CampaignSource Source(AsyncServiceScope scope) => new(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<IImmutableContentStore>(),
            scope.ServiceProvider.GetRequiredService<IDomainEventReader>());

        public Task<PolicyDistributionCampaign> LaunchAsync(params Uuid[] people) =>
            LaunchAsync(people, new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-AC",
                "Access Control Policy", 2, "sha-v2"));

        public async Task<PolicyDistributionCampaign> LaunchAsync(Uuid[] people, CampaignSubject subject)
        {
            var matches = people.ToDictionary(static person => person, person => new RosterMatch(person,
                "Person " + person.ToString()[..4], "employee", null, new DateOnly(2025, 1, 1)));
            var audience = new RosterAudience(matches, new HashSet<Uuid>(people));
            return await CampaignSeed.LaunchAsync(Provider, TenantId, ProgramId, Uuid.CreateVersion4(), subject,
                PolicyAudience.CoreSecurity, [], Uuid.CreateVersion4(), new string('d', 64), audience, Today.AddDays(30),
                "Read and acknowledge", Owner, OwnerMemberId, Now);
        }

        public async Task<CampaignState> StateAsync(PolicyDistributionCampaign campaign)
        {
            await using var scope = Provider.CreateAsyncScope();
            var state = await Source(scope).ReadAsync(TenantId, ProgramId, campaign.Id, CancellationToken.None);
            Assert.True(state.IsSuccess, state.Error?.Message);
            return state.Value;
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
