using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

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
