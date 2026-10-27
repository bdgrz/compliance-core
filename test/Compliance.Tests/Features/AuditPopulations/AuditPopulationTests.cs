using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Readers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using AuditPopulationAggregate = Bdgrz.Compliance.Features.AuditPopulations.AuditPopulationRules;

namespace Bdgrz.Compliance.Tests.Features.AuditPopulations;

public sealed class AuditPopulationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid PopulationId = Uuid.CreateVersion4();
    static readonly Uuid DefinitionId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldDefineAndRecordManuallySuppliedFactsGivenNoExternalIdentity()
    {
        // Arrange
        var population = NewPopulation();
        var rowId = Uuid.CreateVersion4();
        var snapshot = SourceSnapshot(1, "manual-import-2026-10-18", Hash('1'), 1, true);

        // Act
        var defined = population.Define(DefinitionId, Definition(), ClosedPeriod(),
            RecorderId, "Compliance lead", Now);
        var recorded = population.RecordFacts(population.Revision, snapshot,
            [Row(rowId, null, "included", new Dictionary<string, string> { ["control"] = "AC-1" })],
            RecorderId, "Compliance lead", Now.AddMinutes(1));

        // Assert
        Assert.Null(defined);
        Assert.Null(recorded);
        Assert.Equal(rowId, population.Current!.Rows.Single().RowId);
        Assert.Equal(snapshot.SnapshotId, population.Current.Rows.Single().SourceSnapshotId);
        Assert.Null(population.Current.Rows.Single().SourceRowIdentity);
        Assert.Equal("manual-import-2026-10-18", population.Current.SourceSnapshots.Single().SourceReference);
        Assert.Equal("AC-1", population.Current.Rows.Single().AuthorizedFields["control"]);
    }

    [Fact]
    public void ShouldRetainTheExactExaminationAndClosedPeriodGivenPopulationDefinition()
    {
        // Arrange
        var population = NewPopulation();
        var closedPeriod = ClosedPeriod();

        // Act
        var defined = population.Define(DefinitionId, Definition(), closedPeriod,
            RecorderId, "Compliance lead", Now);

        // Assert
        Assert.Null(defined);
        Assert.Equal(closedPeriod.PeriodId, population.Current!.TypeIIPeriodId);
        Assert.Equal(closedPeriod.ExaminationId, population.Current.ExaminationId);
    }

    [Fact]
    public void ShouldRejectUnauthorizedFieldsAndDuplicateStableRowIdentitiesGivenSourceFacts()
    {
        // Arrange
        var population = DefinedPopulation();
        var rowId = Uuid.CreateVersion4();
        var snapshot = SourceSnapshot(1, "access-export", Hash('2'), 1, true);

        // Act
        var unauthorized = population.RecordFacts(population.Revision, snapshot,
            [Row(rowId, "id-1", "included", new Dictionary<string, string>
            {
                ["control"] = "AC-1", ["person_email"] = "person@example.com",
            })], RecorderId, "Compliance lead", Now.AddMinutes(1));
        var duplicated = population.RecordFacts(population.Revision, snapshot,
            [Row(rowId, "id-1", "included"), Row(rowId, "id-2", "included")],
            RecorderId, "Compliance lead", Now.AddMinutes(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(unauthorized).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(duplicated).Code);
        Assert.Empty(population.Current!.Rows);
    }

    [Fact]
    public void ShouldReconcileExactRowClassificationsAndKeepIncompleteSourceIneligibleGivenMissingEvidence()
    {
        // Arrange
        var population = DefinedPopulation();
        var rows = new[]
        {
            Row(Uuid.CreateVersion4(), "row-1", "included"),
            Row(Uuid.CreateVersion4(), "row-2", "excluded"),
            Row(Uuid.CreateVersion4(), "row-3", "missing"),
        };
        Assert.Null(population.RecordFacts(population.Revision,
            SourceSnapshot(3, "tickets-export", Hash('3'), 3, false), rows,
            RecorderId, "Compliance lead", Now.AddMinutes(1)));

        // Act
        var reconciled = population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(3, 1, 1, 0, 1, 0, 0, 0, 0, 0,
                "Source export omitted one matching ticket; recorded as missing."),
            RecorderId, "Compliance lead", Now.AddMinutes(2));

        // Assert
        Assert.Null(reconciled);
        Assert.Equal("incomplete", population.Current!.Reconciliation!.Completeness);
        Assert.Equal(3, population.Current.Reconciliation.SourceTotal);
        Assert.Equal(1, population.Current.Reconciliation.MissingCount);
        Assert.False(population.Current.SampleEligible);
    }

    [Fact]
    public void ShouldFreezePartialCollectionAsExplicitlyIncompleteGivenMissingSourceEvidence()
    {
        // Arrange
        var population = DefinedPopulation();
        Assert.Null(population.RecordFacts(population.Revision,
            SourceSnapshot(1, "manual-register-partial", Hash('6'), 2, false),
            [Row(Uuid.CreateVersion4(), null, "unresolved")], RecorderId,
            "Compliance lead", Now.AddMinutes(1)));
        Assert.Null(population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(1, 0, 0, 0, 0, 0, 1, 0, 0, 0,
                "One source fact is missing and completeness evidence was not supplied."),
            RecorderId, "Compliance lead", Now.AddMinutes(2)));

        // Act
        var frozen = population.Freeze(population.Revision,
            "Preserve the incomplete collection for review.", RecorderId,
            "Compliance lead", Now.AddMinutes(3));

        // Assert
        Assert.Null(frozen);
        Assert.Equal("frozen", population.Current!.Status);
        Assert.Equal("incomplete", population.Current.Current!.Reconciliation.Completeness);
        Assert.False(population.Current.SampleEligible);
        Assert.Equal("unresolved", population.Current.Current.Rows.Single().Classification);
    }

    [Fact]
    public void ShouldFreezeCompletePopulationAndCreateImmutableSuccessorGivenLateCorrection()
    {
        // Arrange
        var population = DefinedPopulation();
        var originalRowId = Uuid.CreateVersion4();
        Assert.Null(population.RecordFacts(population.Revision,
            SourceSnapshot(1, "manual-register", Hash('4'), 1, true, "Evidence supports completeness."),
            [Row(originalRowId, null, "included")], RecorderId, "Compliance lead", Now.AddMinutes(1)));
        Assert.Null(population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                "All source rows accounted for."),
            RecorderId, "Compliance lead", Now.AddMinutes(2)));

        // Act
        var frozen = population.Freeze(population.Revision, "Reviewed complete source reconciliation.",
            RecorderId, "Compliance lead", Now.AddMinutes(3));
        var correctedRow = Row(Uuid.CreateVersion4(), "late-row", "included");
        var amended = population.Amend(population.Revision, "Late source correction.",
            SourceSnapshot(2, "manual-register-amended", Hash('5'), 2, true,
                "Late row reconciled to register."), [correctedRow], RecorderId,
            "Compliance lead", Now.AddMinutes(4));
        var reconciled = population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(2, 2, 0, 0, 0, 0, 0, 0, 0, 0,
                "Original and late source rows accounted for."),
            RecorderId, "Compliance lead", Now.AddMinutes(5));
        var successorFrozen = population.Freeze(population.Revision,
            "Reviewed amended source reconciliation.", RecorderId, "Compliance lead", Now.AddMinutes(6));

        // Assert
        Assert.Null(frozen);
        Assert.Null(amended);
        Assert.Null(reconciled);
        Assert.Null(successorFrozen);
        Assert.Equal("frozen", population.Current!.Status);
        Assert.True(population.Current.SampleEligible);
        Assert.Single(population.Current.Versions[0].Rows);
        Assert.Single(population.Current.Amendments);
        Assert.Equal(2, population.Current.CurrentVersionNumber);
        Assert.Equal(originalRowId, population.Current.Versions[0].Rows[0].RowId);
        Assert.Equal("late-row", population.Current.Rows.Single(row => row.RowId == correctedRow.RowId)
            .SourceRowIdentity);
        Assert.NotEqual(population.Current.Versions[0].ContentSha256,
            population.Current.Versions[^1].ContentSha256);
    }

    [Fact]
    public async Task ShouldReplayExactFrozenPopulationGivenExportIdentity()
    {
        // Arrange
        var population = FrozenPopulation();
        var exportId = Uuid.CreateVersion4();
        var artifactId = Uuid.CreateVersion4();

        // Act
        var exported = population.RecordExport(population.Revision, exportId, 1, "csv",
            "population-v1.csv", artifactId, Hash('6'), 128, RecorderId, "Compliance lead", Now.AddMinutes(4));
        var events = new InMemoryEventStore();
        var content = TypeIIPeriodSeeds.NewContentStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IImmutableContentStore>(content);
        services.AddSingleton<ISourceFactResolver>(new EventStoreSourceFactResolver(events));
        services.AddPortia();
        services.AddVerifiedAuditPopulationReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await AuditPopulationSeeds.AppendLegacyAsync(events, population);
        var complete = await new AuditPopulationReader(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            events, content, new EventStoreSourceFactResolver(events)).ReadAsync(TenantId, ProgramId, PopulationId);
        var replay = complete.Rules;

        // Assert
        Assert.Null(exported);
        Assert.Equal(exportId, replay.Current!.Exports.Single().ExportId);
        Assert.Equal(replay.Current.Current!.ContentSha256, replay.Current.Exports.Single().PopulationContentSha256);
        Assert.Equal(artifactId, replay.Current.Exports.Single().ArtifactId);
        Assert.Equal(Hash('6'), replay.Current.Exports.Single().OutputContentSha256);
        Assert.Equal(128, replay.Current.Exports.Single().OutputContentLength);
        Assert.True(replay.Current.SampleEligible);
    }

    static AuditPopulationAggregate NewPopulation() => new(TenantId, ProgramId, PopulationId);

    static AuditPopulationAggregate DefinedPopulation()
    {
        var population = NewPopulation();
        Assert.Null(population.Define(DefinitionId, Definition(), ClosedPeriod(), RecorderId,
            "Compliance lead", Now));
        return population;
    }

    static AuditPopulationAggregate FrozenPopulation()
    {
        var population = DefinedPopulation();
        Assert.Null(population.RecordFacts(population.Revision,
            SourceSnapshot(1, "manual-register", Hash('4'), 1, true, "Evidence supports completeness."),
            [Row(Uuid.CreateVersion4(), null, "included")], RecorderId,
            "Compliance lead", Now.AddMinutes(1)));
        Assert.Null(population.Reconcile(population.Revision,
            new AuditPopulationReconciliationContent(1, 1, 0, 0, 0, 0, 0, 0, 0, 0,
                "All source rows accounted for."),
            RecorderId, "Compliance lead", Now.AddMinutes(2)));
        Assert.Null(population.Freeze(population.Revision, "Reviewed complete source reconciliation.",
            RecorderId, "Compliance lead", Now.AddMinutes(3)));
        return population;
    }

    static AuditPopulationDefinitionContent Definition() => new("Production changes",
        "All production changes affecting the in-scope payment service.",
        "Approved production-change register", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
        "Include all approved and emergency changes.", "Exclude canceled requests.",
        "Reconcile change IDs and period boundaries to the approved register.",
        ["control"], [Uuid.CreateVersion4()]);

    static TypeIIPeriodView ClosedPeriod() => new(TenantId, ProgramId, Uuid.CreateVersion4(), "closed", 4,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Hash('b'), new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31), Uuid.CreateVersion4(), Hash('c'), [], [], [], [], null, [],
        [CloseSnapshot()], RecorderId, "Compliance lead", Now);

    static TypeIIPeriodCloseSnapshotView CloseSnapshot() => new(Uuid.CreateVersion4(), 1, [], [], [],
        Hash('a'), null, null, [], [], RecorderId, "ceo", "CEO", Now);

    static AuditPopulationSourceSnapshotContent SourceSnapshot(int version, string reference,
        string hash, int expectedTotal, bool complete, string completenessEvidence = "") =>
        new(Uuid.CreateVersion4(), version, reference, hash, expectedTotal, complete,
            completenessEvidence.Length == 0 ? null : completenessEvidence, Now);

    static string Hash(char value) => new(value, 64);

    static AuditPopulationRowContent Row(Uuid rowId, string? sourceIdentity, string classification,
        IReadOnlyDictionary<string, string>? fields = null) => new(rowId, sourceIdentity,
        sourceIdentity is null ? "Manual entry" : "Approved change register", classification,
        classification == "included" ? "Meets population inclusion rule." : "Retained for reconciliation.",
        fields ?? new Dictionary<string, string>(), [], []);
}
