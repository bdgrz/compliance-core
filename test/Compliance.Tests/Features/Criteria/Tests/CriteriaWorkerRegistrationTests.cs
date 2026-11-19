using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Processes;
using Bdgrz.Compliance.Features.Criteria.Projectors;
using Bdgrz.Compliance.Features.Criteria.Reactors;
using Bdgrz.Compliance.Features.Criteria.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class CriteriaWorkerRegistrationTests
{
    [Fact]
    public void ShouldDeclareIndependentWorkersOnlyGivenExplicitOptIn()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddPortia();

        // Act
        var returned = builder.AddCriteriaWorkers();
        builder.AddCriteriaWorkers();
        using var provider = services.BuildServiceProvider();
        var workloads = provider.GetServices<WorkloadRegistration>().ToArray();
        var definitions = provider.GetServices<ProcessDefinition>().ToArray();
        var handlers = provider.GetServices<RequestHandlerRegistration>().ToArray();
        var authorizers = provider.GetServices<RequestAuthorizerRegistration>()
            .Where(item => item.AuthorizerType == typeof(CriteriaSignalSystemAuthorizer)).ToArray();

        // Assert
        Assert.Same(builder, returned);
        Assert.Equal(5, workloads.Length);
        Assert.All(workloads, workload => Assert.Equal(WorkloadScope.Global, workload.Scope));
        Assert.Contains(workloads, item => item.ComponentType == typeof(CriteriaLabelSignalReactor));
        Assert.Contains(workloads, item => item.ComponentType == typeof(CriteriaEntrySignalReactor));
        Assert.Contains(workloads, item => item.ComponentType == typeof(CriteriaValueSignalReactor));
        Assert.Contains(workloads, item => item.ComponentType == typeof(CriteriaPublicationSignalReactor));
        Assert.Contains(workloads, item => item.ComponentType == typeof(RuntimeCriteriaCatalogProjector));
        Assert.DoesNotContain(workloads, item => item.ComponentType == typeof(CriteriaPublicationWitnessReactor));
        Assert.Equal(4, definitions.Length);
        Assert.Contains(definitions, item => item.Name == CriteriaLabelSignal.ProcessName);
        Assert.Contains(definitions, item => item.Name == CriteriaEntrySignal.ProcessName);
        Assert.Contains(definitions, item => item.Name == CriteriaValueSignal.ProcessName);
        Assert.Contains(definitions, item => item.Name == CriteriaPublicationSignal.ProcessName);
        Assert.Equal(18, authorizers.Length);
        Assert.All(authorizers, authorizer =>
            Assert.Contains(handlers, handler => handler.RequestType == authorizer.RequestType));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaFreshEditionHead));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaFreshEntryHead));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaFreshLabelClaimHead));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaParticipantCausalOrder));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(ICriteriaSourceCutCoverage));
    }

    [Fact]
    public void ShouldNotRegisterCriteriaWorkersWithoutExplicitOptIn()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddPortia();

        // Act
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.Empty(provider.GetServices<WorkloadRegistration>());
        Assert.Empty(provider.GetServices<ProcessDefinition>());
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(CriteriaLabelSignalIntake));
    }

    [Fact]
    public async Task ShouldRestrictParticipantDispatchToSystemActor()
    {
        // Arrange
        var authorizer = new CriteriaSignalSystemAuthorizer();
        var request = new ReserveCriteriaEditionLabel(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "test",
            0, Uuid.CreateVersion4());
        var system = new RequestContext<ReserveCriteriaEditionLabel>(request, RequestActor.System);
        var anonymous = new RequestContext<ReserveCriteriaEditionLabel>(request, RequestActor.Anonymous);

        // Act
        var allowed = await authorizer.AuthorizeAsync(system, CancellationToken.None);
        var denied = await authorizer.AuthorizeAsync(anonymous, CancellationToken.None);

        // Assert
        Assert.True(allowed.IsSuccess);
        Assert.False(denied.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error!.Kind);
    }
}
