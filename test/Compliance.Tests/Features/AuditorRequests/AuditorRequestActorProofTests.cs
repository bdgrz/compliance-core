using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

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
