using System.Text.Json;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class TenantIdentityProviderTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProviderId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequireSecureIssuerAndClientAudienceGivenProviderConfiguration()
    {
        // Arrange
        var provider = new TenantIdentityProvider(TenantId, ProviderId);

        // Act
        var insecureIssuer = provider.Configure(Content("http://idp.example.com"), ActorId,
            "Tenant administrator", Now);
        var missingClientAudience = provider.Configure(Content("https://idp.example.com") with
        {
            AllowedAudiences = ["api://compliance"],
        }, ActorId, "Tenant administrator", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(insecureIssuer).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missingClientAudience).Code);
        Assert.Null(provider.Current);
    }

    [Fact]
    public void ShouldRequireSuccessfulTestAfterRotationAndNeverExposeCredentialReferenceGivenConnectionLifecycle()
    {
        // Arrange
        var provider = new TenantIdentityProvider(TenantId, ProviderId);
        Assert.Null(provider.Configure(Content("https://idp.example.com"), ActorId,
            "Tenant administrator", Now));

        // Act
        var failedTest = provider.RecordTest(provider.Revision,
            new TenantIdentityProviderTestResultContent("test-1", "failed", "metadata_unavailable",
                "The provider metadata endpoint could not be reached."), ActorId,
            "Tenant administrator", Now.AddMinutes(1));
        var failedTestView = provider.Current!;
        var rotate = provider.RotateCredentials(provider.Revision, "vault://tenant/idp/credential-v2",
            "rotation-2", ActorId, "Tenant administrator", Now.AddMinutes(2));
        var rotatedView = provider.Current!;
        var passedTest = provider.RecordTest(provider.Revision,
            new TenantIdentityProviderTestResultContent("test-2", "passed", null, null), ActorId,
            "Tenant administrator", Now.AddMinutes(3));
        var passedTestView = provider.Current!;
        var disabled = provider.Disable(provider.Revision, "Client ended this federation connection.",
            ActorId, "Tenant administrator", Now.AddMinutes(4));
        var events = new AggregateScenario<TenantIdentityProvider>(provider).PendingEvents.ToArray();
        var serializedEvents = string.Join(" ", events.Select(item =>
            JsonSerializer.Serialize(item, item.GetType())));
        var replay = new AggregateScenario<TenantIdentityProvider>(
            new TenantIdentityProvider(TenantId, ProviderId)).Given(events).Aggregate;

        // Assert
        Assert.Null(failedTest);
        Assert.Equal("test_failed", failedTestView.Status);
        Assert.Null(rotate);
        Assert.Equal("pending_test", rotatedView.Status);
        Assert.Null(passedTest);
        Assert.Equal("enabled", passedTestView.Status);
        Assert.Null(disabled);
        Assert.Equal("disabled", provider.Current!.Status);
        Assert.DoesNotContain("vault://tenant/idp/credential", JsonSerializer.Serialize(provider.Current));
        Assert.DoesNotContain("Membership", JsonSerializer.Serialize(provider.Current), StringComparison.OrdinalIgnoreCase);
        Assert.True(serializedEvents.Split("vault://tenant/idp/credential-v1", StringSplitOptions.None).Length - 1 == 1,
            serializedEvents);
        Assert.True(serializedEvents.Split("vault://tenant/idp/credential-v2", StringSplitOptions.None).Length - 1 == 1,
            serializedEvents);
        Assert.Equal("disabled", replay.Current!.Status);
        Assert.Equal(provider.Current.ContentSha256, replay.Current.ContentSha256);
        Assert.Throws<InvalidOperationException>(() => new AggregateScenario<TenantIdentityProvider>(
            new TenantIdentityProvider(Uuid.CreateVersion4(), ProviderId)).Given(events));
    }

    static TenantIdentityProviderConfigurationContent Content(string issuer) => new(
        issuer, "client-app", "vault://tenant/idp/credential-v1", ["client-app", "api://compliance"]);
}
