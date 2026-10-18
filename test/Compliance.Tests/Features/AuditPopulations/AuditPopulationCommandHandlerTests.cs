using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.AuditPopulation;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed class AuditPopulationCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldClassifyExistingRowGivenAttributedProgramActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, rowId) = await fixture.SeedPopulationAsync(reconciled: false);

        // Act
        var result = await fixture.ClassifyHandler().HandleAsync(Fixture.Context(
            fixture.UserId, new ClassifyAuditPopulationRow(fixture.TenantId, fixture.ProgramId,
                populationId, rowId, "excluded", "This change was outside the approved window.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, populationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var row = Assert.Single(saved.Current!.Rows);
        Assert.Equal("excluded", row.Classification);
        Assert.Equal("This change was outside the approved window.", row.ClassificationRationale);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), row.RecordedByMemberId);
    }

    [Fact]
    public async Task ShouldFreezeExactReconciledPopulationGivenRationale()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, _) = await fixture.SeedPopulationAsync(reconciled: true);

        // Act
        var result = await fixture.FreezeHandler().HandleAsync(Fixture.Context(fixture.UserId,
            new FreezeAuditPopulation(fixture.TenantId, fixture.ProgramId, populationId, 3,
                "The exact source rows and reconciliation were reviewed.")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, populationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("frozen", saved.Current!.Status);
        Assert.True(saved.Current.SampleEligible);
        Assert.Equal("The exact source rows and reconciliation were reviewed.",
            saved.Current.Current!.FreezeRationale);
    }

    [Fact]
    public async Task ShouldRevisePopulationRulesWithoutChangingItsFrozenCloseGivenReason()
    {
        // Arrange
        await using var fixture = new Fixture();
        var populationId = await fixture.SeedDefinedPopulationAsync();

        // Act
        var result = await fixture.ReviseDefinitionHandler().HandleAsync(Fixture.Context(
            fixture.UserId, new ReviseAuditPopulationDefinition(fixture.TenantId, fixture.ProgramId,
                populationId, 1, "Approved production change register", "Include approved changes.",
                "Exclude canceled and rejected changes.", "Clarify approved source boundaries.")),
            CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, populationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var content = saved.Current!.CurrentDefinition!.Content;
        Assert.Equal("Approved production change register", content.SourceUniverse);
        Assert.Equal("Include approved changes.", content.InclusionRules);
        Assert.Equal("Exclude canceled and rejected changes.", content.ExclusionRules);
        Assert.Equal("Clarify approved source boundaries.",
            saved.Current.CurrentDefinition.ChangeReason);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldStartCollectionForExactFrozenPeriodCloseGivenGenerationKey()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, closeSnapshotId) = await fixture.SeedDefinedPopulationWithCloseAsync();

        // Act
        var result = await fixture.GenerateHandler().HandleAsync(Fixture.Context(fixture.UserId,
            new GenerateAuditPopulation(fixture.TenantId, fixture.ProgramId, populationId,
                closeSnapshotId, "population-generation-1")), CancellationToken.None);
        var saved = await fixture.Reader.HydrateAsync(new AuditPopulationAggregate(fixture.TenantId,
            fixture.ProgramId, populationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("collecting", saved.Current!.Status);
        Assert.Equal("population-generation-1", saved.Current.GenerationKey);
        Assert.Empty(saved.Current.Rows);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly TimeProvider _clock = new FixedTimeProvider(Now.AddMinutes(5));
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }

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

        public ClassifyAuditPopulationRowHandler ClassifyHandler() => new(Executor, _clock);

        public FreezeAuditPopulationHandler FreezeHandler() => new(Executor, _clock);

        public ReviseAuditPopulationDefinitionHandler ReviseDefinitionHandler() =>
            new(Executor, _clock);

        public GenerateAuditPopulationHandler GenerateHandler() => new(Executor, _clock);

        public async Task<(Uuid PopulationId, Uuid RowId)> SeedPopulationAsync(bool reconciled)
        {
            var populationId = Uuid.CreateVersion4();
            var rowId = Uuid.CreateVersion4();
            var actorMemberId = RbacIds.Member(TenantId, UserId);
            var population = new AuditPopulationAggregate(TenantId, ProgramId, populationId);
            Assert.Null(population.Define(populationId, Definition(), ClosedPeriod(TenantId, ProgramId), actorMemberId,
                "Compliance lead", Now));
            Assert.Null(population.RecordFacts(population.Revision, SourceSnapshot(), [Row(rowId)],
                actorMemberId, "Compliance lead", Now.AddMinutes(1)));
            if (reconciled)
                Assert.Null(population.Reconcile(population.Revision,
                    new AuditPopulationReconciliationContent(1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                        "Every source row is accounted for."), actorMemberId,
                    "Compliance lead", Now.AddMinutes(2)));
            await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
            return (populationId, rowId);
        }

        public async Task<Uuid> SeedDefinedPopulationAsync()
        {
            var (populationId, _) = await SeedDefinedPopulationWithCloseAsync();
            return populationId;
        }

        public async Task<(Uuid PopulationId, Uuid CloseSnapshotId)> SeedDefinedPopulationWithCloseAsync()
        {
            var populationId = Uuid.CreateVersion4();
            var closePeriod = ClosedPeriod(TenantId, ProgramId);
            var population = new AuditPopulationAggregate(TenantId, ProgramId, populationId);
            Assert.Null(population.Define(populationId, Definition(), closePeriod,
                RbacIds.Member(TenantId, UserId), "Compliance lead", Now));
            await Writer.SaveAsync(population, new RequestDispatchContext(RequestActor.System));
            return (populationId, closePeriod.CloseSnapshots.Single().CloseSnapshotId);
        }

        AuditPopulationDefinitionContent Definition() => new("Production changes",
            "Production changes affecting the in-scope service.", "Approved change register",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            "Include approved changes.", "Exclude canceled changes.",
            "Reconcile all rows to the approved register.", ["control"], [ControlId]);

        static TypeIIPeriodView ClosedPeriod(Uuid tenantId, Uuid programId) => new(tenantId, programId,
            Uuid.CreateVersion4(), "closed", 4,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Hash('b'), new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31), Uuid.CreateVersion4(), Hash('c'), [], [], [], [], null, [],
            [new TypeIIPeriodCloseSnapshotView(Uuid.CreateVersion4(), 1, [], [], [], Hash('a'),
                null, null, [], [], Uuid.CreateVersion4(), "ceo", "CEO", Now)],
            Uuid.CreateVersion4(), "Compliance lead", Now);

        static AuditPopulationSourceSnapshotContent SourceSnapshot() => new(Uuid.CreateVersion4(), 1,
            "approved-change-register", Hash('d'), 1, true, "All approved changes are included.",
            Now.AddMinutes(1));

        static AuditPopulationRowContent Row(Uuid rowId) => new(rowId, "change-17",
            "approved-change-register", "included", "The change falls in the observation period.",
            new Dictionary<string, string> { ["control"] = "AC-1" }, [], []);

        static string Hash(char value) => new(value, 64);

        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request)
            where T : IRequestBase => new(request, new RequestDispatchContext(Actor(userId),
            new HttpInvocation("POST", "/api/v1/audit-populations/command",
                "/api/v1/audit-populations/command", "audit-population-test")));

        static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
                new Claim("email", "compliance-lead@example.test")], "BdgrzSession"));

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
