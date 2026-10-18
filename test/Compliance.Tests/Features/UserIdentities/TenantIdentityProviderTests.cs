using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
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
    public void ShouldKeepFirstRequestTimeGivenRepeatedProviderTestKey()
    {
        // Arrange
        var provider = new TenantIdentityProvider(TenantId, ProviderId);
        Assert.Null(provider.Configure(Content("https://idp.example.com"), ActorId,
            "Tenant administrator", Now));

        // Act
        var first = provider.RequestTest(provider.Revision, "retry-test", ActorId,
            "Tenant administrator", Now.AddMinutes(1));
        var retry = provider.RequestTest(1, "retry-test", ActorId,
            "Tenant administrator", Now.AddMinutes(2));

        // Assert
        Assert.Null(first);
        Assert.Null(retry);
        Assert.Equal(2, provider.Revision);
        Assert.Equal(Now.AddMinutes(1), provider.Current!.PendingTest!.RequestedAt);
    }

    [Fact]
    public void ShouldInvalidatePendingTestGivenCredentialRotation()
    {
        // Arrange
        var provider = new TenantIdentityProvider(TenantId, ProviderId);
        Assert.Null(provider.Configure(Content("https://idp.example.com"), ActorId,
            "Tenant administrator", Now));
        Assert.Null(provider.RequestTest(provider.Revision, "test-before-rotation", ActorId,
            "Tenant administrator", Now.AddMinutes(1)));

        // Act
        var rotated = provider.RotateCredentials(provider.Revision,
            "vault://tenant/idp/credential-v2", "rotation-2", ActorId,
            "Tenant administrator", Now.AddMinutes(2));
        var lateResult = provider.RecordTest(provider.Revision,
            Uuid.CreateVersion4(),
            new TenantIdentityProviderTestResultContent("test-before-rotation", "passed", null, null),
            ActorReference.ForMember(ActorId, "Tenant administrator"), Now.AddMinutes(3));

        // Assert
        Assert.Null(rotated);
        Assert.Null(provider.Current!.PendingTest);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(lateResult).Code);
        Assert.Empty(provider.Current.TestHistory);
    }

    [Fact]
    public void ShouldReplayLegacyProviderTestEventsGivenDiscriminatorVersionTwo()
    {
        // Arrange
        const string display = "Tenant administrator";
        var content = Content("https://idp.example.com") with
        {
            AllowedAudiences = ["api://compliance", "client-app"],
        };
        var configured = new TenantIdentityProviderView(TenantId, ProviderId, "pending_test", 1, 1,
            content.Issuer, content.ClientId, content.AllowedAudiences, [], null, null, ActorId,
            display, Now, ActorId, display, Now, string.Empty);
        configured = configured with { ContentSha256 = LegacyDigest(configured) };
        var requestedAt = Now.AddMinutes(1);
        var request = new TenantIdentityProviderTestRequestView("legacy-test", 1, ActorId, display,
            requestedAt);
        var pending = configured with
        {
            Revision = 2,
            UpdatedAt = requestedAt,
            PendingTest = request,
            ContentSha256 = string.Empty,
        };
        pending = pending with { ContentSha256 = PriorRequestDigest(pending) };
        var testedAt = Now.AddMinutes(2);
        var test = new TenantIdentityProviderTestView("legacy-test", 1, "passed", null, null,
            ActorId, display, testedAt);
        var completed = pending with
        {
            Status = "enabled",
            Revision = 3,
            TestHistory = [test],
            PendingTest = null,
            UpdatedAt = testedAt,
            ContentSha256 = string.Empty,
        };
        completed = completed with { ContentSha256 = LegacyDigest(completed) };
        DomainEvent[] history =
        [
            new TenantIdentityProviderConfigured(TenantId, ProviderId, 1, ActorId, content,
                configured, Now),
            new TenantIdentityProviderTestRequested(TenantId, ProviderId, 2, request, pending,
                requestedAt),
            new TenantIdentityProviderTestedV2(TenantId, ProviderId, 3, ActorId,
                new TenantIdentityProviderTestResultContent("legacy-test", "passed", null, null),
                completed, testedAt),
        ];

        // Act
        var replay = new AggregateScenario<TenantIdentityProvider>(
            new TenantIdentityProvider(TenantId, ProviderId)).Given(history).Aggregate;

        // Assert
        Assert.Equal("enabled", replay.Current!.Status);
        Assert.Null(replay.Current.PendingTest);
        var recorded = Assert.Single(replay.Current.TestHistory);
        Assert.Null(recorded.TestedByActor);
        Assert.Null(recorded.TestRequestEventId);
        Assert.Equal(ActorId, recorded.TestedByMemberId);
    }

    [Fact]
    public void ShouldRequireSuccessfulTestAfterRotationAndNeverExposeCredentialReferenceGivenConnectionLifecycle()
    {
        // Arrange
        var provider = new TenantIdentityProvider(TenantId, ProviderId);
        Assert.Null(provider.Configure(Content("https://idp.example.com"), ActorId,
            "Tenant administrator", Now));
        Assert.Null(provider.RequestTest(provider.Revision, "test-1", ActorId,
            "Tenant administrator", Now.AddMinutes(1)));

        // Act
        var failedTest = provider.RecordTest(provider.Revision,
            Uuid.CreateVersion4(),
            new TenantIdentityProviderTestResultContent("test-1", "failed", "metadata_unavailable",
                "The provider metadata endpoint could not be reached."),
            ActorReference.ForMember(ActorId, "Tenant administrator"), Now.AddMinutes(1));
        var failedTestView = provider.Current!;
        var rotate = provider.RotateCredentials(provider.Revision, "vault://tenant/idp/credential-v2",
            "rotation-2", ActorId, "Tenant administrator", Now.AddMinutes(2));
        var rotatedView = provider.Current!;
        Assert.Null(provider.RequestTest(provider.Revision, "test-2", ActorId,
            "Tenant administrator", Now.AddMinutes(3)));
        var passedTest = provider.RecordTest(provider.Revision,
            Uuid.CreateVersion4(),
            new TenantIdentityProviderTestResultContent("test-2", "passed", null, null),
            ActorReference.ForMember(ActorId, "Tenant administrator"), Now.AddMinutes(3));
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

    static string LegacyDigest(TenantIdentityProviderView state) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            state.TenantId,
            state.ProviderConnectionId,
            state.Status,
            state.Revision,
            state.CredentialRevision,
            state.Issuer,
            state.ClientId,
            state.AllowedAudiences,
            TestHistory = LegacyTestHistory(state),
            state.LastRotationReference,
            state.DisabledRationale,
            state.ConfiguredByMemberId,
            state.ConfiguredByDisplay,
            state.ConfiguredAt,
            state.ActorMemberId,
            state.ActorDisplay,
            state.UpdatedAt,
        }))).ToLowerInvariant();

    static string PriorRequestDigest(TenantIdentityProviderView state) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            state.TenantId,
            state.ProviderConnectionId,
            state.Status,
            state.Revision,
            state.CredentialRevision,
            state.Issuer,
            state.ClientId,
            state.AllowedAudiences,
            TestHistory = LegacyTestHistory(state),
            state.LastRotationReference,
            state.DisabledRationale,
            state.ConfiguredByMemberId,
            state.ConfiguredByDisplay,
            state.ConfiguredAt,
            state.ActorMemberId,
            state.ActorDisplay,
            state.UpdatedAt,
            state.PendingTest,
        }))).ToLowerInvariant();

    static object[] LegacyTestHistory(TenantIdentityProviderView state) => state.TestHistory.Select(test =>
        (object)new
        {
            test.TestKey,
            test.CredentialRevision,
            test.Outcome,
            test.FailureCode,
            test.ActionableMessage,
            test.TestedByMemberId,
            test.TestedByDisplay,
            test.TestedAt,
        }).ToArray();
}
