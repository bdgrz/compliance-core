using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AccessReviews.Queries;
using Bdgrz.Compliance.Features.AccessReviews.Readers;
using Bdgrz.Compliance.Features.Applications.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class RestrictedAccessReviewCampaignReadTests
{
    [Fact]
    public async Task ShouldHideCampaignGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var handler = new GetAccessReviewCampaignHandler(scenario.Campaigns,
            new AccessReviewManagerPermissions(), TimeProvider.System,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = new RequestContext<GetAccessReviewCampaign>(new GetAccessReviewCampaign(
            scenario.TenantId, scenario.CampaignId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnCampaignItemGivenExactSystemInstanceReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var visibility = RestrictedApplicationVisibilityFixture.Create(scenario.Reader,
            allowedScopes: [new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
                scenario.InstanceId)]);
        var handler = new GetAccessReviewCampaignHandler(scenario.Campaigns,
            new AccessReviewManagerPermissions(), TimeProvider.System, visibility, null!);
        var context = new RequestContext<GetAccessReviewCampaign>(new GetAccessReviewCampaign(
            scenario.TenantId, scenario.CampaignId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(scenario.InstanceId, item.Item.SystemInstanceId);
        Assert.Equal(1, item.Revision);
    }

    [Fact]
    public async Task ShouldOmitCampaignGivenAllItemsHaveRestrictedInstances()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var summary = new AccessReviewCampaignSummaryView(scenario.TenantId,
            scenario.CampaignId, "Payroll review", DateTimeOffset.UtcNow.AddDays(1),
            AccessReviewCampaign.Active, 1, Uuid.CreateVersion4(), DateTimeOffset.UtcNow,
            null, null);
        var handler = new ListAccessReviewCampaignsHandler(
            new CampaignDirectory(summary), new InMemoryEventStore(), scenario.Campaigns, scenario.Reader,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader));
        var context = new RequestContext<ListAccessReviewCampaigns>(
            new ListAccessReviewCampaigns(scenario.TenantId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task ShouldRetainVisibleItemCountGivenExactSystemInstanceReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var summary = new AccessReviewCampaignSummaryView(scenario.TenantId,
            scenario.CampaignId, "Payroll review", DateTimeOffset.UtcNow.AddDays(1),
            AccessReviewCampaign.Active, 1, Uuid.CreateVersion4(), DateTimeOffset.UtcNow,
            null, null);
        var visibility = RestrictedApplicationVisibilityFixture.Create(scenario.Reader,
            allowedScopes: [new AccessGrantScope(AccessGrantScopeKind.SystemInstance,
                scenario.InstanceId)]);
        var handler = new ListAccessReviewCampaignsHandler(new CampaignDirectory(summary),
            new InMemoryEventStore(), scenario.Campaigns, scenario.Reader, visibility);
        var context = new RequestContext<ListAccessReviewCampaigns>(
            new ListAccessReviewCampaigns(scenario.TenantId), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(1, Assert.Single(result.Value.Items).ItemCount);
    }

    [Fact]
    public async Task ShouldHideBulkPreviewGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var handler = new PreviewBulkAccessDecisionHandler(scenario.Campaigns,
            RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = new RequestContext<PreviewBulkAccessDecision>(
            new PreviewBulkAccessDecision(scenario.TenantId, scenario.CampaignId,
                [scenario.ItemId], "keep"), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHideAssignedDecisionGivenReviewerWithoutRestrictedReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var handler = new RecordAccessDecisionHandler(null!, scenario.Campaigns, scenario.Reader, scenario.Content,
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(scenario.Reader), null!);
        var context = PersonalAccessReviewTransportTests.HttpContext<RecordAccessDecision>(new RecordAccessDecision(
            Uuid.CreateVersion4(), scenario.TenantId, scenario.CampaignId, scenario.ItemId, 1, "keep",
            "Still required."), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldHideCampaignCompletionGivenManagerWithoutRestrictedReadGrant()
    {
        // Arrange
        await using var scenario = await ScenarioData.CreateAsync();
        var handler = new CompleteAccessReviewCampaignHandler(null!, scenario.Campaigns, scenario.Content,
            TimeProvider.System, RestrictedApplicationVisibilityFixture.Create(scenario.Reader));
        var context = PersonalAccessReviewTransportTests.HttpContext<CompleteAccessReviewCampaign>(
            new CompleteAccessReviewCampaign(Uuid.CreateVersion4(), scenario.TenantId, scenario.CampaignId, 1,
                "I reviewed the campaign."), Actor(scenario.UserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(result.Error).Kind);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class ScenarioData : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;

        ScenarioData(ServiceProvider provider, Uuid tenantId, Uuid userId, Uuid applicationId, Uuid instanceId,
            Uuid populationId, Uuid campaignId, Uuid itemId)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            (TenantId, UserId, ApplicationId, InstanceId, PopulationId, CampaignId, ItemId) =
                (tenantId, userId, applicationId, instanceId, populationId, campaignId, itemId);
        }

        public Uuid TenantId { get; }
        public Uuid UserId { get; }
        public Uuid ApplicationId { get; }
        public Uuid InstanceId { get; }
        public Uuid PopulationId { get; }
        public Uuid CampaignId { get; }
        public Uuid ItemId { get; }
        public IAggregateReader Reader => _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        public AccessReviewReader Campaigns => _scope.ServiceProvider.GetRequiredService<AccessReviewReader>();
        public IImmutableContentStore Content => _scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();

        public static async Task<ScenarioData> CreateAsync()
        {
            var provider = ProgramManagementServices.Build(new AccessReviewManagerPermissions(), _ => { },
                services =>
                {
                    services.AddSingleton<AccessReviewManifests>();
                    services.AddScoped<AccessReviewReader>();
                });
            var tenantId = Uuid.CreateVersion4();
            var userId = Uuid.CreateVersion4();
            var memberId = RbacIds.Member(tenantId, userId);
            var applicationId = Uuid.CreateVersion4();
            var instanceId = Uuid.CreateVersion4();
            var populationId = Uuid.CreateVersion4();
            var campaignId = Uuid.CreateVersion4();
            var itemId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            var actor = ActorReference.ForMember(memberId, "Manager");
            await ProgramManagementServices.SeedAsync(provider, new DeclaredApplication(tenantId, applicationId),
                application => application.Declare("Payroll", "Run payroll", null, memberId, "Manager", now,
                    isRestricted: true));
            await ProgramManagementServices.SeedAsync(provider, new DeclaredSystemInstance(tenantId, instanceId),
                instance => instance.Declare(applicationId, "Production", "aws_account", null, null, memberId,
                    "Manager", now));
            var item = new AccessReviewItemView(itemId, populationId, Uuid.CreateVersion4(), instanceId, memberId,
                "subject", "user_account", "User", "human", null, null, "entitlement", "permission", "Read", false,
                "expected", [], null);
            await AccessReviewCampaignFacts.SeedLaunchedAsync(provider, tenantId, campaignId,
                [new AccessReviewerView(populationId, instanceId, memberId, false, null)], [item], actor, now);
            return new ScenarioData(provider, tenantId, userId, applicationId, instanceId, populationId, campaignId,
                itemId);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class AccessReviewManagerPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(permission == RbacPermissions.AccessReviewManage);
    }

    sealed class CampaignDirectory(AccessReviewCampaignSummaryView item)
        : IAccessReviewCampaignDirectoryReader
    {
        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<AccessReviewCampaignSummaryView>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<AccessReviewCampaignSummaryView>([item], null));
    }
}
