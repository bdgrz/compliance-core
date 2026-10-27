using Bdgrz.Compliance.Features.Applications.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Queries;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReadTests
{
    [Theory]
    [InlineData("cancel", "canceling")]
    [InlineData("correlate", "preview_ready")]
    [InlineData("accept", "accepting")]
    public async Task ShouldReportLagAndRecoverGivenTransitionOnBatch(string transition, string expectedState)
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync("partial");
        var row = (await fixture.RowsOfAsync(batchId)).Single();
        var result = transition switch
        {
            "cancel" => (await fixture.SendPersonalAcceptedAsync(new CancelApplicationImport(Uuid.CreateVersion4(),
                fixture.Tenant, batchId, 1, "Superseded"))).IsSuccess,
            "correlate" => (await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
                fixture.Tenant, batchId, row.RowId, 1, "create_new", null, null, "Reviewed"))).IsSuccess,
            _ => await AcceptAfterCorrelationAsync(fixture, batchId, row.RowId),
        };
        var revision = (await fixture.Reader.HydrateAsync(new ApplicationImportBatch(fixture.Tenant, batchId))).Revision;

        // Act
        var lagging = await fixture.Bus.SendAsync(new GetApplicationImport(fixture.Tenant, batchId, revision),
            fixture.Actor);
        await fixture.CatchUpImportDirectoryAsync();
        var recovered = await fixture.Bus.SendAsync(new GetApplicationImport(fixture.Tenant, batchId, revision),
            fixture.Actor);

        // Assert
        Assert.True(result);
        Assert.Equal(RequestErrorKind.Conflict, lagging.Error?.Kind);
        Assert.True(lagging.Error!.IsTransient);
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(revision, recovered.Value!.Revision);
        Assert.Equal(expectedState, recovered.Value.State);
    }

    [Fact]
    public async Task ShouldShowAttributedChoiceGivenFreshPreviewWithoutPublishedClaimForRow()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync("partial");
        var row = (await fixture.RowsOfAsync(batchId)).Single();
        Assert.True((await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, row.RowId, 1, "create_new", null, null, "Reviewed identity"))).IsSuccess);
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImport(fixture.Tenant, batchId,
            MinimumRevision: 2), fixture.Actor);

        // Assert
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        var item = Assert.Single(preview.Value!.Items);
        var choice = Assert.IsType<ApplicationImportCorrelationView>(item.Correlation);
        Assert.Equal("create_new", choice.Decision);
        Assert.Equal("Reviewed identity", choice.Reason);
        Assert.Equal(2, choice.Revision);
        Assert.Equal("new", item.MatchState);
        Assert.Empty(item.AcceptanceBlockers);
    }

    [Fact]
    public async Task ShouldClassifyObservationGivenPublishedClaimAndChangedGovernance()
    {
        // Arrange: record "omitted" is claimed by the seeded application; a new batch observes it again.
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (_, _, applicationId) = await fixture.SeedAsync();
        var same = await StageAsync(fixture, new ApplicationImportInputRow("omitted", "Observed application",
            "Purpose", null));
        var changed = await StageAsync(fixture, new ApplicationImportInputRow("omitted", "Renamed", "New purpose",
            "Owner"));
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var unchanged = await fixture.Bus.SendAsync(new PreviewApplicationImport(fixture.Tenant, same), fixture.Actor);
        var differs = await fixture.Bus.SendAsync(new PreviewApplicationImport(fixture.Tenant, changed), fixture.Actor);

        // Assert
        var sameRow = Assert.Single(unchanged.Value!.Items);
        Assert.Equal("unchanged", sameRow.MatchState);
        Assert.Equal([applicationId], sameRow.CandidateApplicationIds);
        Assert.Empty(sameRow.ChangedFields);
        var changedRow = Assert.Single(differs.Value!.Items);
        Assert.Equal("changed", changedRow.MatchState);
        Assert.Equal(["name", "purpose", "owner_reference"], changedRow.ChangedFields);
    }

    [Fact]
    public async Task ShouldBlockPreviewGivenCanceledBatchAndRejectedReportShowCancellation()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync();
        Assert.True((await fixture.SendPersonalAcceptedAsync(new CancelApplicationImport(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, 1, "Superseded"))).IsSuccess);
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImport(fixture.Tenant, batchId), fixture.Actor);
        var report = await fixture.Bus.SendAsync(new GetApplicationImportRejectedReport(fixture.Tenant, batchId),
            fixture.Actor);
        var missing = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, batchId),
            fixture.Actor);

        // Assert
        Assert.Contains("batch_canceled", Assert.Single(preview.Value!.Items).AcceptanceBlockers);
        Assert.Equal("canceling", report.Value!.State);
        Assert.Contains("batch_canceled", Assert.Single(report.Value.Rows).Findings);
        Assert.Contains("batch_canceled", Assert.Single(missing.Value!.Items).AcceptanceBlockers);
    }

    [Fact]
    public async Task ShouldReportDurableProgressGivenAcceptedThenPublishedBatch()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync("partial");
        var row = (await fixture.RowsOfAsync(batchId)).Single();
        Assert.True(await AcceptAfterCorrelationAsync(fixture, batchId, row.RowId));

        // Act
        var accepting = await fixture.Bus.SendAsync(new GetApplicationImportProgress(fixture.Tenant, batchId),
            fixture.Actor);
        var run = await ApplicationImportProcessSupport.RunAsync(fixture.Provider,
            fixture.Provider.CreateScope().ServiceProvider, fixture.Tenant, batchId);
        var committed = await fixture.Bus.SendAsync(new GetApplicationImportProgress(fixture.Tenant, batchId),
            fixture.Actor);

        // Assert
        Assert.True(accepting.IsSuccess, accepting.Error?.Message);
        Assert.Equal("accepting", accepting.Value!.State);
        Assert.Equal(1, accepting.Value.PlannedCount);
        Assert.Equal(0, accepting.Value.DurableEffectCount);
        Assert.NotNull(accepting.Value.AcceptedAt);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal("committed", committed.Value!.State);
        Assert.Equal(1, committed.Value.DurableEffectCount);
        Assert.Equal(1, committed.Value.AppliedCount);
        Assert.NotNull(committed.Value.CommittedAt);
    }

    [Fact]
    public async Task ShouldReportValidationFindingsGivenRejectedStagedRow()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        await fixture.SeedAsync();
        var batchId = await StageAsync(fixture, new ApplicationImportInputRow("", "", "", null),
            new ApplicationImportInputRow("fine", "Fine", "Purpose", null));
        await fixture.CatchUpImportDirectoryAsync();

        // Act
        var report = await fixture.Bus.SendAsync(new GetApplicationImportRejectedReport(fixture.Tenant, batchId),
            fixture.Actor);

        // Assert: the invalid row lists its findings and the uncorrelated row still needs a decision.
        Assert.Equal("preview_ready", report.Value!.State);
        var invalid = report.Value.Rows.Single(item => item.RowNumber == 1);
        Assert.Equal(["source_record_id_required", "name_required", "purpose_required"], invalid.Findings);
        Assert.Equal(["correlation_required"], report.Value.Rows.Single(item => item.RowNumber == 2).Findings);
        Assert.Empty(report.Value.BatchFindings);
    }

    [Fact]
    public async Task ShouldRejectLifecycleReadsGivenWrongTenantOrUnsatisfiedRevision()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync();

        // Act
        var wrongTenant = await fixture.Bus.SendAsync(new GetApplicationImportProgress(Uuid.CreateVersion4(), batchId),
            fixture.Actor);
        var ahead = await fixture.Bus.SendAsync(new GetApplicationImportProgress(fixture.Tenant, batchId, 99),
            fixture.Actor);
        var unknown = await fixture.Bus.SendAsync(new GetApplicationImportRejectedReport(fixture.Tenant,
            Uuid.CreateVersion4()), fixture.Actor);
        var invalid = await fixture.Bus.SendAsync(new GetApplicationImport(fixture.Tenant, batchId, 0), fixture.Actor);

        // Assert
        Assert.False(wrongTenant.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, ahead.Error?.Kind);
        Assert.True(ahead.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, invalid.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReadFrozenOmissionProposalGivenSealedProposalAndHideItAfterChange()
    {
        // Arrange
        await using var fixture = new ApplicationImportCompositionTests.Fixture();
        var (batchId, _, _) = await fixture.SeedAsync();
        var batch = await fixture.Reader.HydrateAsync(new ApplicationImportBatch(fixture.Tenant, batchId));
        var source = await fixture.Reader.HydrateAsync(new ApplicationImportSource(fixture.Tenant, "manual",
            "applications"));
        var preview = await fixture.Bus.SendAsync(new PreviewApplicationImportOmissionProposal(fixture.Tenant, batchId),
            fixture.Actor);

        // Act
        var frozen = await fixture.SendPersonalAsync(new FreezeApplicationImportOmissionProposal(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, 1, source.CommittedStreamPosition, batch.ContentDigest!, "Reviewed omission"));
        var read = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(fixture.Tenant, batchId),
            fixture.Actor);
        var row = (await fixture.RowsOfAsync(batchId)).Single();
        Assert.True((await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, row.RowId, 2, "create_new", null, null, "Reviewed"))).IsSuccess);
        var hidden = await fixture.Bus.SendAsync(new GetApplicationImportOmissionProposal(fixture.Tenant, batchId),
            fixture.Actor);

        // Assert
        Assert.Equal(["omitted"], preview.Value!.MissingRows.Select(missing => missing.SourceRecordId));
        Assert.True(frozen.IsSuccess, frozen.Error?.Message);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("Reviewed omission", read.Value!.Reason);
        Assert.Equal(["present"], read.Value.PresentSourceRecordIds);
        Assert.Equal(2, read.Value.Revision);
        Assert.Equal(source.CommittedStreamPosition, read.Value.SourcePosition);
        Assert.Equal(RequestErrorKind.Conflict, hidden.Error?.Kind);
    }

    static async Task<bool> AcceptAfterCorrelationAsync(ApplicationImportCompositionTests.Fixture fixture, Uuid batchId,
        Uuid rowId)
    {
        var correlated = await fixture.SendPersonalAsync(new CorrelateApplicationImportRow(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, rowId, 1, "create_new", null, null, "Reviewed"));
        if (!correlated.IsSuccess)
            return false;
        var accepted = await fixture.SendPersonalAcceptedAsync(new AcceptApplicationImport(Uuid.CreateVersion4(),
            fixture.Tenant, batchId, 2));
        return accepted.IsSuccess;
    }

    static async Task<Uuid> StageAsync(ApplicationImportCompositionTests.Fixture fixture,
        params ApplicationImportInputRow[] rows)
    {
        var staged = await fixture.Bus.SendAsync(new StageApplicationImport(fixture.Tenant, Uuid.CreateVersion4(),
            "manual", "applications", "partial", rows), fixture.Actor);
        Assert.True(staged.IsSuccess, staged.Error?.Message);
        return staged.Value!.BatchId;
    }
}
