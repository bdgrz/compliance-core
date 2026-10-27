using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Content;

public sealed class ProtectedResponseCompositionTests
{
    [Fact]
    public async Task ShouldPersistOnlyCiphertextGivenConfiguredProductionProtectedStore()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz.invalid/ws",
            ["Fitz:ApplicationName"] = "protected-response-composition-test",
            ["ProtectedResponses:ActiveKeyVersionId"] = "v1",
            ["ProtectedResponses:Keys:v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        }).Build();
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var services = new ServiceCollection();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IImmutableContentStore>(content);
        await using var provider = services.BuildServiceProvider();
        var tenant = Uuid.CreateVersion4().ToString();
        var scope = new ProtectedResponseScope(tenant, new EventStreamAddress(tenant, "protected-test", "owner"),
            Uuid.CreateVersion4(), ActorReference.ForMember(Uuid.CreateVersion4(), "Original"));
        var plaintext = Encoding.UTF8.GetBytes("The confidential original response must never be persisted as plaintext.");

        // Act
        var store = provider.GetService<IProtectedResponseContentStore>();

        // Assert
        Assert.NotNull(store);
        var reference = await store.WriteVerifiedAsync(scope, "protected-test.v1", plaintext);
        var durable = await content.ReadVerifiedAsync(tenant, new ImmutableContentRef(reference.ContentId,
            reference.CiphertextSha256, reference.Bytes, "application/vnd.bdgrz.protected-response",
            "protected-response-aes256gcm.v1"));
        Assert.True(durable.Span.IndexOf(plaintext) < 0);
        var restartedServices = new ServiceCollection();
        restartedServices.AddCompliance(configuration, developerAuthentication: true);
        restartedServices.AddSingleton<IImmutableContentStore>(content);
        await using var restarted = restartedServices.BuildServiceProvider();
        var read = await restarted.GetRequiredService<IProtectedResponseContentStore>().ReadVerifiedAsync(scope, reference);
        Assert.Equal(plaintext, read.ToArray());
    }
}
