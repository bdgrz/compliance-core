using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class PersonalReadinessAnnotationTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldRequirePersonalFeedbackGivenRetainedAssessmentGap(string transport)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync();
        var gap = assessment.Gaps[0];
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessGapPlan(source.TenantId, source.ProgramId, assessment.Gaps[0].GapId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new AnnotateReadinessGap(Uuid.CreateVersion4(), source.TenantId, source.ProgramId, assessment.AssessmentId, gap.GapId, 0, "Consider quarterly review."),
            PersonalReadinessClosureTransportTests.Context(source.DeciderUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ReadinessGapPlan(source.TenantId, source.ProgramId, assessment.Gaps[0].GapId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
            var readiness = scope.ServiceProvider.GetRequiredService<ReadinessReader>();
            var program = await readiness.ReadProgramAsync(source.TenantId, source.ProgramId);
            Assert.Equal(result.Value, Assert.Single((await readiness.ReadAnnotationsAsync(source.TenantId,
                program, assessment.AssessmentId))!));
            Assert.Equal(RbacIds.Member(source.TenantId, source.DeciderUserId), result.Value.AuthorMemberId);
            Assert.Equal(result.Value.AuthorMemberId.ToString(), result.Value.AuthoredBy.Id);
            Assert.Equal(gap.GapId, result.Value.GapId);
            Assert.Equal(assessment.AssessmentId, result.Value.AssessmentId);
            Assert.Null(program.DecisionOf(assessment.AssessmentId));
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }
    internal static async Task<Result<ReadinessAnnotationView>> HttpAsync(IServiceProvider provider, Uuid user,
        AnnotateReadinessGap request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            PersonalReadinessClosureTransportTests.Context(user, "http"), CancellationToken.None);
        if (expectedFailure is { } kind)
            Assert.Equal(kind, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    [Theory]
    [InlineData("gap", RequestErrorKind.NotFound, "The readiness gap was not found.")]
    [InlineData("revision", RequestErrorKind.Conflict, "The readiness gap plan changed. Current revision: 0. Reload it and retry.")]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor may not manage this program.")]
    public async Task ShouldPreserveSourceRefusalGivenNativeHttpFeedback(string fault, RequestErrorKind kind, string message)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync();
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider, source.TenantId, source.ProgramId,
            new RecordingPermissionAuthorizer(fault != "grant"));
        var before = await ProgramManagementServices.HydrateAsync(provider, new ReadinessGapPlan(source.TenantId, source.ProgramId, assessment.Gaps[0].GapId));

        // Act
        var result = await HttpAsync(provider, source.DeciderUserId, new AnnotateReadinessGap(Uuid.CreateVersion4(), source.TenantId, source.ProgramId,
            assessment.AssessmentId, fault == "gap" ? Uuid.CreateVersion4() : assessment.Gaps[0].GapId, fault == "revision" ? 2 : 0, "Consider review."), kind);

        // Assert
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition, (await ProgramManagementServices.HydrateAsync(provider,
            new ReadinessGapPlan(source.TenantId, source.ProgramId, assessment.Gaps[0].GapId))).CommittedStreamPosition);
    }

}
