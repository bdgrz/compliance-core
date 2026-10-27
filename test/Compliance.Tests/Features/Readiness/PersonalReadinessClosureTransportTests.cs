using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Remediation;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class PersonalReadinessClosureTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseReadinessSignOffGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync();
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ProgramReadiness(source.TenantId, source.ProgramId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new DecideReadiness(Uuid.CreateVersion4(), source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "Keep remediating the retained gaps."),
            Context(source.DeciderUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ProgramReadiness(source.TenantId, source.ProgramId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(after.DecisionOf(assessment.AssessmentId));
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldRefuseFindingVerificationGivenNonHttpInvocation(string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new FindingAggregate(source.TenantId, source.ProgramId, finding.FindingId));
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            source.Close(finding), Context(source.ApproverUserId, transport), CancellationToken.None);
        var after = await ProgramManagementServices.HydrateAsync(provider,
            new FindingAggregate(source.TenantId, source.ProgramId, finding.FindingId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(after.Closed);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task ShouldRetainAttributedReadinessSignOffGivenNativeHttp()
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync();
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await DecideHttpAsync(provider, source.DeciderUserId,
            new DecideReadiness(Uuid.CreateVersion4(), source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "Continue remediation."));
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ProgramReadiness(source.TenantId, source.ProgramId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(result.Value, await scope.ServiceProvider.GetRequiredService<ReadinessReader>()
            .ReadDecisionAsync(source.TenantId, retained, assessment.AssessmentId));
        Assert.Equal(RbacIds.Member(source.TenantId, source.DeciderUserId), result.Value.DeciderMemberId);
    }

    [Fact]
    public async Task ShouldRetainAttributedFindingClosureGivenNativeHttp()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);
        await using var scope = provider.CreateAsyncScope();

        var request = source.Close(finding);

        // Act
        var accepted = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(source.ApproverUserId, "http"), CancellationToken.None);
        var run = await FindingProcesses.RunClosureAsync(provider, source.TenantId, source.ProgramId, finding.FindingId,
            request.CommandId);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new FindingAggregate(source.TenantId, source.ProgramId, finding.FindingId));
        var view = await scope.ServiceProvider.GetRequiredService<FindingReader>().ReadViewAsync(source.TenantId,
            source.ProgramId, finding.FindingId, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.Equal("closed", view!.Status);
        Assert.Equal(source.ApproverMemberId, view.Closure!.CloserMemberId);
        var expectedClosure = Assert.IsType<FindingClosureView>(view.Closure);
        var retainedClosure = await scope.ServiceProvider.GetRequiredService<IImmutableContentStore>()
            .ReadJsonAsync<FindingClosureView>(source.TenantId.ToString(), retained.Closed!.ClosureContent);
        Assert.Equal(expectedClosure.DecisionId, retainedClosure.DecisionId);
        Assert.Equal(expectedClosure.VerificationRationale,
            retainedClosure.VerificationRationale);
        Assert.Equal(expectedClosure.ResolutionEvidence.ToArray(),
            retainedClosure.ResolutionEvidence.ToArray());
        Assert.Equal(expectedClosure.Rationale, retainedClosure.Rationale);
        Assert.Equal(expectedClosure.CloserMemberId, retainedClosure.CloserMemberId);
        Assert.Equal(expectedClosure.ClosedBy, retainedClosure.ClosedBy);
        Assert.Equal(expectedClosure.ClosedAt, retainedClosure.ClosedAt);
        Assert.Equal(expectedClosure.SeparationOfDutiesWaiverId,
            retainedClosure.SeparationOfDutiesWaiverId);
    }

    [Fact]
    public async Task ShouldPreserveRunnerSeparationGivenNativeHttpReadiness()
    {
        // Arrange
        var source = await ReadinessAssessmentHandlerTests.Fixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var assessment = await source.RunAsync();
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);

        // Act
        var result = await DecideHttpAsync(provider, source.RunnerUserId,
            new DecideReadiness(Uuid.CreateVersion4(), source.TenantId, source.ProgramId, assessment.AssessmentId,
                assessment.Revision, "do_not_proceed", "I ran the assessment."), RequestErrorKind.Forbidden);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ProgramReadiness(source.TenantId, source.ProgramId));

        // Assert
        Assert.Contains("ran an assessment", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(retained.DecisionOf(assessment.AssessmentId));
    }

    [Fact]
    public async Task ShouldPreserveOwnerSeparationGivenNativeHttpClosure()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        var finding = await FindingTests.RaiseWithCompletedActionAsync(source, source.LeadMemberId);
        await using var provider = await ComposeAsync(source.Provider, source.TenantId, source.ProgramId);

        // Act
        var result = await CloseHttpAsync(provider, source.LeadUserId, source.Close(finding), RequestErrorKind.Forbidden);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new FindingAggregate(source.TenantId, source.ProgramId, finding.FindingId));

        // Assert
        Assert.Contains("owner cannot verify and close their own remediation", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(retained.Closed);
        Assert.Null(retained.PendingClosure);
    }

    internal static async Task<Result<OperationAcceptedResponse>> CloseHttpAsync(IServiceProvider provider, Uuid actor,
        CloseFinding request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    internal static async Task<Result<ReadinessDecisionView>> DecideHttpAsync(IServiceProvider provider, Uuid actor,
        DecideReadiness request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
        {
            Assert.Equal(error, result.Error?.Kind);
            return Result<ReadinessDecisionView>.Failure(result.Error!);
        }
        Assert.True(result.IsSuccess, result.Error?.Message);
        var process = await ReadinessProcesses.RunDecisionAsync(provider, request.TenantId, request.ProgramId,
            request.CommandId);
        Assert.Equal(ProcessState.Completed, process.State);
        var readiness = scope.ServiceProvider.GetRequiredService<ReadinessReader>();
        var program = await readiness.ReadProgramAsync(request.TenantId, request.ProgramId);
        var view = await readiness.ReadDecisionAsync(request.TenantId, program, request.AssessmentId);
        return Result<ReadinessDecisionView>.Success(Assert.IsType<ReadinessDecisionView>(view));
    }

    internal static RequestDispatchContext Context(Uuid actor, string transport) => new(
        ProgramManagementServices.Actor(actor), transport == "http"
            ? new HttpInvocation("POST", "/synthetic/personal/decision", "/synthetic/personal/decision", "synthetic")
            : transport == "mcp" ? new McpInvocation("synthetic.personal.decision") : new DirectInvocation());

    internal static async Task<ServiceProvider> ComposeAsync(ServiceProvider source, Uuid tenant, Uuid programId,
        IPermissionAuthorizer? permissions = null)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var store = source.GetRequiredService<IEventStore>();
        services.AddSingleton(store);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)store);
        services.AddScoped<ISourceFactResolver>(provider =>
            new EventStoreSourceFactResolver(provider.GetRequiredService<IDomainEventReader>()));
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton(source.GetRequiredService<IImmutableContentStore>());
        permissions ??= new RecordingPermissionAuthorizer(allowed: true);
        services.AddSingleton(permissions);
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(permissions));
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await ProgramManagementServices.SeedAsync(provider, new ProgramRules(tenant, programId), program =>
        {
            if (!program.IsCreated)
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    Uuid.CreateVersion4(), "Client manager", DateTimeOffset.UtcNow));
            return Result.Success;
        });
        return provider;
    }
}
