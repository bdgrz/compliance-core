using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessProjectionReadTests
{
    [Fact]
    public async Task ShouldExposeAssessmentOnlyAfterAdoptionGivenAdmittedRun()
    {
        // Arrange
        var fixture = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var provider = fixture.Provider;
        var assessmentId = Uuid.CreateVersion4();
        await fixture.Scenario(fixture.RunnerUserId)
            .When(new RunReadinessAssessment(assessmentId, fixture.TenantId, fixture.ProgramId, 0))
            .ExpectSuccess();
        var before = await fixture.QueryAsync<ListReadinessAssessments, Cntryl.Fitz.Extensions.Page<ReadinessAssessmentSummaryView>>(
            new ListReadinessAssessments(fixture.TenantId, fixture.ProgramId));

        // Act
        var process = await ReadinessProcesses.RunRunAsync(provider, fixture.TenantId, fixture.ProgramId, assessmentId);
        var after = await fixture.QueryAsync<ListReadinessAssessments, Cntryl.Fitz.Extensions.Page<ReadinessAssessmentSummaryView>>(
            new ListReadinessAssessments(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Empty(before.Items);
        Assert.Equal(ProcessState.Completed, process.State);
        Assert.Equal(assessmentId, Assert.Single(after.Items).AssessmentId);
    }
}
