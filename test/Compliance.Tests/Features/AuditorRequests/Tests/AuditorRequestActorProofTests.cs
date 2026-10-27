using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

public sealed class AuditorRequestActorProofTests
{
    [Theory]
    [InlineData("member", "malformed-member-id")]
    [InlineData("system_process", "fd4b2565-1686-4e2c-bcb5-6b6b9c149257")]
    public async Task ShouldClassifyContradictoryNativeActorAsPermanentContentFailureGivenRequestRead(string kind, string id)
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var requestId = await fixture.SeedRequestAsync();
        var native = (await fixture.RequestAsync(requestId)).Recorded!;
        var actorId = kind == "member" ? id : native.Operation.Actor.Id;
        var altered = native with { Operation = native.Operation with { Actor = new ActorReference(kind, actorId, "Contradictory actor") } };
        altered.AttachMetadata(native.Metadata);
        var events = new InMemoryEventStore();
        await events.AppendAsync(new AuditorRequest(fixture.TenantId, fixture.ProgramId, requestId).Stream, 0,
            [altered], CancellationToken.None);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var requests = new AuditorRequestReader(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), events,
            fixture.Content);

        // Act
        var error = await Record.ExceptionAsync(async () => _ = await requests.ReadAsync(fixture.TenantId,
            fixture.ProgramId, requestId));

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(error);
        Assert.IsNotType<ImmutableContentUnavailableException>(error);
        Assert.Equal(1, (await fixture.RequestAsync(requestId)).Revision);
    }
}
