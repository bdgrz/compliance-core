using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.AuditPopulation;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class SampleSelectionCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldRecordSelectionFromExactFrozenIncludedRowsGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, includedRowId, _, contentSha256) =
            await fixture.SeedFrozenPopulationAsync();
        var selectionId = Uuid.CreateVersion4();
        var command = fixture.RecordCommand(requestId, selectionId, populationId,
            [includedRowId]);

        // Act
        var result = await fixture.RecordHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var retry = await fixture.RecordHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        var selection = Assert.Single(saved.Current!.SampleSelections);
        Assert.Equal(selectionId, selection.SampleSelectionId);
        Assert.Equal(populationId, selection.PopulationSnapshotId);
        Assert.Equal(1, selection.PopulationVersion);
        Assert.Equal(contentSha256, selection.PopulationContentSha256);
        Assert.Equal("random", selection.SelectionMethod);
        Assert.Equal("external_email", selection.Source);
        Assert.Equal("sample-mail-001", selection.ExternalReference);
        Assert.Equal(includedRowId, Assert.Single(selection.SelectedRowIds));
    }

    [Fact]
    public async Task ShouldRejectExcludedPopulationRowsGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, _, excludedRowId, _) = await fixture.SeedFrozenPopulationAsync();
        var command = fixture.RecordCommand(requestId, Uuid.CreateVersion4(), populationId,
            [excludedRowId]);

        // Act
        var result = await fixture.RecordHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(saved.Current!.SampleSelections);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleAuditorRequestRevisionGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, includedRowId, _, _) = await fixture.SeedFrozenPopulationAsync();
        var command = fixture.RecordCommand(requestId, Uuid.CreateVersion4(), populationId,
            [includedRowId]) with
        { ExpectedRevision = 0 };

        // Act
        var result = await fixture.RecordHandler().HandleAsync(fixture.Context(command),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Current!.SampleSelections);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, includedRowId, _, _) = await fixture.SeedFrozenPopulationAsync();
        var command = fixture.RecordCommand(requestId, Uuid.CreateVersion4(), populationId,
            [includedRowId]);

        // Act
        var result = await fixture.RecordHandler().HandleAsync(
            new Cntryl.Portia.RequestContext<RecordSampleSelection>(command, RequestActor.System),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(saved.Current!.SampleSelections);
    }

    [Fact]
    public async Task ShouldAppendAmendedSelectionFromLaterFrozenVersionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, includedRowId, _, firstContentSha256) =
            await fixture.SeedFrozenPopulationAsync();
        var firstSelectionId = Uuid.CreateVersion4();
        var firstResult = await fixture.RecordHandler().HandleAsync(fixture.Context(
            fixture.RecordCommand(requestId, firstSelectionId, populationId, [includedRowId])),
            CancellationToken.None);
        Assert.True(firstResult.IsSuccess, firstResult.Error?.Message);
        var secondContentSha256 = await fixture.AmendPopulationAsync(populationId, includedRowId);
        var amendment = new AmendSampleSelection(fixture.TenantId, fixture.ProgramId,
            fixture.ExaminationId, requestId, 2, Uuid.CreateVersion4(), firstSelectionId,
            populationId, 2, [includedRowId], "targeted", "external_portal",
            "portal-export-119", "The auditor selected this row again from the amended population.");

        // Act
        var result = await fixture.AmendHandler().HandleAsync(fixture.Context(amendment),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditorRequest(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var selections = saved.Current!.SampleSelections;
        Assert.Equal(2, selections.Count);
        Assert.Equal(1, selections[0].PopulationVersion);
        Assert.Equal(firstContentSha256, selections[0].PopulationContentSha256);
        Assert.Null(selections[0].SupersedesSelectionId);
        Assert.Equal(2, selections[1].PopulationVersion);
        Assert.Equal(secondContentSha256, selections[1].PopulationContentSha256);
        Assert.Equal(firstSelectionId, selections[1].SupersedesSelectionId);
        Assert.Equal("The auditor selected this row again from the amended population.",
            selections[1].AmendmentReason);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddMinutes(5));
        readonly Uuid _userId = Uuid.CreateVersion4();
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid ExaminationId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

        Uuid OwnerMemberId => RbacIds.Member(TenantId, _userId);

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public RecordSampleSelectionHandler RecordHandler() => new(Executor, Reader, _clock);

        public AmendSampleSelectionHandler AmendHandler() => new(Executor, Reader, _clock);

        public RecordSampleSelection RecordCommand(Uuid requestId, Uuid selectionId,
            Uuid populationId, IReadOnlyList<Uuid> rows) => new(TenantId, ProgramId,
            ExaminationId, requestId, 1, selectionId, populationId, 1, rows, "random",
            "external_email", "sample-mail-001");

        public async Task<Uuid> SeedRequestAsync()
        {
            var requestId = Uuid.CreateVersion4();
            var aggregate = new AuditorRequest(TenantId, ProgramId, requestId);
            Assert.Null(aggregate.Create(new AuditorRequestContent(ExaminationId,
                "external_email", "audit-mail-001", "Select evidence for the examination.",
                "Approved production changes", "normal", OwnerMemberId,
                new DateOnly(2026, 11, 1), true), OwnerMemberId, "Compliance lead", Now));
            await Writer.SaveAsync(aggregate, new RequestDispatchContext(RequestActor.System));
            return requestId;
        }

        public async Task<(Uuid PopulationId, Uuid IncludedRowId, Uuid ExcludedRowId,
            string ContentSha256)> SeedFrozenPopulationAsync()
        {
            var populationId = Uuid.CreateVersion4();
            var controlId = Uuid.CreateVersion4();
            var includedRowId = Uuid.CreateVersion4();
            var excludedRowId = Uuid.CreateVersion4();
            var memberId = OwnerMemberId;
            var population = new AuditPopulationAggregate(TenantId, ProgramId, populationId);
            Assert.Null(population.Define(populationId, new AuditPopulationDefinitionContent(
                "Production changes", "Changes in the examination period.",
                "Approved change register", new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), "Include approved changes.",
                "Exclude canceled changes.", "Reconcile every source row.", ["control"],
                [controlId]), ClosedPeriod(ExaminationId), memberId, "Compliance lead", Now));
            Assert.Null(population.RecordFacts(population.Revision, SourceSnapshot(),
                [Row(includedRowId, "included"), Row(excludedRowId, "excluded")], memberId,
                "Compliance lead", Now.AddMinutes(1)));
            Assert.Null(population.Reconcile(population.Revision,
                new AuditPopulationReconciliationContent(2, 1, 1, 0, 0, 0, 0, 0, 0, 0,
                    "Every source row is accounted for."), memberId, "Compliance lead",
                Now.AddMinutes(2)));
            Assert.Null(population.Freeze(population.Revision,
                "The exact source rows and reconciliation were reviewed.", memberId,
                "Compliance lead", Now.AddMinutes(3)));
            var contentSha256 = population.Current!.Current!.ContentSha256;
            await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
            return (populationId, includedRowId, excludedRowId, contentSha256);
        }

        public async Task<string> AmendPopulationAsync(Uuid populationId, Uuid includedRowId)
        {
            var population = await Reader.HydrateAsync(new AuditPopulationAggregate(TenantId,
                ProgramId, populationId));
            var sourceReference = "approved-change-register-v2";
            Assert.Null(population.Amend(population.Revision,
                "The auditor received the corrected approved change register.",
                new AuditPopulationSourceSnapshotContent(Uuid.CreateVersion4(), 2,
                    sourceReference, Hash('e'), 2, true,
                    "The corrected export includes the same reconciled population.", Now.AddMinutes(4)),
                [Row(includedRowId, "included", sourceReference)], OwnerMemberId,
                "Compliance lead", Now.AddMinutes(4)));
            Assert.Null(population.Reconcile(population.Revision,
                new AuditPopulationReconciliationContent(2, 1, 1, 0, 0, 0, 0, 0, 0, 0,
                    "The corrected register reconciles to the frozen population."), OwnerMemberId,
                "Compliance lead", Now.AddMinutes(5)));
            Assert.Null(population.Freeze(population.Revision,
                "The corrected source was reconciled and reviewed.", OwnerMemberId,
                "Compliance lead", Now.AddMinutes(6)));
            var contentSha256 = population.Current!.Current!.ContentSha256;
            await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
            return contentSha256;
        }

        TypeIIPeriodView ClosedPeriod(Uuid examinationId) => new(TenantId: TenantId,
            ProgramId: ProgramId, PeriodId: Uuid.CreateVersion4(), Status: "closed", Revision: 4,
            ExaminationId: examinationId, TypeIIPlanId: Uuid.CreateVersion4(),
            TypeIIPlanVersion: 1, TypeIIPlanContentSha256: Hash('b'),
            PeriodStart: new DateOnly(2026, 1, 1), PeriodEnd: new DateOnly(2026, 12, 31),
            OpeningSnapshotId: Uuid.CreateVersion4(), OpeningSnapshotSha256: Hash('c'),
            OpeningSourceReferences: [], AcknowledgedOpeningGapIds: [], ExpectedOccurrences: [],
            Amendments: [], CloseValidation: null, CloseApprovals: [],
            CloseSnapshots: [new TypeIIPeriodCloseSnapshotView(Uuid.CreateVersion4(), 1, [],
                [], [], Hash('a'), null, null, [], [], Uuid.CreateVersion4(), "ceo", "CEO", Now)],
            ActorMemberId: Uuid.CreateVersion4(), ActorDisplay: "Compliance lead", UpdatedAt: Now);

        static AuditPopulationSourceSnapshotContent SourceSnapshot() => new(
            Uuid.CreateVersion4(), 1, "approved-change-register", Hash('d'), 2, true,
            "Complete export from the approved change register.", Now.AddMinutes(1));

        static AuditPopulationRowContent Row(Uuid rowId, string classification,
            string sourceReference = "approved-change-register") => new(rowId,
            $"change-{classification}", sourceReference, classification,
            "The row was classified against the approved change register.",
            new Dictionary<string, string> { ["control"] = "AC-1" }, [], []);

        static string Hash(char value) => new(value, 64);

        static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/auditor-requests/samples",
                "/api/v1/auditor-requests/samples", "sample-selection-handler-test")));

        public Cntryl.Portia.RequestContext<T> Context<T>(T request) where T : IRequestBase =>
            Context(_userId, request);

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "lead@example.test")], "BdgrzSession"));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
