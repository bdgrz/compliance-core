using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPagingTests
{
    [Fact]
    public async Task ShouldShareCursorGivenRowAndPreviewPagingAndRestartAfterLifecycleChange()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var batchId = await StageAsync(fixture, "partial", Enumerable.Range(1, 5).Select(number =>
            new ApplicationImportInputRow($"record-{number}", $"Application {number}", "Purpose", null)).ToArray());
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var rows = await fixture.Bus.SendAsync(new ListApplicationImportRows(fixture.Tenant, batchId, Limit: 2),
            fixture.Actor);
        var previewWithRowsCursor = await fixture.Bus.SendAsync(new PreviewApplicationImport(fixture.Tenant, batchId,
            Limit: 2, Cursor: rows.Value!.NextCursor), fixture.Actor);
        var row = (await fixture.RowsOfAsync(batchId))[0];
        Assert.True((await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, row.RowId, 1, "create_new", null, null, "Reviewed"))).IsSuccess);
        await fixture.CatchUpImportDirectoryAsync();
        var afterChange = await fixture.Bus.SendAsync(new ListApplicationImportRows(fixture.Tenant, batchId, Limit: 2,
            Cursor: rows.Value.NextCursor), fixture.Actor);
        var invalid = await fixture.Bus.SendAsync(new ListApplicationImportRows(fixture.Tenant, batchId, Limit: 2,
            Cursor: "application_import_v1:garbage"), fixture.Actor);

        // Assert
        Assert.Equal(2, rows.Value.Items.Count);
        Assert.Equal([3, 4], previewWithRowsCursor.Value!.Items.Select(item => item.RowNumber));
        Assert.Equal(RequestErrorKind.Conflict, afterChange.Error?.Kind);
        Assert.True(afterChange.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPageAllOmissionsGivenBoundedPreviewAndRejectCursorAfterSourceAdvances()
    {
        // Arrange: five claims are published; a complete-source batch observes none of them.
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var published = await StageAsync(fixture, "partial", Enumerable.Range(1, 5).Select(number =>
            new ApplicationImportInputRow($"claim-{number}", $"Claimed {number}", "Purpose", null)).ToArray());
        var publishedRows = await fixture.RowsOfAsync(published);
        var revision = 1L;
        foreach (var row in publishedRows)
            Assert.True((await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
                fixture.Tenant, published, row.RowId, revision++, "create_new", null, null, "Reviewed"))).IsSuccess);
        Assert.True((await fixture.SendPersonalAcceptedAsync(new AcceptApplicationImport(Uuid.CreateVersion4(),
            fixture.Tenant, published, revision))).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await ApplicationImportProcessSupport.RunAsync(fixture.Provider,
            fixture.Provider.CreateScope().ServiceProvider, fixture.Tenant, published)).State);
        var complete = await StageAsync(fixture, "declared_complete",
            new ApplicationImportInputRow("present", "Present", "Purpose", null));
        await fixture.CatchUpImportDirectoryAsync();
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var first = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, complete,
            Limit: 2), fixture.Actor);
        var second = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, complete,
            Limit: 2, Cursor: first.Value!.NextCursor), fixture.Actor);
        var third = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, complete,
            Limit: 2, Cursor: second.Value!.NextCursor), fixture.Actor);
        var invalidLimit = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, complete,
            Limit: 201), fixture.Actor);
        var invalidCursor = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant,
            complete, Limit: 2, Cursor: "garbage"), fixture.Actor);

        // Assert: every omission appears once in canonical order, then the cursor ends.
        var ids = first.Value.Items.Concat(second.Value.Items).Concat(third.Value!.Items)
            .Select(item => item.SourceRecordId).ToArray();
        Assert.Equal(["claim-1", "claim-2", "claim-3", "claim-4", "claim-5"], ids);
        Assert.Null(third.Value.NextCursor);
        Assert.Equal(RequestErrorKind.Validation, invalidLimit.Error?.Kind);
        Assert.False(invalidCursor.IsSuccess);
    }

    static async Task<Uuid> StageAsync(ApplicationImportCompositionTests.Fixture fixture, string coverage,
        params ApplicationImportInputRow[] rows)
    {
        var staged = await fixture.Bus.SendAsync(new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(),
            "manual", "applications", coverage, rows), fixture.Actor);
        Assert.True(staged.IsSuccess, staged.Error?.Message);
        return staged.Value!.BatchId;
    }
}
