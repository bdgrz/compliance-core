using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.Workforce.Projections;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class CassieWorkforceRepositoryTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Theory]
    [InlineData("person")]
    [InlineData("relationship")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("observation")]
    [InlineData("resolution")]
    public async Task ShouldPersistRowsAndProgressGivenCommittedNativeCassieBatch(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var checkpoint = new ProjectionCheckpoint(new EventCursor("workforce-progress-1"));
        await using (var scenario = Create(database.Database, kind, tenant, id))
        {
            await using var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start));
            await scenario.Apply();
            await batch.CommitAsync(checkpoint);
        }

        // Act
        await using var reopened = Create(database.Database, kind, tenant, id);
        var found = await reopened.Exists(tenant);
        var alien = await reopened.Exists(Uuid.CreateVersion4());
        var progress = await reopened.Store.LoadCheckpointAsync(reopened.Identity);

        // Assert
        Assert.True(found);
        Assert.False(alien);
        Assert.Equal(checkpoint, progress);
    }

    [Theory]
    [InlineData("person")]
    [InlineData("relationship")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("observation")]
    [InlineData("resolution")]
    public async Task ShouldDiscardRowsAndProgressGivenUncommittedNativeCassieBatch(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, id);

        // Act
        await using (await scenario.Store.BeginAsync(new ProjectionBatchContext(scenario.Identity, ProjectionCheckpoint.Start)))
            await scenario.Apply();

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
    }

    [Theory]
    [InlineData("person")]
    [InlineData("relationship")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("observation")]
    [InlineData("resolution")]
    public async Task ShouldKeepRebuildRowsOutOfLiveReadsGivenSeparateProjectionGeneration(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, Uuid.CreateVersion4());
        var rebuild = new CheckpointIdentity(scenario.Identity.ComponentName, scenario.Identity.Pattern, "rebuild-one");
        var checkpoint = new ProjectionCheckpoint(new EventCursor("rebuild-progress-1"));

        // Act
        await using (var batch = await scenario.Store.BeginAsync(new ProjectionBatchContext(rebuild, ProjectionCheckpoint.Start)))
        {
            await scenario.Apply();
            await batch.CommitAsync(checkpoint);
        }

        // Assert
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(scenario.Identity));
        Assert.Equal(checkpoint, await scenario.Store.LoadCheckpointAsync(rebuild));
    }

    [Theory]
    [InlineData("person")]
    [InlineData("relationship")]
    [InlineData("identity")]
    [InlineData("source")]
    [InlineData("observation")]
    [InlineData("resolution")]
    public async Task ShouldRefuseWithoutRowsGivenFactOutsideActiveTenant(string kind)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        var tenant = Uuid.CreateVersion4();
        await using var scenario = Create(database.Database, kind, tenant, Uuid.CreateVersion4());
        var alienIdentity = new CheckpointIdentity(scenario.Identity.ComponentName,
            EventStreamPattern.ForPattern(Uuid.CreateVersion4().ToString(), scenario.Identity.Pattern.Area));

        // Act
        Exception? failure;
        await using (await scenario.Store.BeginAsync(new ProjectionBatchContext(alienIdentity, ProjectionCheckpoint.Start)))
            failure = await Record.ExceptionAsync(async () => await scenario.Apply());

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(await scenario.Exists(tenant));
        Assert.Equal(ProjectionCheckpoint.Start, await scenario.Store.LoadCheckpointAsync(alienIdentity));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("1.invalid.invalid.invalid")]
    public async Task ShouldRejectCursorGivenMalformedContinuation(string cursor)
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var people = new CassiePersonDirectory(database.Database);

        // Act
        var failure = await Record.ExceptionAsync(async () => await people.ListAsync(Uuid.CreateVersion4(), 1, cursor));

        // Assert
        Assert.IsType<WorkforceDirectoryQueryException>(failure);
        Assert.Equal("The workforce cursor is invalid.", failure!.Message);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenAnotherTenantOrDirectory()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var people = new CassiePersonDirectory(database.Database);
        await using var identities = new CassieServiceIdentityDirectory(database.Database);
        var tenant = Uuid.CreateVersion4();
        await using (var batch = await people.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("PersonDirectoryV2", EventStreamPattern.ForPattern(tenant.ToString(), "people")), ProjectionCheckpoint.Start)))
        {
            await people.ApplyAsync(new PersonRecorded(tenant, Uuid.CreateVersion4(), "Ada", null, Author, Now));
            await people.ApplyAsync(new PersonRecorded(tenant, Uuid.CreateVersion4(), "Grace", null, Author, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await people.ListAsync(tenant, 1, null);

        // Act
        var tenantFailure = await Record.ExceptionAsync(async () => await people.ListAsync(Uuid.CreateVersion4(), 1, page.NextCursor));
        var directoryFailure = await Record.ExceptionAsync(async () => await identities.ListAsync(tenant, 1, page.NextCursor));

        // Assert
        Assert.NotNull(page.NextCursor);
        Assert.IsType<WorkforceDirectoryQueryException>(tenantFailure);
        Assert.IsType<WorkforceDirectoryQueryException>(directoryFailure);
    }

    [Fact]
    public async Task ShouldRejectCursorGivenDifferentSourceTargetFilter()
    {
        // Arrange
        await using var database = await CassieRepositoryTestDatabase.OpenAsync(CassieWorkforceSchema.Statements);
        await using var sources = new CassieWorkforceSourceDirectory(database.Database);
        var tenant = Uuid.CreateVersion4();
        var target = Uuid.CreateVersion4();
        await using (var batch = await sources.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("WorkforceSourcesV1", EventStreamPattern.ForPattern(tenant.ToString(), "workforce-source-observations")), ProjectionCheckpoint.Start)))
        {
            foreach (var index in Enumerable.Range(1, 2))
                await sources.ApplyAsync(new WorkforceSourceObserved(tenant, Uuid.CreateVersion4(),
                    new WorkforceSourceIdentity("hris", "source", index.ToString(System.Globalization.CultureInfo.InvariantCulture), "v1"),
                    "person", target, 1, new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null)), Now, Author, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var page = await sources.ListAsync(tenant, "person", target, 1, null);

        // Act
        var failure = await Record.ExceptionAsync(async () => await sources.ListAsync(tenant, "person", Uuid.CreateVersion4(), 1, page.NextCursor));

        // Assert
        Assert.NotNull(page.NextCursor);
        Assert.IsType<WorkforceDirectoryQueryException>(failure);
    }

    static Scenario Create(CassieProjectionDatabase database, string kind, Uuid tenant, Uuid id)
    {
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2026, 1, 1), null, "Engineering", null, null);
        var relationship = new WorkRelationshipRecorded(tenant, id, Uuid.CreateVersion4(), "E-100", terms, Author, Now);
        switch (kind)
        {
            case "person":
                var people = new CassiePersonDirectory(database);
                return new Scenario(people, "PersonDirectoryV2", "people", tenant,
                    () => people.ApplyAsync(new PersonRecorded(tenant, id, "Ada", null, Author, Now)),
                    async target => await people.GetAsync(target, id) is not null);
            case "relationship":
                var relationships = new CassieWorkRelationshipDirectory(database);
                return new Scenario(relationships, "WorkRelationshipDirectoryV1", "work-relationships", tenant,
                    () => relationships.ApplyAsync(relationship), async target => await relationships.GetAsync(target, id) is not null);
            case "identity":
                var identities = new CassieServiceIdentityDirectory(database);
                return new Scenario(identities, "ServiceIdentityDirectoryV1", "service-identities", tenant,
                    () => identities.ApplyAsync(new ServiceIdentityRecorded(tenant, id,
                        new ServiceIdentityTerms("Bot", "bot", "Approved purpose", null, "active", "person", Uuid.CreateVersion4(), new DateOnly(2027, 1, 1)), Author, Now)),
                    async target => await identities.GetAsync(target, id) is not null);
            case "source":
                var sources = new CassieWorkforceSourceDirectory(database);
                return new Scenario(sources, "WorkforceSourcesV1", "workforce-source-observations", tenant,
                    () => sources.ApplyAsync(new WorkforceSourceObserved(tenant, id, new WorkforceSourceIdentity("hris", "source", "100", "v1"),
                        "person", Uuid.CreateVersion4(), 1, new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null)), Now, Author, Now)),
                    async target => await sources.GetAsync(target, id) is not null);
            case "observation":
                var observations = new CassieWorkforceObservationDirectory(database);
                return new Scenario(observations, "WorkforceObservationsV1", "work-relationships", tenant,
                    () => observations.ApplyAsync(relationship),
                    async target => await observations.GetAsync(target, Uuid.CreateVersion5(id, "observation:1")) is not null);
            case "resolution":
                var resolutions = new CassieWorkforceObservationResolutionDirectory(database);
                return new Scenario(resolutions, "WorkforceObservationResolutionsV1", "workforce-observation-resolutions", tenant,
                    () => resolutions.ApplyAsync(new WorkforceObservationResolved(tenant, id, "resolved", "Reviewed", Author, Now)),
                    async target => (await resolutions.GetManyAsync(target, [id])).Count != 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    sealed class Scenario(CassieProjectionRepository store, string component, string pattern, Uuid tenant,
        Func<ValueTask> apply, Func<Uuid, ValueTask<bool>> exists) : IAsyncDisposable
    {
        public CassieProjectionRepository Store { get; } = store;
        public CheckpointIdentity Identity { get; } = new(component, EventStreamPattern.ForPattern(tenant.ToString(), pattern));
        public Func<ValueTask> Apply { get; } = apply;
        public Func<Uuid, ValueTask<bool>> Exists { get; } = exists;
        public ValueTask DisposeAsync() => Store.DisposeAsync();
    }
}
