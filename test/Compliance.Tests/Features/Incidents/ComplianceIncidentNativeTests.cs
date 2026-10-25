using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Incidents;

public sealed class ComplianceIncidentNativeTests
{
    [Fact]
    public async Task ShouldReturnNoScopedIncidentGivenValidIdentityRequestedUnderAnotherProgram()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var body = fixture.Body();
        await fixture.SeedAsync(body);
        var otherProgram = Uuid.CreateVersion4();
        var before = await fixture.FactsAsync();

        // Act
        var read = await fixture.Incidents.ReadAsync(fixture.Tenant, otherProgram, fixture.Incident);
        var authority = await fixture.Incidents.ReadAuthorityAsync(fixture.Tenant, otherProgram, fixture.Incident);
        var changed = await new ReviseComplianceIncidentHandler(fixture.Operations).HandleAsync(fixture.Context(
            new ReviseComplianceIncident(Uuid.CreateVersion4(), fixture.Tenant, otherProgram, fixture.Incident, 1,
                body with { Title = "Wrong program write" }, "Forbidden scope change.")), CancellationToken.None);

        // Assert
        Assert.Null(read.View);
        Assert.Null(authority);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, changed.Error.Kind);
        Assert.Equal(before.Count, (await fixture.FactsAsync()).Count);
    }

    [Fact]
    public async Task ShouldReplayOriginalCreationGivenLaterCorrectionAndClockAdvance()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var body = fixture.Body();
        var request = new RecordComplianceIncident(fixture.Tenant, fixture.Program, fixture.Incident, body);
        var handler = new RecordComplianceIncidentHandler(fixture.Operations);
        Assert.True((await handler.HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        Assert.True((await new ReviseComplianceIncidentHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseComplianceIncident(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Incident, 1, body with { Title = "Corrected current narrative" }, "Correction")), CancellationToken.None)).IsSuccess);

        // Act
        var replay = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var changed = await handler.HandleAsync(fixture.Context(request with { Content = body with { Title = "Changed authored creation" } }), CancellationToken.None);
        var actor = await handler.HandleAsync(fixture.Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var read = await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, actor.Error!.Kind);
        Assert.Equal("Corrected current narrative", read.View!.Current!.Content.Title);
        Assert.Equal(2, read.View.Revision);
        var facts = await fixture.FactsAsync();
        Assert.Equal(2, facts.Count);
        Assert.All(facts, value => Assert.Equal(read.Owner.Stream, ((IComplianceIncidentFact)value).Operation.FirstOwner));
    }
    [Fact]
    public async Task ShouldRecoverOriginalReceiptGivenUnknownIncidentAppendOutcome()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await fixture.SeedAsync();
        var operations = new IncidentOperations(new NativeOperationExecutor(fixture.Reader, new CommitThenFailExecutor(fixture.Executor), fixture.Content),
            fixture.Incidents, fixture.Content, fixture.Clock);
        var original = (await fixture.Incidents.ReadCurrentAsync(fixture.Tenant, fixture.Program, fixture.Incident)).View!.Current!.Content;
        var request = new ReviseComplianceIncident(Uuid.CreateVersion4(), fixture.Tenant, fixture.Program, fixture.Incident, 1,
            original with { Title = "Exact accepted correction" }, "Correction");

        // Act
        var result = await new ReviseComplianceIncidentHandler(operations).HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, (await fixture.FactsAsync()).Count);
        var fact = Assert.IsType<ComplianceIncidentRevised>((await fixture.FactsAsync())[^1]);
        Assert.Equal(request.CommandId, fact.Operation.LogicalId);
        Assert.Equal("mutation-result-content.v1", fact.OperationResult.Schema);
    }
    [Fact]
    public async Task ShouldEditCurrentIncidentWithoutHistoricalBodiesGivenOlderVersionUnavailable()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var body = fixture.Body();
        await fixture.SeedAsync(body);
        var initial = Assert.IsType<ComplianceIncidentRecorded>(Assert.Single(await fixture.FactsAsync()));
        var authority = await fixture.Content.ReadJsonAsync<ComplianceIncidentCurrentAuthority>(fixture.Tenant.ToString(), initial.CurrentContent);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        Assert.True((await new ReviseComplianceIncidentHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseComplianceIncident(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Incident, 1, body with { Title = "Second version" }, "Correction")), CancellationToken.None)).IsSuccess);
        var failed = new FailedContent(fixture.Content, value => value.ContentId == authority.VersionContent.ContentId);
        var reader = new ComplianceIncidentReader(fixture.Reader, fixture.Events, failed);
        var operations = new IncidentOperations(new NativeOperationExecutor(fixture.Reader, fixture.Executor, failed), reader, failed, fixture.Clock);

        // Act
        var result = await new ReviseComplianceIncidentHandler(operations).HandleAsync(fixture.Context(new ReviseComplianceIncident(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Incident, 2, body with { Title = "Third version" }, "Correction")), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(0, failed.Reads);
        var current = await reader.ReadCurrentAsync(fixture.Tenant, fixture.Program, fixture.Incident);
        Assert.Equal("Third version", current.View!.Current!.Content.Title);
        Assert.Single(current.View.Versions);
        Assert.Equal(3, (await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident)).View!.Versions.Count);
        await Assert.ThrowsAsync<ImmutableContentUnavailableException>(async () => await reader.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident));
        Assert.Equal(1, failed.Reads);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholeScopedReadGivenMissingOrCorruptNativeIncidentContent(bool unavailable)
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await fixture.SeedAsync();
        var failed = new FailedContent(fixture.Content, value => value.Schema == ComplianceIncidentContentStore.VersionSchema, unavailable);
        var reader = new ComplianceIncidentReader(fixture.Reader, fixture.Events, failed);
        var handler = new GetComplianceIncidentHandler(reader, new(new AllowPermissions()), new(fixture.Reader));

        // Act
        var result = await handler.HandleAsync(fixture.Context(new GetComplianceIncident(fixture.Tenant, fixture.Program, fixture.Incident)), CancellationToken.None);
        var foreign = await handler.HandleAsync(fixture.Context(new GetComplianceIncident(fixture.Tenant, Uuid.CreateVersion4(), fixture.Incident)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, foreign.Error!.Kind);
        Assert.Equal(1, failed.Reads);
        Assert.Single(await fixture.FactsAsync());
    }
    [Fact]
    public async Task ShouldUseFrozenReceiptCutGivenLaterIncidentCorrection()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        var body = fixture.Body();
        await fixture.SeedAsync(body);
        var cutoff = fixture.Clock.Now;
        fixture.Clock.Now = cutoff.AddHours(1);
        Assert.True((await new ReviseComplianceIncidentHandler(fixture.Operations).HandleAsync(fixture.Context(new ReviseComplianceIncident(Uuid.CreateVersion4(),
            fixture.Tenant, fixture.Program, fixture.Incident, 1, body with { Title = "Later correction" }, "Correction")), CancellationToken.None)).IsSuccess);

        // Act
        var historical = await fixture.Incidents.ReadThroughAsync(fixture.Tenant, fixture.Program, fixture.Incident, cutoff);
        var current = await fixture.Incidents.ReadAsync(fixture.Tenant, fixture.Program, fixture.Incident);

        // Assert
        Assert.Equal(body.Title, historical.View!.Current!.Content.Title);
        Assert.Equal(1, historical.View.Revision);
        Assert.Equal("Later correction", current.View!.Current!.Content.Title);
        Assert.Equal(2, current.View.Revision);
    }
    [Fact]
    public async Task ShouldFenceDirectoryLagGivenIncidentListRequiresCompleteNativeSource()
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await fixture.SeedAsync();
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient(), fixture.Incidents);
        var handler = new ListComplianceIncidentsHandler(directory, new(new AllowPermissions()), new(directory, fixture.Events));
        var context = fixture.Context(new ListComplianceIncidents(fixture.Tenant, fixture.Program, 50));

        // Act
        var behind = await handler.HandleAsync(context, CancellationToken.None);
        await ProjectAsync(directory, fixture);
        var current = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, behind.Error!.Kind);
        Assert.True(behind.Error.IsTransient);
        Assert.True(current.IsSuccess, current.Error?.Message);
        Assert.Equal("Production credential incident", Assert.Single(current.Value.Items).Name);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldFailWholePageGivenListedIncidentBodyUnavailableOrCorrupt(bool unavailable)
    {
        // Arrange
        await using var fixture = new IncidentNativeFixture();
        await fixture.SeedAsync();
        var failed = new FailedContent(fixture.Content, value => value.Schema == ComplianceIncidentContentStore.VersionSchema, unavailable);
        var reader = new ComplianceIncidentReader(fixture.Reader, fixture.Events, failed);
        var directory = new FitzComplianceIncidentDirectory(new InMemoryKvClient(), reader);
        await ProjectAsync(directory, fixture);
        var handler = new ListComplianceIncidentsHandler(directory, new(new AllowPermissions()), new(directory, fixture.Events));

        // Act
        var result = await handler.HandleAsync(fixture.Context(new ListComplianceIncidents(fixture.Tenant, fixture.Program, 50)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(unavailable, result.Error.IsTransient);
        Assert.Single(await fixture.FactsAsync());
    }
    static async Task ProjectAsync(FitzComplianceIncidentDirectory directory, IncidentNativeFixture fixture)
    {
        var checkpoint = await directory.LoadCheckpointAsync(fixture.Tenant);
        var pattern = EventStreamPattern.ForPattern(fixture.Tenant.ToString(), ComplianceIncident.Area);
        await using var batch = await directory.BeginAsync(new(new(ComplianceIncidentDirectoryProjector.ProjectorName, pattern), checkpoint));
        var cursor = checkpoint.Cursor;
        await foreach (var record in fixture.Events.ReadAsync(pattern, cursor, CancellationToken.None))
        { if (record.Event is IComplianceIncidentFact) await directory.ApplyAsync(record.Event); cursor = record.NextCursor; }
        await batch.CommitAsync(new(cursor));
    }
    sealed class FailedContent(IImmutableContentStore inner, Func<ImmutableContentRef, bool> reject, bool unavailable = true) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reject(reference))
            { Reads++; throw unavailable ? new ImmutableContentUnavailableException("Missing native incident component") : new ImmutableContentIntegrityException("Corrupt native incident component"); }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
    sealed class AllowPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenant, Uuid user, Uuid member, string permission, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class CommitThenFailExecutor(IAggregateExecutor inner) : IAggregateExecutor
    {
        int _calls;
        public ValueTask<Result> ExecuteAsync<T>(T aggregate, Func<T, AggregateOutcome> action, IExecutionContext context, CancellationToken ct) where T : Aggregate => inner.ExecuteAsync(aggregate, action, context, ct);
        public async ValueTask<Result<TOut>> ExecuteAsync<T, TOut>(T aggregate, Func<T, AggregateOutcome<TOut>> action, IExecutionContext context, CancellationToken ct) where T : Aggregate
        {
            var result = await inner.ExecuteAsync(aggregate, action, context, ct);
            if (Interlocked.Increment(ref _calls) == 1)
                throw new EventStreamConcurrencyException("Unknown incident append outcome", new TimeoutException());
            return result;
        }
    }
}
