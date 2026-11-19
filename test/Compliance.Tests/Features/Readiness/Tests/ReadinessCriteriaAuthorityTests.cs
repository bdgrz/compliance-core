using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Handlers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessCriteriaAuthorityTests
{
    [Fact]
    public async Task ShouldRejectAssessmentWithoutChangingRevisionGivenSelectedEditionIsAbsentFromLegacyCatalog()
    {
        // Arrange
        var fixture = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync(selectedEdition: Uuid.CreateVersion4());
        var revision = await fixture.RevisionAsync();

        // Act
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new RunReadinessAssessment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, revision))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(revision, await fixture.RevisionAsync());
    }

    [Fact]
    public async Task ShouldRequireSourceCoverageGivenRuntimeEditionSelection()
    {
        // Arrange
        var fixture = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        var program = await fixture.Provider.GetRequiredService<ProgramReader>()
            .HistoryAsync(fixture.TenantId, fixture.ProgramId);
        var selected = Assert.Single(program.Facts.OfType<ProgramCriteriaEditionSelected>());
        var runtimeSelection = selected with
        {
            CriteriaPublication = new CriteriaPublicationProof(selected.EditionId, Uuid.CreateVersion4(), 1),
        };
        var runtimeProgram = program with
        {
            Facts = program.Facts.Select(fact => fact == selected ? runtimeSelection : fact).ToArray(),
        };

        // Act
        var requiresCoverage = RunReadinessAssessmentHandler.HasRuntimeCriteriaSelection(runtimeProgram,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.True(requiresCoverage);
        Assert.False(RunReadinessAssessmentHandler.HasRuntimeCriteriaSelection(program, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ShouldRequireSourceCoverageGivenRuntimeEditionAtProgramCreation()
    {
        // Arrange
        var fixture = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync(selectEdition: false);
        var program = await fixture.Provider.GetRequiredService<ProgramReader>()
            .HistoryAsync(fixture.TenantId, fixture.ProgramId);
        var created = Assert.Single(program.Facts.OfType<ProgramCreated>());
        var editionId = Uuid.CreateVersion4();
        var runtimeCreation = created with
        {
            CriteriaEditionId = editionId,
            CriteriaPublication = new CriteriaPublicationProof(editionId, Uuid.CreateVersion4(), 1),
        };
        var runtimeProgram = program with
        {
            Facts = program.Facts.Select(fact => fact == created ? runtimeCreation : fact).ToArray(),
        };

        // Act
        var requiresCoverage = RunReadinessAssessmentHandler.HasRuntimeCriteriaSelection(runtimeProgram,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.True(requiresCoverage);
        Assert.False(RunReadinessAssessmentHandler.HasRuntimeCriteriaSelection(program, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ShouldUseEffectiveSelectionGivenRuntimeCreationAndLaterLegacySelection()
    {
        // Arrange
        var fixture = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        var program = await fixture.Provider.GetRequiredService<ProgramReader>()
            .HistoryAsync(fixture.TenantId, fixture.ProgramId);
        var created = Assert.Single(program.Facts.OfType<ProgramCreated>());
        var editionId = Uuid.CreateVersion4();
        var runtimeCreation = created with
        {
            CriteriaEditionId = editionId,
            CriteriaPublication = new CriteriaPublicationProof(editionId, Uuid.CreateVersion4(), 1),
        };
        var runtimeProgram = program with
        {
            Facts = program.Facts.Select(fact => fact == created ? runtimeCreation : fact).ToArray(),
        };

        // Act
        var requiresCoverage = RunReadinessAssessmentHandler.HasRuntimeCriteriaSelection(runtimeProgram,
            DateTimeOffset.UtcNow);

        // Assert
        Assert.False(requiresCoverage);
    }
}
