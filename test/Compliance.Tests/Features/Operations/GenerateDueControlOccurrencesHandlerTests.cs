using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Operations;

public sealed class GenerateDueControlOccurrencesHandlerTests
{
    [Fact]
    public async Task ShouldSelectCompleteManifestAndReplayExactRetryGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, plan) = await StartAsync(fixture);
        var request = Request(fixture, period, "generation-001");

        // Act
        var first = await Generate(fixture, request);
        var retried = await Generate(fixture, request);
        var changed = await Generate(fixture, request with { ThroughDate = fixture.FirstEnd.AddDays(-1) });
        var selected = Assert.Single(await fixture.Host.EventsAsync<DueControlOccurrencesGenerated>(
            fixture.StreamOf(period)));
        var ids = await fixture.Host.Content.ReadJsonAsync<IReadOnlyList<Uuid>>(fixture.TenantId.ToString(),
            selected.OccurrenceIdsManifest);
        var manifest = await fixture.Host.Content.ReadJsonAsync<ControlOccurrenceSelectionManifest>(
            fixture.TenantId.ToString(), selected.CreationIntentManifest);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(4, ids.Count);
        Assert.Equal(ids, manifest.Selections.Select(selection => selection.OccurrenceId));
        Assert.Equal(ids.OrderBy(static id => id.ToString(), StringComparer.Ordinal), ids);
        var windows = new List<ControlCalendarWindow>();
        foreach (var selection in manifest.Selections)
        {
            var intent = await fixture.Host.Content.ReadJsonAsync<ControlOccurrenceCreationIntent>(
                fixture.TenantId.ToString(), selection.Intent);
            var window = Assert.IsType<ControlCalendarWindow>(intent.Window);
            windows.Add(window);
            Assert.Equal(ControlOccurrenceIdentity.Of(fixture.TenantId, fixture.ProgramId, fixture.ControlId, window),
                intent.OccurrenceId);
            Assert.Equal(plan.PlanVersionId, intent.PlanVersionId);
            Assert.Equal(plan.Owner, intent.Owner);
            Assert.Equal(plan.ReviewerMemberId, intent.ReviewerMemberId);
            Assert.Equal(ControlOccurrenceCreationIntent.RecurringTrigger, intent.Trigger);
            Assert.Equal(selected.Operation.RootOperationId, intent.Header.RootOperationId);
        }

        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1), new DateOnly(2027, 4, 1),
            new DateOnly(2027, 7, 1)], windows.Select(window => window.From).Order());
        Assert.Equal(2, (await fixture.PeriodAsync(period)).Revision);
    }

    [Fact]
    public async Task ShouldSelectOnlyNewlyDueOccurrencesGivenSecondGenerationKey()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);
        var firstQuarter = new GenerateDueControlOccurrences(fixture.TenantId, fixture.ProgramId, period,
            new DateOnly(2026, 12, 31), "generation-q1");

        // Act
        var first = await Generate(fixture, firstQuarter);
        var second = await Generate(fixture, Request(fixture, period, "generation-rest"));
        var selections = new List<int>();
        foreach (var selected in await fixture.Host.EventsAsync<DueControlOccurrencesGenerated>(fixture.StreamOf(period)))
            selections.Add((await fixture.Host.Content.ReadJsonAsync<IReadOnlyList<Uuid>>(fixture.TenantId.ToString(),
                selected.OccurrenceIdsManifest)).Count);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal([1, 3], selections);
    }

    [Fact]
    public async Task ShouldFailClosedGivenDueRecurringControlWithoutApprovedPlan()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var period = await fixture.StartPeriodAsync();

        // Act
        var result = await Generate(fixture, Request(fixture, period, "generation-001"));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(await fixture.Host.EventsAsync<DueControlOccurrencesGenerated>(fixture.StreamOf(period)));
        Assert.Equal(1, (await fixture.PeriodAsync(period)).Revision);
    }

    [Fact]
    public async Task ShouldRejectGenerationGivenThroughDateOutsidePeriodOrMissingPeriod()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var (period, _) = await StartAsync(fixture);

        // Act
        var outside = await Generate(fixture, Request(fixture, period, "outside") with
        {
            ThroughDate = fixture.FirstEnd.AddDays(1),
        });
        var missing = await Generate(fixture, Request(fixture, Uuid.CreateVersion4(), "missing"));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, outside.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, missing.Error?.Kind);
        Assert.Empty(await fixture.Host.EventsAsync<DueControlOccurrencesGenerated>(fixture.StreamOf(period)));
    }

    static async Task<(Uuid Period, ControlOperatingPlanView Plan)> StartAsync(TypeIIRangeFixture fixture)
    {
        var period = await fixture.StartPeriodAsync();
        return (period, await fixture.SeedOperatingPlanAsync());
    }

    static GenerateDueControlOccurrences Request(TypeIIRangeFixture fixture, Uuid period, string key) =>
        new(fixture.TenantId, fixture.ProgramId, period, fixture.FirstEnd, key);

    static ValueTask<Result<UnitResult>> Generate(TypeIIRangeFixture fixture, GenerateDueControlOccurrences request) =>
        fixture.Host.GenerateHandler(fixture.Clock).HandleAsync(fixture.Context(request), CancellationToken.None);
}
