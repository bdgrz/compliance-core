using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
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
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
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
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

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
