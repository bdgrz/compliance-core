using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
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
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.Controls.Commands;
using Bdgrz.Compliance.Features.Controls.Handlers;
using Bdgrz.Compliance.Features.Controls.Queries;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Controls.Tests;

public sealed class ControlActivationHandlerTests
{
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOnePersonalControlDecisionGivenSameCommandAndChangedIntent(bool approval)
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);
        var commandId = Uuid.CreateVersion4();
        var reviewId = approval ? await fixture.ReviewAsync() : Uuid.Empty;
        IRequest request = approval
            ? new ApproveControl(commandId, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                1, reviewId, EffectiveFrom, "Approved")
            : new ReviewControl(commandId, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                1, "accept", "Reviewed");
        var user = approval ? fixture.ApproverUserId : fixture.ReviewerUserId;
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, user, request);
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));
        IRequest changed = request switch
        {
            ApproveControl command => command with { Rationale = "Changed" },
            ReviewControl command => command with { Rationale = "Changed" },
            _ => throw new InvalidOperationException(),
        };

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, user, request);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, user, changed, RequestErrorKind.Conflict);
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, user,
            new ReviewControl(fixture.ControlId, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                1, "accept", "Creation identity reused"), RequestErrorKind.Conflict);
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));

        // Assert
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
        Assert.Equal(before.ReadDecisions(), after.ReadDecisions());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayAcceptedDecisionGivenEmissionGateLaterDisabled(bool approval)
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);
        var commandId = Uuid.CreateVersion4();
        var reviewId = approval ? await fixture.ReviewAsync() : Uuid.Empty;
        var user = approval ? fixture.ApproverUserId : fixture.ReviewerUserId;
        IRequest request = approval
            ? new ApproveControl(commandId, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                1, reviewId, EffectiveFrom, "Approved")
            : new ReviewControl(commandId, fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                1, "accept", "Reviewed");
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, user, request);
        var before = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var dispatch = new RequestDispatchContext(ProgramManagementServices.Actor(user),
            new HttpInvocation("PUT", "/synthetic/control-decision", null, "gate-replay"));
        var disabled = new ControlActivationReleaseGate(false);

        // Act
        var replay = request switch
        {
            ApproveControl command => await ActivatorUtilities.CreateInstance<ApproveControlHandler>(
                scope.ServiceProvider, disabled).HandleAsync(new Cntryl.Portia.RequestContext<ApproveControl>(
                command, dispatch), CancellationToken.None),
            ReviewControl command => await ActivatorUtilities.CreateInstance<ReviewControlHandler>(
                scope.ServiceProvider, disabled).HandleAsync(new Cntryl.Portia.RequestContext<ReviewControl>(
                command, dispatch), CancellationToken.None),
            _ => throw new InvalidOperationException(),
        };
        var after = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(before.SourcePhysicalPosition, after.SourcePhysicalPosition);
    }

    [Fact]
    public async Task ShouldRejectActivationGivenSuspendedOwnerThenActivateGivenReinstatedOwner()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);
        await fixture.SuspendOwnerAsync();
        var reviewId = await fixture.ReviewAsync();

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(reviewId), RequestErrorKind.Conflict);
        await fixture.ReinstateOwnerAsync();
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, fixture.Approve(reviewId));
        // Assert
        var version = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                EffectiveFrom));
        Assert.Equal(fixture.OwnerMemberId, version.OwnerMemberId);
        var before = await fixture.QueryFailureAsync(new GetEffectiveControlVersion(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, EffectiveFrom.AddDays(-1)));
        Assert.Equal(RequestErrorKind.NotFound, before);
        var decisions = await fixture.QueryAsync<ListControlDecisions, Page<ControlDecisionView>>(
            new ListControlDecisions(fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1));
        Assert.Equal("review", Assert.Single(decisions.Items).Kind);
        Assert.NotNull(decisions.NextCursor);
        var next = await fixture.QueryAsync<ListControlDecisions, Page<ControlDecisionView>>(
            new ListControlDecisions(fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1,
                decisions.NextCursor));
        Assert.Equal("approval", Assert.Single(next.Items).Kind);
        Assert.Null(next.NextCursor);
    }

    [Fact]
    public async Task ShouldRejectReviewGivenDisabledReleaseGate()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: false);

        // Act
        await PersonalControlDecisionTransportTests.HttpAsync(fixture.Provider, fixture.ApproverUserId, new ReviewControl(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1,
                "accept", "Reviewed"), RequestErrorKind.Conflict);
        // Assert
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlRules(fixture.TenantId, fixture.ControlId));
        Assert.Empty(source.ReadDecisions());
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenControlFromAnotherProgram()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);

        // Act
        var error = await fixture.QueryFailureAsync(new GetCurrentControlVersion(
            fixture.TenantId, Uuid.CreateVersion4(), fixture.ControlId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, error);
    }

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-HTTP");
        public Uuid OwnerMemberId => RbacIds.Member(TenantId, OwnerUserId);

        public static async Task<Fixture> CreateAsync(bool activationEnabled)
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<ReviewControlHandler>()
                    .AddRequestHandler<ApproveControlHandler>()
                    .AddRequestHandler<GetCurrentControlVersionHandler>()
                    .AddRequestHandler<GetEffectiveControlVersionHandler>()
                    .AddRequestHandler<ListControlDecisionsHandler>(),
                services => services
                    .AddSingleton(new ControlActivationReleaseGate(activationEnabled))
                    .AddScoped<ControlActivationSource>()
                    .AddSingleton(new ControlLifecycleReleaseGate(false))
                    .AddScoped<ControlImpactService>()
                    .AddSingleton<IControlApplicabilityReferenceValidator, AcceptingValidator>());
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(provider,
                new ControlRules(fixture.TenantId, fixture.ControlId), control => control.Create(
                    fixture.ProgramId, Uuid.CreateVersion4(), "AC-HTTP", new ControlDraftContent(
                        "Access review", "Review access", "Management reviews access",
                        "The security lead reviews access quarterly.", ["Dated review record"]),
                    RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now));
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, fixture.OwnerUserId), member => member.Register());
            await ProgramManagementServices.SeedAsync(provider,
                new ControlRules(fixture.TenantId, fixture.ControlId), control =>
                    CommandResult(control.AssignResponsibility(new ResponsibilityScope("control",
                            fixture.ControlId, ControlVersionIds.Initial(fixture.ControlId), 1),
                        Uuid.CreateVersion4(), fixture.OwnerMemberId,
                        ResponsibilityType.ControlOwner,
                        RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now,
                        now.AddMinutes(-1), null, [])));
            return fixture;
        }

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public ApproveControl Approve(Uuid reviewId) => new(Uuid.CreateVersion4(), TenantId, ProgramId, ControlId, 1,
            reviewId, EffectiveFrom, "Ready to operate.");

        public async Task<Uuid> ReviewAsync()
        {
            var requestId = Uuid.CreateVersion4();
            await PersonalControlDecisionTransportTests.HttpAsync(Provider, ReviewerUserId,
                new ReviewControl(requestId, TenantId, ProgramId, ControlId, 1, "accept", "Reviewed"),
                metadata: new RequestMetadata(requestId, requestId, null));
            return requestId;
        }

        public Task SuspendOwnerAsync() => ProgramManagementServices.SeedAsync(Provider,
            new Member(TenantId, OwnerUserId), member => member.Suspend(
                RbacIds.Member(TenantId, AuthorUserId), "Admin", DateTimeOffset.UtcNow,
                "Leave of absence"));

        public Task ReinstateOwnerAsync() => ProgramManagementServices.SeedAsync(Provider,
            new Member(TenantId, OwnerUserId), member => member.Reinstate(
                RbacIds.Member(TenantId, AuthorUserId), "Admin", DateTimeOffset.UtcNow));

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectSuccess();
            return result.Value;
        }

        public async Task<RequestErrorKind> QueryFailureAsync<TRequest>(TRequest request)
            where TRequest : IRequest<ControlVersionView>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectFailure();
            return result.Error!.Kind;
        }

        static Result CommandResult(CommandFailure? failure) => failure is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));
    }

    sealed class AcceptingValidator : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }
}
