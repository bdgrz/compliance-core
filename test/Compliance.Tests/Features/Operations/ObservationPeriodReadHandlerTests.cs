using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypeIIPlanAggregate = Bdgrz.Compliance.Features.Examinations.TypeIIPlan;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class ObservationPeriodReadHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReadCurrentLifecycleDatesAndScopeGivenExactNativePeriod()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedAsync(Now);

        // Act
        var result = await fixture.GetAsync(period.Id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.Tenant, result.Value!.TenantId);
        Assert.Equal(fixture.Program, result.Value.ProgramId);
        Assert.Equal(period.Id, result.Value.ObservationPeriodId);
        Assert.Equal(period.Current!.ExaminationId, result.Value.ExaminationId);
        Assert.Equal(period.Current.PeriodStart, result.Value.PeriodStart);
        Assert.Equal(period.Current.PeriodEnd, result.Value.PeriodEnd);
        Assert.Equal("active", result.Value.Status);
        Assert.Equal(1, result.Value.Revision);
        Assert.Equal(4, result.Value.ExpectedOccurrenceCount);
        Assert.Null(result.Value.LatestCloseSnapshotId);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("period")]
    public async Task ShouldHidePeriodGivenForeignTenantProgramOrIdentity(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedAsync(Now);
        var request = new GetObservationPeriod(changed == "tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            changed == "program" ? Uuid.CreateVersion4() : fixture.Program,
            changed == "period" ? Uuid.CreateVersion4() : period.Id);

        // Act
        var result = await new GetObservationPeriodHandler(fixture.Reader).HandleAsync(Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldComposeCurrentDatesGivenDirectoryRetainsOnlyOriginalOpeningIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedAsync(Now);
        await fixture.ProjectAsync();
        Assert.Null(period.Amend(period.Revision, period.Current!.PeriodStart, period.Current.PeriodEnd.AddDays(7),
            "Extend observation one week.", fixture.Member, "Period manager", Now.AddDays(1)));
        await TypeIIPeriodSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.Content, period);
        await fixture.ProjectAsync();
        var identity = Assert.Single((await fixture.Directory.ListProgramAsync(fixture.Tenant, fixture.Program, 50, null)).Items);

        // Act
        var result = await fixture.ListAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Now, identity.OpenedAt);
        var summary = Assert.Single(result.Value!.Items);
        Assert.Equal(period.Id, summary.ObservationPeriodId);
        Assert.Equal(period.Current!.PeriodEnd, summary.PeriodEnd);
        Assert.Equal(2, summary.Revision);
        Assert.Equal(Now.AddDays(1), summary.UpdatedAt);
    }

    [Fact]
    public async Task ShouldPageByStableOpeningGivenCurrentUpdatesAndForeignScopes()
    {
        // Arrange
        await using var fixture = new Fixture();
        var first = await fixture.SeedAsync(Now);
        var second = await fixture.SeedAsync(Now.AddMinutes(1));
        var third = await fixture.SeedAsync(Now.AddMinutes(2));
        await fixture.SeedAsync(Now, program: Uuid.CreateVersion4());
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedAsync(Now, tenant: otherTenant);
        Assert.Null(first.Amend(first.Revision, first.Current!.PeriodStart, first.Current.PeriodEnd.AddDays(7),
            "Extend the first period.", fixture.Member, "Period manager", Now.AddDays(1)));
        await TypeIIPeriodSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.Content, first);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(otherTenant);

        // Act
        var page1 = await fixture.ListAsync(2);
        var page2 = await fixture.ListAsync(2, page1.Value!.NextCursor);

        // Assert
        Assert.True(page1.IsSuccess, page1.Error?.Message);
        Assert.True(page2.IsSuccess, page2.Error?.Message);
        Assert.Equal(new[] { first.Id, second.Id }, page1.Value!.Items.Select(value => value.ObservationPeriodId));
        Assert.Equal(2, page1.Value.Items[0].Revision);
        Assert.Equal(third.Id, Assert.Single(page2.Value!.Items).ObservationPeriodId);
        Assert.Null(page2.Value.NextCursor);
        Assert.All(page1.Value.Items.Concat(page2.Value.Items), value =>
        {
            Assert.Equal(fixture.Tenant, value.TenantId);
            Assert.Equal(fixture.Program, value.ProgramId);
        });
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    public async Task ShouldRejectCursorGivenAnotherScope(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(Now);
        await fixture.SeedAsync(Now.AddMinutes(1));
        var otherProgram = Uuid.CreateVersion4();
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedAsync(Now, tenant: otherTenant);
        await fixture.SeedAsync(Now, program: otherProgram);
        await fixture.ProjectAsync();
        await fixture.ProjectAsync(otherTenant);
        var first = await fixture.ListAsync(1);

        // Act
        var result = await fixture.ListAsync(1, first.Value!.NextCursor,
            tenant: changed == "tenant" ? otherTenant : fixture.Tenant,
            program: changed == "program" ? otherProgram : fixture.Program);

        // Assert
        Assert.NotNull(first.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(201, null)]
    [InlineData(50, "invalid-cursor")]
    public async Task ShouldRejectInvalidPaginationGivenScopedList(int limit, string? cursor)
    {
        // Arrange
        await using var fixture = new Fixture();

        // Act
        var result = await fixture.ListAsync(limit, cursor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnPendingConflictGivenOpeningNotYetProjected()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(Now);

        // Act
        var result = await fixture.ListAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetryWholePageGivenSourceChangesDuringCurrentAuthorityRead(bool project)
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedAsync(Now);
        await fixture.ProjectAsync();
        var hooked = new HookedDirectory(fixture.Directory, async () =>
        {
            await fixture.SeedAsync(Now.AddMinutes(1));
            if (project)
                await fixture.ProjectAsync();
        });
        var handler = new ListObservationPeriodsHandler(hooked, new ObservationPeriodDirectoryReadConsistency(hooked, fixture.Events), fixture.Reader);

        // Act
        var result = await handler.HandleAsync(Context(new ListObservationPeriods(fixture.Tenant, fixture.Program)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldKeepOriginalIdentityGivenProjectionReplayAndRejectChangedProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedAsync(Now);
        await fixture.ProjectAsync();
        ObservationPeriodStarted? opening = null;
        await foreach (var record in fixture.Events.ReadAsync(new TypeIIPeriod(fixture.Tenant, fixture.Program, period.Id).Stream, 0))
            if (record.Event is ObservationPeriodStarted started)
                opening = started;
        Assert.NotNull(opening);
        var checkpoint = await fixture.Directory.LoadCheckpointAsync(fixture.Tenant);
        // Act
        await using (var batch = await fixture.Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
            FitzObservationPeriodDirectory.ProjectorName, EventStreamPattern.ForPattern(fixture.Tenant.ToString(), TypeIIPeriod.Area)), checkpoint)))
        {
            await fixture.Directory.ApplyAsync(opening);
            var changed = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Directory.ApplyAsync(opening with
            { Header = opening.Header with { ProgramId = Uuid.CreateVersion4() } }).AsTask());
            await batch.CommitAsync(checkpoint);
            Assert.Contains("cannot change", changed.Message, StringComparison.Ordinal);
        }

        // Assert
        Assert.Equal(period.Id, Assert.Single((await fixture.Directory.ListProgramAsync(fixture.Tenant, fixture.Program, 50, null)).Items).ObservationPeriodId);
    }

    [Fact]
    public async Task ShouldReadExactHistoricalCloseGivenLaterImmutableAmendment()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedAsync();
        var original = period.Current!.CloseSnapshots[0];
        await fixture.AmendClosedAsync(period);
        await fixture.ProjectAsync();

        // Act
        var result = await fixture.CloseAsync(original.CloseSnapshotId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(period.Id, result.Value!.PeriodId);
        Assert.Equal(original.CloseSnapshotId, result.Value.Snapshot.CloseSnapshotId);
        Assert.Equal(original.ContentSha256, result.Value.Snapshot.ContentSha256);
        Assert.Equal(2, result.Value.CloseSnapshots.Count);
        Assert.Equal(original.CloseSnapshotId, result.Value.CloseSnapshots[^1].AmendsCloseSnapshotId);
    }

    [Fact]
    public async Task ShouldReturnConflictGivenScopedCloseIdentityIsAmbiguousAcrossPeriods()
    {
        // Arrange
        await using var fixture = new Fixture();
        var close = Uuid.CreateVersion4();
        await fixture.SeedClosedAsync(close);
        await fixture.SeedClosedAsync(close);
        await fixture.ProjectAsync();

        // Act
        var result = await fixture.CloseAsync(close);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
        Assert.Equal(2, (await fixture.Directory.FindAsync(fixture.Tenant, fixture.Program, close)).Count);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("program")]
    [InlineData("close")]
    public async Task ShouldHideHistoricalCloseGivenAnotherScopeOrIdentity(string changed)
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedAsync();
        var close = period.Current!.CloseSnapshots[0].CloseSnapshotId;
        await fixture.ProjectAsync();

        // Act
        var result = await fixture.CloseAsync(changed == "close" ? Uuid.CreateVersion4() : close,
            tenant: changed == "tenant" ? Uuid.CreateVersion4() : fixture.Tenant,
            program: changed == "program" ? Uuid.CreateVersion4() : fixture.Program);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTypedFailureGivenHistoricalCloseBodyCannotBeVerified(bool unavailable)
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedAsync();
        await fixture.ProjectAsync();
        var reader = new TypeIIPeriodReader(fixture.Reader, fixture.Events, new FaultedContent(fixture.Content, unavailable));
        var handler = new GetObservationPeriodCloseHandler(fixture.Directory,
            new ObservationPeriodDirectoryReadConsistency(fixture.Directory, fixture.Events), reader);

        // Act
        var result = await handler.HandleAsync(Context(new GetObservationPeriodClose(fixture.Tenant, fixture.Program,
            period.Current!.CloseSnapshots[0].CloseSnapshotId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldReturnPendingConflictGivenCloseIdentityHasNotReachedItsNativeSource()
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedAsync();

        // Act
        var result = await fixture.CloseAsync(period.Current!.CloseSnapshots[0].CloseSnapshotId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetryHistoricalCloseGivenNativeSourceChangesDuringLocatorRead(bool project)
    {
        // Arrange
        await using var fixture = new Fixture();
        var period = await fixture.SeedClosedAsync();
        await fixture.ProjectAsync();
        var directory = new HookedCloseDirectory(fixture.Directory, async () =>
        {
            await fixture.SeedAsync(Now.AddMinutes(1));
            if (project)
                await fixture.ProjectAsync();
        });
        var handler = new GetObservationPeriodCloseHandler(directory,
            new ObservationPeriodDirectoryReadConsistency(fixture.Directory, fixture.Events),
            new TypeIIPeriodReader(fixture.Reader, fixture.Events, fixture.Content));

        // Act
        var result = await handler.HandleAsync(Context(new GetObservationPeriodClose(fixture.Tenant, fixture.Program,
            period.Current!.CloseSnapshots[0].CloseSnapshotId)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    static Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase => new(request, new RequestDispatchContext(RequestActor.System));

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Member { get; } = Uuid.CreateVersion4();
        public InMemoryEventStore Events { get; } = new();
        public IImmutableContentStore Content { get; } = TypeIIPeriodSeeds.NewContentStore();
        public FitzObservationPeriodDirectory Directory { get; } = new(new InMemoryKvClient());
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }
        public async Task<TypeIIPeriodRules> SeedAsync(DateTimeOffset at, Uuid? tenant = null, Uuid? program = null)
        {
            var realm = tenant ?? Tenant;
            var scope = program ?? Program;
            var exam = Uuid.CreateVersion4();
            var start = new DateOnly(2026, 10, 1);
            var end = new DateOnly(2027, 9, 30);
            ExaminationContentReference[] sources = [new("control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('a', 64))];
            var plan = new TypeIIPlanAggregate(realm, scope, Uuid.CreateVersion4());
            var terms = new TypeIIPlanContent(exam, Uuid.CreateVersion4(), new string('b', 64), Uuid.CreateVersion4(), start, end,
                "Audit Firm", ["security"], Uuid.CreateVersion4(), Uuid.CreateVersion4(), sources,
                [new TypeIIControlPlanEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Quarterly control", Member, "quarterly", 4,
                    start, null, ExpectedEvidenceDescriptions: ["Dated review"])], [], [], []);
            Assert.Null(plan.Propose(terms, Member, "Plan author", at.AddHours(-3)));
            Assert.Null(plan.Review(plan.Revision, 1, "approve", "Reviewed exact plan.", Uuid.CreateVersion4(), "Independent reviewer", at.AddHours(-2)));
            Assert.Null(plan.Approve(plan.Revision, 1, "Approved exact plan.", Uuid.CreateVersion4(), "CISO", "Executive approver", at.AddHours(-1)));
            var period = new TypeIIPeriodRules(realm, scope, Uuid.CreateVersion4());
            Assert.Null(period.Start(plan.Current!, new TypeIIPeriodOpeningContent(exam, Uuid.CreateVersion4(), new string('c', 64), sources, []),
                Member, "Period manager", at));
            await TypeIIPeriodSeeds.WriteAsync(Writer, Content, period);
            return period;
        }
        public async Task<TypeIIPeriodRules> SeedClosedAsync(Uuid? closeId = null)
        {
            var period = await SeedAsync(Now);
            var expected = period.Current!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            var at = Now.AddYears(1);
            Assert.Null(period.RecordCloseValidation(period.Revision, new TypeIIPeriodCloseValidationContent(
                period.Current.OpeningSourceReferences, expected, expected, [], [], []), Uuid.CreateVersion4(), "Close validator", at));
            Assert.Null(period.ApproveClose(period.Revision, [], "Reviewed exact close.", Member, "CEO", "Executive approver", at.AddMinutes(1)));
            Assert.Null(period.FreezeClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(closeId ?? Uuid.CreateVersion4(),
                period.Current.CloseValidation!.SourceReferences, expected, []), Member, "CEO", at.AddMinutes(2)));
            await TypeIIPeriodSeeds.AppendAsync(Reader, Writer, Content, period);
            return period;
        }
        public async Task AmendClosedAsync(TypeIIPeriodRules period)
        {
            var prior = period.Current!.CloseSnapshots[^1];
            var at = Now.AddYears(1).AddDays(1);
            var source = prior.SourceReferences[0] with
            {
                VersionId = Uuid.CreateVersion4(),
                Version = prior.SourceReferences[0].Version + 1,
                ContentSha256 = new string('f', 64)
            };
            var operation = RootOperation.Provenance(Uuid.CreateVersion5(period.Id, "seed:" + nameof(AmendTypeIIPeriodClose) + ":" + (period.Revision + 1)),
                nameof(AmendTypeIIPeriodClose), period.Stream, ActorReference.ForMember(Member, "Executive approver"),
                period.Revision, new string('e', 64), at);
            var publication = await Content.WriteJsonAsync(Tenant.ToString(), Uuid.CreateVersion4(), SnapshotSourcePublicationManifest.Schema,
                new SnapshotSourcePublicationManifest(Tenant, Program, period.Current.ExaminationId, []));
            var reconciliation = new TypeIIPeriodCloseValidationContent([source], prior.ExpectedOccurrenceIds, prior.ExpectedOccurrenceIds,
                [], [], prior.AcknowledgedMaterialItemIds)
            { SourceContentManifest = publication, RecordedThrough = at };
            var proof = await new TypeIIPeriodCloseMaterialReader(Events, Content).FreezeAsync(period.Current, prior, reconciliation, operation, CancellationToken.None);
            var evidence = await Content.ReadJsonAsync<TypeIIPeriodCloseMaterialProof>(Tenant.ToString(), proof);
            Assert.Null(period.AmendClose(period.Revision, new TypeIIPeriodCloseSnapshotContent(Uuid.CreateVersion4(), [source],
                prior.ExpectedOccurrenceIds, prior.AcknowledgedMaterialItemIds)
            { SourceContentManifest = publication },
                "Correct the exact source version.", evidence.ChangedRecordIds, evidence.AffectedExaminationMaterialIds,
                Member, "CEO", "Executive approver", at, proof));
            await TypeIIPeriodSeeds.AppendAsync(Reader, Writer, Content, period);
        }
        public Task<Result<ObservationPeriodCloseView>> CloseAsync(Uuid closeId, Uuid? tenant = null, Uuid? program = null) =>
            new GetObservationPeriodCloseHandler(Directory, new ObservationPeriodDirectoryReadConsistency(Directory, Events),
                new TypeIIPeriodReader(Reader, Events, Content)).HandleAsync(Context(new GetObservationPeriodClose(
                    tenant ?? Tenant, program ?? Program, closeId)), CancellationToken.None).AsTask();
        public Task<Result<ObservationPeriodView>> GetAsync(Uuid id) => new GetObservationPeriodHandler(Reader)
            .HandleAsync(Context(new GetObservationPeriod(Tenant, Program, id)), CancellationToken.None).AsTask();
        public Task<Result<Page<ObservationPeriodSummaryView>>> ListAsync(int? limit = null, string? cursor = null,
            Uuid? tenant = null, Uuid? program = null) => new ListObservationPeriodsHandler(Directory,
                new ObservationPeriodDirectoryReadConsistency(Directory, Events), Reader)
            .HandleAsync(Context(new ListObservationPeriods(tenant ?? Tenant, program ?? Program, limit, cursor)), CancellationToken.None).AsTask();
        public async Task ProjectAsync(Uuid? tenantId = null)
        {
            var tenant = tenantId ?? Tenant;
            var checkpoint = await Directory.LoadCheckpointAsync(tenant);
            var pattern = EventStreamPattern.ForPattern(tenant.ToString(), TypeIIPeriod.Area);
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(
                FitzObservationPeriodDirectory.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in Events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                if (record.Event is ObservationPeriodStarted started)
                    await Directory.ApplyAsync(started);
                else if (record.Event is ObservationPeriodCloseFrozen frozen)
                    await Directory.ApplyAsync(frozen);
                else if (record.Event is TypeIIPeriodCloseAmended amended)
                    await Directory.ApplyAsync(amended);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class HookedDirectory(IObservationPeriodDirectoryReader inner, Func<Task> afterPage) : IObservationPeriodDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId, CancellationToken ct = default) => inner.LoadCheckpointAsync(tenantId, ct);
        public async ValueTask<Page<ObservationPeriodIdentity>> ListProgramAsync(Uuid tenantId, Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            var page = await inner.ListProgramAsync(tenantId, programId, limit, cursor, ct);
            await afterPage();
            return page;
        }
    }
    sealed class HookedCloseDirectory(IObservationPeriodCloseDirectoryReader inner, Func<Task> afterRead) : IObservationPeriodCloseDirectoryReader
    {
        public async ValueTask<IReadOnlyList<ObservationPeriodCloseIdentity>> FindAsync(Uuid tenantId, Uuid programId, Uuid closeId, CancellationToken ct = default)
        {
            var values = await inner.FindAsync(tenantId, programId, closeId, ct);
            await afterRead();
            return values;
        }
    }

    sealed class FaultedContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw (unavailable ? new ImmutableContentUnavailableException("The exact historical close body is absent.") :
                new ImmutableContentIntegrityException("The exact historical close body is corrupt."));
    }
}
