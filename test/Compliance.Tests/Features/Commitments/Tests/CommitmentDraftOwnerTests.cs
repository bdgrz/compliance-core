using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
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
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Commands;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Handlers;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Commitments.Tests;

public sealed class CommitmentDraftOwnerTests
{
    [Fact]
    public async Task ShouldRefuseOnlyMissingOrConflictingTargetGivenPrepareAcrossLifecycle()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var empty = await scenario.OwnerAsync();
        var emptyCreate = empty.Prepare(scenario.Program, true);
        var emptyChange = empty.Prepare(scenario.Program, false);
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var created = await scenario.OwnerAsync();

        // Act
        var duplicate = created.Prepare(scenario.Program, true);
        var otherProgram = created.Prepare(Uuid.CreateVersion4(), false);
        var matching = created.Prepare(scenario.Program, false);

        // Assert
        Assert.True(emptyCreate.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, emptyChange.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, otherProgram.Error!.Kind);
        Assert.True(matching.IsSuccess);
    }

    [Fact]
    public async Task ShouldReturnOriginalCreateWithoutNewFactGivenExactRetryAfterLaterRevisions()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        var first = await scenario.CreateDraftAsync();
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True((await scenario.ReviseAsync(Uuid.CreateVersion4(), 1, "Statement 2")).IsSuccess);
        Assert.True((await scenario.ReviseAsync(Uuid.CreateVersion4(), 2, "Statement 3")).IsSuccess);

        // Act
        var retry = await scenario.CreateDraftAsync();
        var facts = await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id);
        var owner = await scenario.OwnerAsync();

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(3, facts.Count);
        Assert.Equal(3L, owner.Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalRevisionAndRefuseChangedIntentGivenRetryAfterLaterRevision()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var command = Uuid.CreateVersion4();
        Assert.True((await scenario.ReviseAsync(command, 1, "Statement 2")).IsSuccess);
        Assert.True((await scenario.ReviseAsync(Uuid.CreateVersion4(), 2, "Statement 3")).IsSuccess);

        // Act
        var retry = await scenario.ReviseAsync(command, 1, "Statement 2");
        var changed = await scenario.ReviseAsync(command, 1, "Different statement");
        var facts = await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id);
        var owner = await scenario.OwnerAsync();

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(3, facts.Count);
        Assert.Equal(3L, owner.Revision);
    }

    [Fact]
    public async Task ShouldAppendNothingGivenStaleRevisionOrBlankStatement()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);

        // Act
        var stale = await scenario.ReviseAsync(Uuid.CreateVersion4(), 7, "Statement 2");
        var invalid = await scenario.ReviseAsync(Uuid.CreateVersion4(), 1, " ");
        var facts = await scenario.Source.FactsAsync(scenario.Tenant, scenario.Id);
        var owner = await scenario.OwnerAsync();

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.False(invalid.IsSuccess);
        Assert.Single(facts);
        Assert.Equal(1L, owner.Revision);
    }

    [Fact]
    public async Task ShouldRefuseCompetingAppendWithoutFactGivenStalePreparedSequenceOrPosition()
    {
        // Arrange
        await using var scenario = await Scenario.CreateAsync();
        Assert.True((await scenario.CreateDraftAsync()).IsSuccess);
        var owner = await scenario.OwnerAsync();
        var rules = await scenario.Source.Commitments.RulesAsync(scenario.Tenant, scenario.Program, scenario.Id);
        var before = rules.Authority(owner.Header!);
        var member = RbacIds.Member(scenario.Tenant, Uuid.CreateVersion4());
        var command = Uuid.CreateVersion4();
        Assert.Null(rules.Review(scenario.Program, 1, command, "accept", "Owner", "applicable", "supported", null,
            "Reviewed", member, "Reviewer", scenario.Source.Clock.Now, sourceVerifiedReference: "MSA", sourceEvidence: "Signed MSA checked"));
        var mutation = new CommitmentMutationContent(rules.PendingChanges.ToArray());
        var operation = OwnerProvenance.For(owner, command, nameof(ReviewCommitmentDraft), ActorReference.ForMember(member, "Reviewer"),
            scenario.Id.ToString(), 1, AuthoredIntentDigest.Compute(new ReviewCommitmentDraft(command, scenario.Tenant, scenario.Program,
                scenario.Id, 1, "accept", "Reviewed", "Owner", "applicable", "supported", SourceVerifiedReference: "MSA", SourceEvidence: "Signed MSA checked")),
            scenario.Source.Clock.Now);
        var fact = Assert.IsType<CommitmentReviewed>(await CommitmentSeeds.FreezeAsync(scenario.Source.Content, rules, mutation, operation, 2));
        var after = rules.Authority(fact.Header);
        var preparedSequence = owner.NativeSequence;
        var preparedPosition = owner.CommittedStreamPosition;

        // Act
        var staleSequence = owner.Append(fact, preparedSequence - 1, preparedPosition, mutation, before, after);
        var stalePosition = owner.Append(fact, preparedSequence, preparedPosition + 1, mutation, before, after);
        var sequenceAfterRefusals = owner.NativeSequence;
        var accepted = owner.Append(fact, preparedSequence, preparedPosition, mutation, before, after);
        var competing = owner.Append(fact, preparedSequence, preparedPosition, mutation, before, after);
        var raised = new AggregateScenario<CommitmentDraft>(owner).PendingEvents.Count;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, staleSequence.Error!.Kind);
        Assert.True(staleSequence.Error.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, stalePosition.Error!.Kind);
        Assert.True(stalePosition.Error.IsTransient);
        Assert.Equal(1L, sequenceAfterRefusals);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(2L, owner.NativeSequence);
        Assert.False(competing.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, competing.Error!.Kind);
        Assert.Equal(2L, owner.NativeSequence);
        Assert.Equal(1, raised);
    }

    sealed class Scenario : IAsyncDisposable
    {
        public CommitmentSourceFixture Source { get; } = new();
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Service { get; } = Uuid.CreateVersion4();
        public Uuid Author { get; } = Uuid.CreateVersion4();
        public Uuid Id => CommitmentDraft.IdFor(Tenant, Program, "service_commitment", "OWNER");
        public CreateCommitmentDraft Request => new(Id, Tenant, Program, Service, "service_commitment", "OWNER", "Statement 1", "Context", "MSA");
        public static async Task<Scenario> CreateAsync()
        {
            var scenario = new Scenario();
            var service = new ProductClientServiceRules(scenario.Tenant, scenario.Service);
            Assert.True(service.Create(scenario.Program, "Service", "Purpose", "Owner", scenario.Author,
                "Author", scenario.Source.Clock.Now).IsSuccess);
            await ProductClientServiceSeeds.WriteAsync(scenario.Source.Reader, scenario.Source.Writer, scenario.Source.Events, scenario.Source.Content, service);
            return scenario;
        }
        public ValueTask<Result<CommitmentDraftRegistration>> CreateDraftAsync() =>
            new CreateCommitmentDraftHandler(Source.Operations(), new ProviderSource(Source.Reader, Source.Content))
                .HandleAsync(CommitmentSourceFixture.Context(Request, Author), CancellationToken.None);
        public ValueTask<Result> ReviseAsync(Uuid command, long expectedRevision, string statement) =>
            new ReviseCommitmentDraftHandler(Source.Operations()).HandleAsync(CommitmentSourceFixture.Context(
                new ReviseCommitmentDraft(command, Tenant, Program, Id, expectedRevision, statement, "Context", "MSA"), Author),
                CancellationToken.None);
        public async Task<CommitmentDraft> OwnerAsync() => await Source.Reader.HydrateAsync(new CommitmentDraft(Tenant, Id));
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }
}
