using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class CriteriaTypeIBaselineSourceContributorTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldBindCriteriaSelectionAndCatalogEditionGivenTypeIEngagement()
    {
        // Arrange
        var engagement = Engagement();
        var edition = Edition(Uuid.CreateVersion4());
        var program = Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var catalog = Catalog(edition);
        var contributor = Contributor(new ProgramReader(program), catalog);

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("criteria", contributor.Family);
        var selection = Assert.Single(result.Value.Sources,
            static source => source.Kind == "criteria_selection");
        var sourceEdition = Assert.Single(result.Value.Sources,
            static source => source.Kind == "criteria_edition");
        Assert.Equal(ProgramId, selection.RecordId);
        Assert.Equal(edition.EditionId, selection.VersionId);
        Assert.Equal(2, selection.Revision);
        Assert.Equal(edition.EditionId, sourceEdition.RecordId);
        Assert.Equal(edition.EditionId, sourceEdition.VersionId);
        Assert.Equal(1, sourceEdition.Revision);
        Assert.Empty(result.Value.Blockers);
    }

    [Fact]
    public async Task ShouldUseEditionSelectedOnTypeIDateGivenLaterProgramSelection()
    {
        // Arrange
        var engagement = Engagement();
        var selected = Edition(Uuid.CreateVersion4());
        var later = Edition(Uuid.CreateVersion4(), "later edition");
        var program = Program(selected.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero), (later.EditionId, new DateTimeOffset(2026, 10, 1, 12, 0, 0,
                TimeSpan.Zero)));
        var contributor = Contributor(new ProgramReader(program), Catalog(selected));

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Sources, source => source.Kind == "criteria_edition" &&
            source.RecordId == selected.EditionId);
        Assert.DoesNotContain(result.Value.Sources, source => source.RecordId == later.EditionId);
    }

    [Fact]
    public async Task ShouldBlockGivenNoCriteriaEditionSelectedByTypeIDate()
    {
        // Arrange
        var engagement = Engagement();
        var program = new ComplianceProgram(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", Plan(), MemberId, "Lead", Now));
        var contributor = Contributor(new ProgramReader(program), Catalog(Edition(Uuid.CreateVersion4())));

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Sources);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("criteria edition",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldBlockGivenSelectedCriteriaEditionIsIncomplete()
    {
        // Arrange
        var engagement = Engagement();
        var edition = Edition(Uuid.CreateVersion4()) with { IsComplete = false };
        var program = Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var contributor = Contributor(new ProgramReader(program), Catalog(edition));

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("incomplete",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShouldBlockGivenPriorCriteriaOverlayRevisionCannotBeResolvedAsOfTypeIDate()
    {
        // Arrange
        var engagement = Engagement();
        var edition = Edition(Uuid.CreateVersion4());
        var program = Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var entry = Assert.Single(Catalog(edition).ListEntries(edition.EditionId, null, null, null));
        var overlay = new CriteriaTextOverlayMetadata(2, "AICPA", "License-1",
            new CriteriaOverlayUsageFlags(true, false), ActorReference.ForMember(MemberId, "Lead"),
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(new ProgramReader(program), Catalog(edition),
            new OverlayReader([entry with { Overlay = overlay, LicensedText = null }]));

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("historical",
            StringComparison.OrdinalIgnoreCase));
    }

    static CriteriaTypeIBaselineSourceContributor Contributor(IAggregateReader reader,
        ICriteriaCatalog catalog, ICriteriaTextOverlayReader? overlays = null) =>
        new(reader, catalog, overlays ?? new OverlayReader([]));

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId, Uuid.CreateVersion4(),
        "type_i", "Audit Firm", new DateOnly(2026, 9, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, Now, MemberId, "Lead");

    static ComplianceProgram Program(Uuid firstEdition, DateTimeOffset firstSelectedAt,
        params (Uuid EditionId, DateTimeOffset SelectedAt)[] laterSelections)
    {
        var program = new ComplianceProgram(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", Plan(), MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(1, firstEdition, MemberId, "Lead",
            firstSelectedAt));
        var revision = 2L;
        foreach (var selection in laterSelections)
            Assert.Null(program.SelectCriteriaEdition(revision++, selection.EditionId, MemberId,
                "Lead", selection.SelectedAt));
        return program;
    }

    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    static CriteriaCatalogEdition Edition(Uuid id, string label = "2017_tsc_2022_pof") =>
        new(id, "tsc", label, new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), true,
            "All numbered criteria", "https://example.test/criteria", "identifiers_and_original_summaries", []);

    static CriteriaCatalog Catalog(CriteriaCatalogEdition edition) => new(edition,
        [new Criterion(edition.EditionId, "CC1.1", "CC1.1", "security", "criterion", null,
            "Leadership sets ethical expectations.")]);

    sealed class ProgramReader(params ComplianceProgram[] programs) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(programs.FirstOrDefault(program => program.Id == aggregate.Id)
                is TAggregate found ? found : aggregate);
    }

    sealed class OverlayReader(IReadOnlyList<Criterion> projected) : ICriteriaTextOverlayReader
    {
        public ValueTask<Result<Criterion>> ApplyAsync(Uuid tenantId, Criterion entry,
            bool isExport, CancellationToken ct) =>
            ValueTask.FromResult(Result<Criterion>.Success(projected.FirstOrDefault(item =>
                item.Identifier == entry.Identifier) ?? entry));

        public ValueTask<Result<IReadOnlyList<Criterion>>> ApplyPageAsync(Uuid tenantId,
            IReadOnlyList<Criterion> entries, bool isExport, CancellationToken ct) =>
            ValueTask.FromResult(Result<IReadOnlyList<Criterion>>.Success(entries.Select(entry =>
                projected.FirstOrDefault(item => item.Identifier == entry.Identifier) ?? entry)
                .ToArray()));
    }
}
