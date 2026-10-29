using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class PersonalOperatingPlanApprovalTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldEnforcePersonalTransportGivenValidOperatingPlanApproval(string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var proposed = await source.ProposeAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperatingPlan(source.TenantId, source.ProgramId, source.ControlId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveControlOperatingPlan(Uuid.CreateVersion4(), source.TenantId, source.ProgramId,
                source.ControlId, proposed.Revision, proposed.PlanVersionId, "Approve independently."),
            PersonalReadinessClosureTransportTests.Context(source.ApproverUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperatingPlan(source.TenantId, source.ProgramId, source.ControlId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal("approved", result.Value.Status);
            Assert.Equal(source.ApproverMemberId.ToString(), result.Value.ApprovedBy!.Id);
            Assert.True(after.CommittedStreamPosition > before.CommittedStreamPosition);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Equal(proposed.PlanVersionId, after.PendingPlanVersionId);
        }
    }
    [Fact]
    public async Task ShouldPreserveProposerSeparationGivenNativeHttpApproval()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var proposed = await source.ProposeAsync(0);
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOperatingPlan(source.TenantId, source.ProgramId, source.ControlId));

        // Act
        var result = await HttpAsync(provider, source.LeadUserId, new ApproveControlOperatingPlan(Uuid.CreateVersion4(),
            source.TenantId, source.ProgramId, source.ControlId, proposed.Revision, proposed.PlanVersionId, "Self approval."), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOperatingPlan(source.TenantId, source.ProgramId, source.ControlId));

        // Assert
        Assert.Equal("The member who proposed an operating plan cannot approve it.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null) =>
        PersonalOccurrenceProofTransportTests.HttpAsync(provider, actor, request, expectedFailure);

}
