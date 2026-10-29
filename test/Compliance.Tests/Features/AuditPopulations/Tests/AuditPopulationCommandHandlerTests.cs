using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Tests.Testing;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.Rules.AuditPopulationRules;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
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
using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Handlers;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;

public sealed class AuditPopulationCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 18, 0, 0, TimeSpan.Zero);

    static string Hash(char value) => new(value, 64);

    [Fact]
    public async Task ShouldClassifyExistingRowGivenAttributedProgramActor()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, rowId) = await fixture.SeedPopulationAsync(reconciled: false);

        // Act
        var result = await fixture.ClassifyHandler().HandleAsync(Fixture.Context(
            fixture.UserId, new ClassifyAuditPopulationRow(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                populationId, rowId, "excluded", "This change was outside the approved window.")),
            CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

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
            new FreezeAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, populationId, 3,
                "The exact source rows and reconciliation were reviewed.")), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

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
            fixture.UserId, new ReviseAuditPopulationDefinition(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                populationId, 1, "Approved production change register", "Include approved changes.",
                "Exclude canceled and rejected changes.", "Clarify approved source boundaries.")),
            CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

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
    public async Task ShouldRecordExactManualSourceSnapshotAndRowsGivenCurrentDefinitionRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, _) = await fixture.SeedDefinedPopulationWithCloseAsync();
        var rowId = Uuid.CreateVersion4();
        var request = new RecordAuditPopulationFacts(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            populationId, 1, Fixture.SourceSnapshot(), [Fixture.Row(rowId)]);

        // Act
        var result = await fixture.RecordFactsHandler().HandleAsync(Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("collecting", saved.Current!.Status);
        var source = Assert.Single(saved.Current.SourceSnapshots);
        Assert.Equal("approved-change-register", source.SourceReference);
        Assert.Equal(Hash('d'), source.ContentSha256);
        Assert.Equal(1, source.ExpectedTotal);
        Assert.True(source.CollectionComplete);
        Assert.Equal("All approved changes are included.", source.CompletenessEvidence);
        var row = Assert.Single(saved.Current.Rows);
        Assert.Equal(rowId, row.RowId);
        Assert.Equal(source.SnapshotId, row.SourceSnapshotId);
        Assert.Equal("change-17", row.SourceRowIdentity);
        Assert.Equal("included", row.Classification);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), row.RecordedByMemberId);
    }

    [Fact]
    public async Task ShouldRejectStalePopulationRevisionGivenSourceFacts()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, _) = await fixture.SeedDefinedPopulationWithCloseAsync();
        var request = new RecordAuditPopulationFacts(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            populationId, 0, Fixture.SourceSnapshot(), [Fixture.Row(Uuid.CreateVersion4())]);

        // Act
        var result = await fixture.RecordFactsHandler().HandleAsync(Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(saved.Current!.SourceSnapshots);
        Assert.Empty(saved.Current.Rows);
        Assert.Equal(1, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectUnauthorizedPopulationFieldsGivenManualSourceRows()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, _) = await fixture.SeedDefinedPopulationWithCloseAsync();
        var row = Fixture.Row(Uuid.CreateVersion4()) with
        {
            AuthorizedFields = new Dictionary<string, string> { ["secret"] = "do-not-record" },
        };
        var request = new RecordAuditPopulationFacts(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            populationId, 1, Fixture.SourceSnapshot(), [row]);

        // Act
        var result = await fixture.RecordFactsHandler().HandleAsync(Fixture.Context(
            fixture.UserId, request), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(saved.Current!.Rows);
        Assert.Equal(1, saved.Revision);
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
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("collecting", saved.Current!.Status);
        Assert.Equal("population-generation-1", saved.Current.GenerationKey);
        Assert.Empty(saved.Current.Rows);
    }

    [Fact]
    public async Task ShouldAmendFrozenPopulationWithNewSnapshotAndChangedRowsGivenReason()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, rowId) = await fixture.SeedPopulationAsync(reconciled: true);
        var freeze = await fixture.FreezeHandler().HandleAsync(Fixture.Context(fixture.UserId,
            new FreezeAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, populationId, 3,
                "The exact source rows and reconciliation were reviewed.")), CancellationToken.None);
        Assert.True(freeze.IsSuccess, freeze.Error?.Message);
        var sourceSnapshot = new AuditPopulationSourceSnapshotContent(Uuid.CreateVersion4(), 2,
            "approved-change-register-amended", Hash('e'), 1, true,
            "The corrected source export contains the complete population.", Now.AddMinutes(3));
        var changedRow = Fixture.Row(rowId) with
        {
            Classification = "excluded",
            ClassificationRationale = "The corrected source confirms the change was outside the period.",
        };
        var request = new AmendAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, populationId,
            4, "Correct the source classification", sourceSnapshot, [changedRow]);

        // Act
        var result = await fixture.AmendHandler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("amending", saved.Current!.Status);
        var amendment = Assert.Single(saved.Current.Amendments);
        Assert.Equal(1, amendment.SupersededVersion);
        Assert.Equal(2, amendment.SuccessorVersion);
        Assert.Equal("Correct the source classification", amendment.Reason);
        Assert.Equal(rowId, Assert.Single(amendment.ChangedRowIds));
        Assert.Equal(2, saved.Current.SourceSnapshots.Count);
        Assert.Equal("excluded", Assert.Single(saved.Current.Rows).Classification);
        Assert.Equal(sourceSnapshot.SnapshotId, Assert.Single(saved.Current.Rows).SourceSnapshotId);
    }

    [Fact]
    public async Task ShouldRejectPopulationAmendmentGivenReconciledPopulationBeforeFreeze()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, rowId) = await fixture.SeedPopulationAsync(reconciled: true);
        var sourceSnapshot = new AuditPopulationSourceSnapshotContent(Uuid.CreateVersion4(), 2,
            "approved-change-register-amended", Hash('e'), 1, true,
            "Corrected complete source.", Now.AddMinutes(3));
        var request = new AmendAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, populationId,
            3, "Attempt early amendment", sourceSnapshot, [Fixture.Row(rowId)]);

        // Act
        var result = await fixture.AmendHandler().HandleAsync(Fixture.Context(fixture.UserId,
            request), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(saved.Current!.Amendments);
        Assert.Single(saved.Current.SourceSnapshots);
        Assert.Equal(3, saved.Revision);
    }

    [Fact]
    public async Task ShouldReconcileExactStoredRowClassificationsGivenRationale()
    {
        // Arrange
        await using var fixture = new Fixture();
        var (populationId, _) = await fixture.SeedPopulationAsync(reconciled: false);

        // Act
        var result = await fixture.ReconcileHandler().HandleAsync(Fixture.Context(fixture.UserId,
            new ReconcileAuditPopulation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, populationId, 2,
                "Every row was compared with the approved source export.")), CancellationToken.None);
        var saved = (await fixture.Populations.ReadAsync(fixture.TenantId, fixture.ProgramId, populationId)).Rules;

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("reconciled", saved.Current!.Status);
        Assert.Equal("complete", saved.Current.Reconciliation!.Completeness);
        Assert.Equal(1, saved.Current.Reconciliation.SourceTotal);
        Assert.Equal(1, saved.Current.Reconciliation.IncludedCount);
        Assert.Equal("Every row was compared with the approved source export.",
            saved.Current.Reconciliation.ReconciliationRationale);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly DefineAuditPopulationHandlerTests.Fixture _canonical = new();
        public Uuid TenantId => _canonical.TenantId;
        public Uuid ProgramId => _canonical.ProgramId;
        public Uuid UserId => _canonical.UserId;
        public Uuid ControlId => _canonical.ControlId;
        public Bdgrz.Compliance.Features.AuditPopulations.Readers.AuditPopulationReader Populations => _canonical.Populations;
        public IAggregateReader Reader => _canonical.Reader;
        public IAggregateWriter Writer => _canonical.Writer;
        public IAggregateExecutor Executor => _canonical.Executor;
        public ClassifyAuditPopulationRowHandler ClassifyHandler() => new(_canonical.Mutations);
        public FreezeAuditPopulationHandler FreezeHandler() => new(_canonical.Mutations);
        public ReviseAuditPopulationDefinitionHandler ReviseDefinitionHandler() => new(_canonical.Mutations);
        public GenerateAuditPopulationHandler GenerateHandler() => new(_canonical.Mutations, _canonical.Clock);
        public ReconcileAuditPopulationHandler ReconcileHandler() => new(_canonical.Mutations);
        public RecordAuditPopulationFactsHandler RecordFactsHandler() => new(_canonical.Mutations);
        public AmendAuditPopulationHandler AmendHandler() => new(_canonical.Mutations);

        public async Task<(Uuid PopulationId, Uuid RowId)> SeedPopulationAsync(bool reconciled)
        {
            var (populationId, _) = await SeedDefinedPopulationWithCloseAsync();
            var complete = await Populations.ReadAsync(TenantId, ProgramId, populationId);
            var population = complete.Rules;
            var rowId = Uuid.CreateVersion4();
            var actor = RbacIds.Member(TenantId, UserId);
            Assert.Null(population.RecordFacts(population.Revision, SourceSnapshot(), [Row(rowId)], actor,
                "Compliance lead", _canonical.Clock.GetUtcNow().AddMinutes(-4)));
            if (reconciled)
                Assert.Null(population.Reconcile(population.Revision,
                    new AuditPopulationReconciliationContent(1, 1, 0, 0, 0, 0, 0, 0, 0, 0, "Every source row is accounted for."),
                    actor, "Compliance lead", _canonical.Clock.GetUtcNow().AddMinutes(-3)));
            await AuditPopulationSeeds.AppendLegacyAsync(_canonical.PeriodEvents, population, complete.Owner.CommittedStreamPosition);
            return (populationId, rowId);
        }
        public async Task<Uuid> SeedDefinedPopulationAsync() => (await SeedDefinedPopulationWithCloseAsync()).PopulationId;
        public async Task<(Uuid PopulationId, Uuid CloseSnapshotId)> SeedDefinedPopulationWithCloseAsync()
        {
            var closeId = await _canonical.SeedClosedPeriodAsync();
            var actual = await _canonical.Periods.ReadAsync(TenantId, ProgramId, _canonical.PeriodId);
            var populationId = Uuid.CreateVersion4();
            var population = new AuditPopulationAggregate(TenantId, ProgramId, populationId);
            var definition = _canonical.Definition() with { AuthorizedFields = ["control"] };
            Assert.Null(population.Define(populationId, definition, actual.View!, RbacIds.Member(TenantId, UserId),
                "Compliance lead", _canonical.Clock.GetUtcNow().AddMinutes(-5)));
            await AuditPopulationSeeds.AppendLegacyAsync(_canonical.PeriodEvents, population);
            return (populationId, closeId);
        }
        public static AuditPopulationSourceSnapshotContent SourceSnapshot() => new(Uuid.CreateVersion4(), 1,
            "approved-change-register", Hash('d'), 1, true, "All approved changes are included.", Now.AddMinutes(1));
        public static AuditPopulationRowContent Row(Uuid rowId) => new(rowId, "change-17",
            "approved-change-register", "included", "The change falls in the observation period.",
            new Dictionary<string, string> { ["control"] = "AC-1" }, [], []);
        public static Cntryl.Portia.RequestContext<T> Context<T>(Uuid userId, T request) where T : IRequestBase =>
            new(request, new RequestDispatchContext(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()), new Claim("email", "compliance-lead@example.test")],
                "BdgrzSession")), new HttpInvocation("POST", "/api/v1/audit-populations/command", "/api/v1/audit-populations/command", "audit-population-test")));
        public ValueTask DisposeAsync() => _canonical.DisposeAsync();
    }
}
