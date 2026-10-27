using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class SampleSelectionCommandHandlerTests
{
    [Fact]
    public async Task ShouldRecordSelectionFromExactFrozenIncludedRowsGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, includedRowId, _, digest) = await fixture.SeedFrozenPopulationAsync();
        var command = RecordCommand(fixture, requestId, populationId, includedRowId);

        // Act
        var result = await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var materialized = await fixture.MaterializeAsync(command.SelectionId);
        var retry = await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var intent = await SelectionAsync(fixture, requestId, command.SelectionId);
        var population = await fixture.ReadAsync<AuditPopulationVersionView>(intent.PopulationContent);
        var rows = await ImmutableSelectionManifests.ReadAsync<Uuid>(fixture.Content, fixture.TenantId.ToString(), intent.PopulationRowIds);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(result.Value, retry.Value);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.Equal(2, (await fixture.RequestAsync(requestId)).Revision);
        Assert.Equal(populationId, intent.PopulationId);
        Assert.Equal(1, intent.PopulationVersion);
        Assert.Equal(digest, population.ContentSha256);
        Assert.Equal("random", intent.SelectionMethod);
        Assert.Equal("external_email", intent.Source);
        Assert.Equal("sample-mail-001", intent.ExternalReference);
        Assert.Equal(includedRowId, Assert.Single(rows));
    }

    [Fact]
    public async Task ShouldRejectExcludedPopulationRowsGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, _, excludedRowId, _) = await fixture.SeedFrozenPopulationAsync();
        var command = RecordCommand(fixture, requestId, populationId, excludedRowId);

        // Act
        var result = await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var request = await fixture.RequestAsync(requestId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty((await fixture.RequestHistoryAsync(requestId)).Selections);
        Assert.Equal(1, request.Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleAuditorRequestRevisionGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, rowId, _, _) = await fixture.SeedFrozenPopulationAsync();
        var command = RecordCommand(fixture, requestId, populationId, rowId) with { ExpectedRequestRevision = 0 };

        // Act
        var result = await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(command), CancellationToken.None);
        var request = await fixture.RequestAsync(requestId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty((await fixture.RequestHistoryAsync(requestId)).Selections);
        Assert.Equal(1, request.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemDispatchGivenSampleSelection()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, rowId, _, _) = await fixture.SeedFrozenPopulationAsync();
        var command = RecordCommand(fixture, requestId, populationId, rowId);

        // Act
        var result = await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(
            new RequestContext<RecordSampleSelection>(command, RequestActor.System), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty((await fixture.RequestHistoryAsync(requestId)).Selections);
    }

    [Fact]
    public async Task ShouldAppendAmendedSelectionFromLaterFrozenVersionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var (populationId, rowId, _, firstDigest) = await fixture.SeedFrozenPopulationAsync();
        var first = RecordCommand(fixture, requestId, populationId, rowId);
        Assert.True((await fixture.Handler<RecordSampleSelectionHandler>().HandleAsync(fixture.Context(first), CancellationToken.None)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await fixture.MaterializeAsync(first.SelectionId)).State);
        var secondDigest = await fixture.AmendPopulationAsync(populationId, rowId);
        var amendment = new AmendSampleSelection(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId, requestId,
            2, Uuid.CreateVersion4(), first.SelectionId, populationId, 2, [rowId], "targeted", "external_portal",
            "portal-export-119", "The auditor selected this row again from the amended population.");

        // Act
        var result = await fixture.Handler<AmendSampleSelectionHandler>().HandleAsync(fixture.Context(amendment), CancellationToken.None);
        var materialized = await fixture.MaterializeAsync(amendment.SelectionId);
        var original = await SelectionAsync(fixture, requestId, first.SelectionId);
        var revised = await SelectionAsync(fixture, requestId, amendment.SelectionId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ProcessState.Completed, materialized.State);
        Assert.Equal(2, (await fixture.RequestHistoryAsync(requestId)).Selections.Count);
        Assert.Equal(1, original.PopulationVersion);
        Assert.Equal(firstDigest, (await fixture.ReadAsync<AuditPopulationVersionView>(original.PopulationContent)).ContentSha256);
        Assert.Null(original.SupersedesSelectionId);
        Assert.Equal(2, revised.PopulationVersion);
        Assert.Equal(secondDigest, (await fixture.ReadAsync<AuditPopulationVersionView>(revised.PopulationContent)).ContentSha256);
        Assert.Equal(first.SelectionId, revised.SupersedesSelectionId);
        Assert.Equal(amendment.AmendmentReason, revised.AmendmentReason);
    }

    internal static RecordSampleSelection RecordCommand(AuditorRequestFixture fixture, Uuid requestId,
        Uuid populationId, Uuid rowId) => new(fixture.TenantId, fixture.ProgramId, fixture.ExaminationId,
        requestId, 1, Uuid.CreateVersion4(), populationId, 1, [rowId], "random", "external_email", "sample-mail-001");

    internal static async Task<AuditorSampleSelectionIntent> SelectionAsync(AuditorRequestFixture fixture,
        Uuid requestId, Uuid selectionId)
    {
        var owner = await fixture.Reader.HydrateAsync(new AuditorSampleSelection(fixture.TenantId, fixture.ProgramId,
            requestId, selectionId));
        Assert.True(owner.IsMaterialized);
        return await fixture.ReadAsync<AuditorSampleSelectionIntent>(owner.Recorded!.Change);
    }
}
