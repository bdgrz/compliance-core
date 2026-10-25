using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldSelectCriteriaByBrokerReceiptGivenSelectionPayloadTimeDisagrees(bool lateBrokerReceipt)
    {
        // Arrange
        var cutoff = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
        var first = Edition(Uuid.CreateVersion4());
        var second = Edition(Uuid.CreateVersion4(), "later edition");
        var program = await Program(first.EditionId, cutoff.AddDays(-20),
            (second.EditionId, lateBrokerReceipt ? cutoff.AddDays(-1) : cutoff.AddDays(1)));
        var events = new InMemoryEventStore();
        await events.AppendAsync(program.Stream, 0, program.Facts);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()),
            EventCursor.Start, CancellationToken.None))
            receipts.Record(record, lateBrokerReceipt && record.Event is ProgramCriteriaEditionSelected selected && selected.EditionId == second.EditionId
                ? cutoff.AddMilliseconds(1) : cutoff);
        var contributor = Contributor(new CriteriaProgramReader(program), Catalog(lateBrokerReceipt ? first : second));
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [])
        {
            RecordedThrough = cutoff,
            ReceiptCut = new RecordedSourceReceiptCut(TenantId, cutoff, events, receipts),
        };

        // Act
        var result = await contributor.ReadAsync(Engagement(), context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Empty(result.Value.Blockers);
        Assert.Equal(lateBrokerReceipt ? first.EditionId : second.EditionId,
            Assert.Single(result.Value.Sources, source => source.Kind == "criteria_selection").VersionId);
        Assert.True(receipts.Reads > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainHistoricalLicensedBodyAndCurrentUsageLimitsGivenLaterOverlayReceipt(bool currentAllowsDisplay)
    {
        // Arrange
        var cutoff = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
        var edition = Edition(Uuid.CreateVersion4());
        var catalog = Catalog(edition);
        var original = catalog.Entries[0];
        var program = await Program(edition.EditionId, cutoff.AddDays(-20));
        var events = new InMemoryEventStore();
        await events.AppendAsync(program.Stream, 0, program.Facts);
        var store = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var overlay = new CriteriaTextOverlay(TenantId, edition.EditionId, original.Identifier);
        var first = new CriteriaTextOverlayContent("Original licensed narrative", "Supplier", "License", new(true, true));
        var second = new CriteriaTextOverlayContent("Later licensed narrative", "Supplier", "License", new(currentAllowsDisplay, true));
        var firstBody = await store.WriteJsonAsync(TenantId.ToString(), Uuid.CreateVersion4(), CriteriaTextOverlay.ContentSchema, first);
        var secondBody = await store.WriteJsonAsync(TenantId.ToString(), Uuid.CreateVersion4(), CriteriaTextOverlay.ContentSchema, second);
        var firstOperation = NativeFacts.Root(overlay, Uuid.CreateVersion4(), "SetCriteriaTextOverlay", 0, MemberId, cutoff.AddDays(-10));
        var secondOperation = NativeFacts.Root(overlay, Uuid.CreateVersion4(), "SetCriteriaTextOverlay", 1, MemberId, cutoff.AddDays(-1));
        Assert.True(overlay.Set(firstOperation, firstBody, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        Assert.True(overlay.Set(secondOperation, secondBody, NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        await events.AppendAsync(overlay.Stream, 0, NativeFacts.Raised(overlay));
        var firstRevision = new CriteriaTextOverlayRevision(TenantId, edition.EditionId, original.Identifier, 1,
            first, firstOperation.Actor, firstOperation.AcceptedAt);
        var secondRevision = new CriteriaTextOverlayRevision(TenantId, edition.EditionId, original.Identifier, 2,
            second, secondOperation.Actor, secondOperation.AcceptedAt);
        var current = CriteriaTextOverlayPolicy.Apply(original, secondRevision, secondRevision, false);
        var expectedHistorical = CriteriaTextOverlayPolicy.Apply(original, firstRevision, secondRevision, false);
        var expected = await Contributor(new CriteriaProgramReader(program), catalog, new OverlayReader([expectedHistorical]))
            .ReadAsync(Engagement(), CancellationToken.None);
        var receipts = new ScriptedNativeEventReceipts();
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(TenantId.ToString()),
            EventCursor.Start, CancellationToken.None))
            receipts.Record(record, record.Event is CriteriaTextOverlayEntryRevised { Revision: 2 } ? cutoff.AddMilliseconds(1) : cutoff);
        var contributor = new CriteriaTypeIBaselineSourceContributor(new CriteriaProgramReader(program), events, catalog, new OverlayReader([current]), store);
        var context = new TypeIBaselineSourceReadContext(Uuid.Empty, [])
        {
            RecordedThrough = cutoff,
            ReceiptCut = new RecordedSourceReceiptCut(TenantId, cutoff, events, receipts),
        };

        // Act
        var actual = await contributor.ReadAsync(Engagement(), context, CancellationToken.None);

        // Assert
        Assert.True(actual.IsSuccess, actual.Error?.Message);
        Assert.Equal(Assert.Single(expected.Value.Sources, source => source.Kind == "criteria_edition").ContentSha256,
            Assert.Single(actual.Value.Sources, source => source.Kind == "criteria_edition").ContentSha256);
        Assert.True(receipts.Reads > 0);
    }

    [Fact]
    public async Task ShouldBindCriteriaSelectionAndCatalogEditionGivenTypeIEngagement()
    {
        // Arrange
        var engagement = Engagement();
        var edition = Edition(Uuid.CreateVersion4());
        var program = await Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var catalog = Catalog(edition);
        var contributor = Contributor(new CriteriaProgramReader(program), catalog);

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
        var program = await Program(selected.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero), (later.EditionId, new DateTimeOffset(2026, 10, 1, 12, 0, 0,
                TimeSpan.Zero)));
        var contributor = Contributor(new CriteriaProgramReader(program), Catalog(selected));

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
        var program = new ProgramRules(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", Plan(), MemberId, "Lead", Now));
        var contributor = Contributor(new CriteriaProgramReader(await SourceAsync(program)), Catalog(Edition(Uuid.CreateVersion4())));

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
        var program = await Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var contributor = Contributor(new CriteriaProgramReader(program), Catalog(edition));

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
        var program = await Program(edition.EditionId, new DateTimeOffset(2026, 9, 10, 12, 0, 0,
            TimeSpan.Zero));
        var entry = Assert.Single(Catalog(edition).ListEntries(edition.EditionId, null, null, null));
        var overlay = new CriteriaTextOverlayMetadata(2, "AICPA", "License-1",
            new CriteriaOverlayUsageFlags(true, false), ActorReference.ForMember(MemberId, "Lead"),
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
        var contributor = Contributor(new CriteriaProgramReader(program), Catalog(edition),
            new OverlayReader([entry with { Overlay = overlay, LicensedText = null }]));

        // Act
        var result = await contributor.ReadAsync(engagement, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value.Blockers, blocker => blocker.Contains("historical",
            StringComparison.OrdinalIgnoreCase));
    }

    static CriteriaTypeIBaselineSourceContributor Contributor(CriteriaProgramReader reader,
        ICriteriaCatalog catalog, ICriteriaTextOverlayReader? overlays = null) =>
        new(reader, reader.Events, catalog, overlays ?? new OverlayReader([]));

    static ExaminationEngagementView Engagement() => new(TenantId, ProgramId, Uuid.CreateVersion4(),
        "type_i", "Audit Firm", new DateOnly(2026, 9, 30), null,
        ["security", "availability", "confidentiality", "processing_integrity", "privacy"],
        Uuid.CreateVersion4(), [], "draft", 1, Now, MemberId, "Lead");

    static async Task<ProgramSource> Program(Uuid firstEdition, DateTimeOffset firstSelectedAt,
        params (Uuid EditionId, DateTimeOffset SelectedAt)[] laterSelections)
    {
        var program = new ProgramRules(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", Plan(), MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(1, firstEdition, MemberId, "Lead",
            firstSelectedAt));
        var revision = 2L;
        foreach (var selection in laterSelections)
            Assert.Null(program.SelectCriteriaEdition(revision++, selection.EditionId, MemberId,
                "Lead", selection.SelectedAt));
        return await SourceAsync(program);
    }

    static ProgramPlan Plan() => new(null, null, null, null, null, null);

    static CriteriaCatalogEdition Edition(Uuid id, string label = "2017_tsc_2022_pof") =>
        new(id, "tsc", label, new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), true,
            "All numbered criteria", "https://example.test/criteria", "identifiers_and_original_summaries", []);

    static CriteriaCatalog Catalog(CriteriaCatalogEdition edition) => new(edition,
        [new Criterion(edition.EditionId, "CC1.1", "CC1.1", "security", "criterion", null,
            "Leadership sets ethical expectations.")]);

    static async Task<ProgramSource> SourceAsync(ProgramRules rules)
    {
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var facts = await ProgramSeeds.FactsAsync(content, rules);
        var owner = new ComplianceProgram(rules.TenantId, rules.Id);
        new AggregateScenario<ComplianceProgram>(owner).Given(facts.ToArray());
        var events = new InMemoryEventStore();
        await events.AppendAsync(owner.Stream, 0, facts);
        return new ProgramSource(owner, events, facts);
    }

    sealed record ProgramSource(ComplianceProgram Owner, InMemoryEventStore Events, IReadOnlyList<DomainEvent> Facts)
    {
        public EventStreamAddress Stream => Owner.Stream;
    }

    sealed class CriteriaProgramReader(params ProgramSource[] programs) : IAggregateReader
    {
        public IDomainEventReader Events => programs[0].Events;
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult(programs.FirstOrDefault(program => program.Owner.Id == aggregate.Id)?.Owner
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
