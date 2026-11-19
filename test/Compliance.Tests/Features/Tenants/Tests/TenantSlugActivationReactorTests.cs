using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Reactors;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class TenantSlugActivationReactorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldActivateOnlyCompletedCreatorGivenSlugFinishesLast(bool recovering, bool primaryReady)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creator = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creator, "Company", "company", creatorIsAdministrator: true).IsSuccess);
        if (recovering)
        {
            Assert.True(tenant.RejectSlug("company").IsSuccess);
            Assert.True(tenant.RequestSlugChange("company-next").IsSuccess);
        }
        var facts = new AggregateScenario<Tenant>(tenant);
        Assert.True(tenant.ConfirmSlug(recovering ? "company-next" : "company").IsSuccess);
        var trigger = Assert.Single(facts.PendingEvents, fact => fact is TenantSlugConfirmed or TenantSlugChanged);
        await writer.SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
        var member = new Member(tenantId, creator);
        Assert.True(member.Register().IsSuccess);
        var designation = Uuid.CreateVersion4();
        var primaries = new PrimaryOrgAdmins(tenantId, designation);
        Assert.True(primaries.Designate(designation, creator, member.MembershipEpisodeId).IsSuccess);
        if (primaryReady)
        {
            Assert.True(member.MarkPrimary(designation, member.MembershipEpisodeId).IsSuccess);
            Assert.True(primaries.RecordOutcome(designation, creator, member.MembershipEpisodeId, null).IsSuccess);
        }
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        await writer.SaveAsync(primaries, new RequestDispatchContext(RequestActor.System));
        var scenario = new ReactorScenario().Given(trigger).RespondTo<ActivateTenant>(Result.Success);
        var reactor = new TenantSlugActivationReactor(new InMemoryProjectionCheckpointStore(),
            scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scenario.Requests,
            NullLogger<TenantSlugActivationReactor>.Instance);

        // Act
        await scenario.RunAsync(reactor);

        // Assert
        if (primaryReady)
        {
            var activation = Assert.Single(scenario.SentRequests.OfType<ActivateTenant>());
            Assert.Equal(tenantId, activation.TenantId);
            Assert.Equal(creator, activation.FirstAdministratorUserId);
        }
        else
            Assert.DoesNotContain(scenario.SentRequests, request => request is ActivateTenant);
    }
}
