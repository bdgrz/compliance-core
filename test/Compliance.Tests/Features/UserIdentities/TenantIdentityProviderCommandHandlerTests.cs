using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class TenantIdentityProviderCommandHandlerTests
{
    [Fact]
    public void ShouldCarryClientIdentityGivenEveryProviderCommand()
    {
        // Arrange
        var configure = new ConfigureTenantIdentityProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "https://idp.example.test", "client", "vault://a/b", ["client"], 0);
        var rotate = new RotateTenantIdentityProviderCredentials(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "vault://a/c", "rotation", 1);
        var disable = new DisableTenantIdentityProvider(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, "No longer used.");
        var connection = Uuid.CreateVersion4();
        var test = new TestTenantIdentityProvider(Uuid.CreateVersion4(), connection, 1, "oidc-check");
        var record = new RecordTenantIdentityProviderTestResult(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            connection, 1, Uuid.CreateVersion4(), TenantIdentityProviderHost.Passed("oidc-check"), DateTimeOffset.UtcNow);

        // Act
        var identities = new[]
        {
            ((IIdentifiedRequest)configure).RequestId, ((IIdentifiedRequest)rotate).RequestId,
            ((IIdentifiedRequest)disable).RequestId, ((IIdentifiedRequest)record).RequestId,
        };

        // Assert
        Assert.Equal([configure.CommandId, rotate.CommandId, disable.CommandId, record.CommandId], identities);
        Assert.Equal(TestTenantIdentityProvider.TestId(connection, "oidc-check"), ((IIdentifiedRequest)test).RequestId);
        Assert.NotEqual(((IIdentifiedRequest)test).RequestId,
            TestTenantIdentityProvider.TestId(connection, "oidc-check-2"));
        Assert.NotEqual(((IIdentifiedRequest)test).RequestId,
            TestTenantIdentityProvider.TestId(Uuid.CreateVersion4(), "oidc-check"));
    }

    [Fact]
    public async Task ShouldConfigureConnectionAsPendingTestGivenPersonalAdministrator()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var command = host.ConfigureCommand();

        // Act
        var result = await host.ConfigureAsync(command);
        var saved = await host.ProviderAsync();
        var configured = Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderConfigured>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("pending_test", saved.Status);
        Assert.Equal(1, saved.Revision);
        Assert.Equal(1, saved.CredentialRevision);
        Assert.Equal("https://idp.example.test", saved.Issuer);
        Assert.Equal(["api://compliance", "client-app"], saved.AllowedAudiences);
        Assert.Equal(command.CommandId, configured.Operation.LogicalId);
        Assert.Equal(host.MemberId, configured.ActorMemberId);
        Assert.Equal(host.Clock.GetUtcNow(), configured.ConfiguredAt);
        Assert.NotNull(configured.FirstResponse);
    }

    [Fact]
    public async Task ShouldKeepTheCredentialReferenceOutOfFactsAndDigestsGivenConfigureAndRotate()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var configure = host.ConfigureCommand();
        await host.ConfigureAsync(configure);
        var rotate = host.RotateCommand(1);
        Assert.True((await host.Rotate.HandleAsync(host.Context(rotate), CancellationToken.None)).IsSuccess);

        // Act
        var facts = await host.ReadEventsAsync();
        var configured = facts.OfType<TenantIdentityProviderConfigured>().Single();
        var rotated = facts.OfType<TenantIdentityProviderCredentialsRotated>().Single();
        var serialized = string.Join('\n', JsonSerializer.Serialize(configured,
            ComplianceCoreJsonContext.Default.TenantIdentityProviderConfigured), JsonSerializer.Serialize(rotated,
            ComplianceCoreJsonContext.Default.TenantIdentityProviderCredentialsRotated));
        var protectedContent = await host.Content.ReadJsonAsync<TenantIdentityProviderCredentialContent>(
            host.TenantId.ToString(), configured.CredentialReference);

        // Assert
        Assert.DoesNotContain("vault://", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(TenantIdentityProviderHost.Reference, configured.Operation.AuthoredIntentSha256);
        Assert.Equal(TenantIdentityProviderHost.Reference, protectedContent.EncryptedCredentialReference);
        Assert.Equal(AuthoredIntentDigest.Compute(new ConfigureTenantIdentityProviderAuthoredIntent(host.TenantId,
                host.ProviderId, "https://idp.example.test", "client-app", ["api://compliance", "client-app"],
                configured.CredentialReference.Sha256, 0)), configured.Operation.AuthoredIntentSha256);
        Assert.NotEqual(AuthoredIntentDigest.Compute(configure), configured.Operation.AuthoredIntentSha256);
        Assert.Equal(AuthoredIntentDigest.Compute(new RotateTenantIdentityProviderCredentialsAuthoredIntent(host.TenantId,
                host.ProviderId, rotated.CredentialReference.Sha256, "rotation-2026-10", 1)),
            rotated.Operation.AuthoredIntentSha256);
        Assert.NotEqual(configured.CredentialReference.Sha256, rotated.CredentialReference.Sha256);
    }

    [Fact]
    public async Task ShouldReplayConfigureGivenExactRetryAfterLaterRevisions()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var command = host.ConfigureCommand();
        await host.ConfigureAsync(command);
        await host.RequestTestAsync("first-check", 1);

        // Act
        var retry = await host.ConfigureAsync(command);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderConfigured>());
        Assert.Equal(2, (await host.ProviderAsync()).Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenChangedIntentOnTheSameConfigureCommandIdentity()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var command = host.ConfigureCommand();
        await host.ConfigureAsync(command);

        // Act
        var otherReference = await host.ConfigureAsync(command with { EncryptedCredentialReference = TenantIdentityProviderHost.NextReference });
        var otherIssuer = await host.ConfigureAsync(command with { Issuer = "https://other-idp.example.test" });
        var otherAudiences = await host.ConfigureAsync(command with { AllowedAudiences = ["client-app"] });
        var reordered = await host.ConfigureAsync(command with { AllowedAudiences = ["api://compliance", "client-app"] });

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherReference.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherIssuer.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherAudiences.Error?.Kind);
        Assert.True(reordered.IsSuccess, reordered.Error?.Message);
        Assert.Single(await host.ReadEventsAsync());
    }

    [Fact]
    public async Task ShouldRejectConfigureGivenAnotherCommandOnAnExistingConnection()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();

        // Act
        var second = await host.ConfigureAsync(host.ConfigureCommand(expectedRevision: 1));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Single(await host.ReadEventsAsync());
    }

    [Fact]
    public async Task ShouldRejectConfigureGivenStaleExpectedRevision()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();

        // Act
        var stale = await host.ConfigureAsync(host.ConfigureCommand(expectedRevision: 3));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Empty(await host.ReadEventsAsync());
    }

    [Fact]
    public async Task ShouldRejectConfigureGivenInsecureIssuerOrMissingAudience()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var command = host.ConfigureCommand();

        // Act
        var insecure = await host.ConfigureAsync(command with { Issuer = "http://idp.example.test" });
        var audience = await host.ConfigureAsync(command with { AllowedAudiences = ["api://other"] });
        var reference = await host.ConfigureAsync(command with { EncryptedCredentialReference = "plain-secret" });

        // Assert
        Assert.Equal(RequestErrorKind.Validation, insecure.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, audience.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, reference.Error?.Kind);
        Assert.Empty(await host.ReadEventsAsync());
    }

    [Fact]
    public async Task ShouldPersistProviderTestRequestGivenPersonalAdministrator()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();

        // Act
        var requestEventId = await host.RequestTestAsync("oidc-check-2026-10", 1);
        var saved = await host.ProviderAsync();
        var requested = Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTestRequested>());

        // Assert
        Assert.Equal(2, saved.Revision);
        Assert.Equal("oidc-check-2026-10", saved.PendingTestKey);
        Assert.Equal(1, saved.PendingTestCredentialRevision);
        Assert.Equal(host.MemberId, saved.PendingTestRequestedBy);
        Assert.Equal(requestEventId, saved.PendingTestRequestEventId);
        Assert.Equal(TestTenantIdentityProvider.TestId(host.ProviderId, "oidc-check-2026-10"),
            requested.Operation.LogicalId);
        Assert.Equal(host.Clock.GetUtcNow(), requested.RequestedAt);
    }

    [Fact]
    public async Task ShouldRejectStaleProviderTestRequestGivenConcurrentProviderChange()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();

        // Act
        var result = await host.Test.HandleAsync(host.Context(
            new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 0, "stale-test")), CancellationToken.None);
        var saved = await host.ProviderAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Revision);
        Assert.Null(saved.PendingTestKey);
    }

    [Fact]
    public async Task ShouldTreatRepeatedTestKeyAsIdempotentGivenSamePendingRequest()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var request = new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 1, "retry-test");

        // Act
        var first = await host.Test.HandleAsync(host.Context(request), CancellationToken.None);
        var retry = await host.Test.HandleAsync(host.Context(request), CancellationToken.None);
        var saved = await host.ProviderAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(2, saved.Revision);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTestRequested>());
    }

    [Fact]
    public async Task ShouldReplayTestRequestGivenExactRetryAfterItsResultAndConflictOnKeyReuse()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("reused-key", 1);
        Assert.True((await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Passed("reused-key"))).IsSuccess);
        var request = new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 1, "reused-key");

        // Act
        var retry = await host.Test.HandleAsync(host.Context(request), CancellationToken.None);
        var reuse = await host.Test.HandleAsync(host.Context(request with { ExpectedRevision = 3 }), CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, reuse.Error?.Kind);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTestRequested>());
        Assert.Equal("enabled", (await host.ProviderAsync()).Status);
    }

    [Fact]
    public async Task ShouldRejectSecondTestGivenAnotherTestIsPending()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        await host.RequestTestAsync("first-check", 1);

        // Act
        var second = await host.Test.HandleAsync(host.Context(
            new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 2, "second-check")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTestRequested>());
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenProviderTestRequest()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var context = new Cntryl.Portia.RequestContext<TestTenantIdentityProvider>(
            new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 1, "system-check"),
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("PUT",
                "/api/v1/tenants/test/identity-providers/test/tests/key",
                "/api/v1/tenants/test/identity-providers/test/tests/key", "identity-provider-test")));

        // Act
        var result = await host.Test.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(1, (await host.ProviderAsync()).Revision);
    }

    [Fact]
    public async Task ShouldRecordExactTestResultWithNamedWorkerAttributionGivenPersistedTestRequest()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("worker-check", 1);

        // Act
        var result = await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Passed("worker-check"));
        var saved = await host.ProviderAsync();
        var tested = Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTested>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("enabled", saved.Status);
        Assert.Equal(3, saved.Revision);
        Assert.Null(saved.PendingTestKey);
        Assert.Equal(requestEventId, tested.TestRequestEventId);
        Assert.Equal(ActorReference.ForSystemProcess(RecordTenantIdentityProviderTestResultAuthorizer.ProcessId,
            "Tenant identity provider test worker"), tested.TestedByActor);
        Assert.Equal(requestEventId, tested.Operation.CausationEventId);
    }

    [Fact]
    public async Task ShouldReplayResultGivenRedeliveredWorkerCallbackAndConflictOnChangedResult()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("callback-check", 1);
        var completedAt = host.Clock.GetUtcNow();
        Assert.True((await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Failed("callback-check"),
            completedAt)).IsSuccess);

        // Act
        var redelivered = await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Failed("callback-check"),
            completedAt);
        var changed = await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Passed("callback-check"),
            completedAt);

        // Assert
        Assert.True(redelivered.IsSuccess, redelivered.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTested>());
        Assert.Equal("test_failed", (await host.ProviderAsync()).Status);
    }

    [Fact]
    public async Task ShouldRejectWrongWorkerCauseAndFabricatedRequestGivenPendingProviderTest()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("pending-check", 1);
        var result = TenantIdentityProviderHost.Passed("pending-check");
        var commandId = RecordTenantIdentityProviderTestResultHandler.CommandIdOf(requestEventId, host.TenantId, host.ProviderId);

        // Act
        var wrongCause = await host.Record.HandleAsync(TenantIdentityProviderHost.WorkerContext(
            new RecordTenantIdentityProviderTestResult(commandId, host.TenantId, host.ProviderId, 2, requestEventId, result,
                host.Clock.GetUtcNow()), Uuid.CreateVersion4()), CancellationToken.None);
        var fabricatedId = Uuid.CreateVersion4();
        var fabricated = await host.Record.HandleAsync(TenantIdentityProviderHost.WorkerContext(
            new RecordTenantIdentityProviderTestResult(Uuid.CreateVersion4(), host.TenantId, host.ProviderId, 2, fabricatedId,
                result, host.Clock.GetUtcNow()), fabricatedId), CancellationToken.None);
        var wrongActor = await host.Record.HandleAsync(TenantIdentityProviderHost.WorkerContext(
            new RecordTenantIdentityProviderTestResult(commandId, host.TenantId, host.ProviderId, 2, requestEventId, result,
                host.Clock.GetUtcNow()), requestEventId, actor: RequestActor.System), CancellationToken.None);
        var wrongInvocation = await host.Record.HandleAsync(TenantIdentityProviderHost.WorkerContext(
            new RecordTenantIdentityProviderTestResult(commandId, host.TenantId, host.ProviderId, 2, requestEventId, result,
                host.Clock.GetUtcNow()), requestEventId, invocation: new HttpInvocation("PUT", "/x", "/x", "x")),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, wrongCause.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, fabricated.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, wrongActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, wrongInvocation.Error?.Kind);
        Assert.Empty((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTested>());
        Assert.Equal("pending-check", (await host.ProviderAsync()).PendingTestKey);
    }

    [Fact]
    public async Task ShouldRejectLateTestResultGivenCredentialRotationAfterRequest()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("late-check", 1);
        Assert.True((await host.Rotate.HandleAsync(host.Context(host.RotateCommand(2)), CancellationToken.None)).IsSuccess);

        // Act
        var late = await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Passed("late-check"));
        var saved = await host.ProviderAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        Assert.Equal("pending_test", saved.Status);
        Assert.Null(saved.PendingTestKey);
        Assert.Empty((await host.ReadEventsAsync()).OfType<TenantIdentityProviderTested>());
    }

    [Fact]
    public async Task ShouldRotateCredentialAndRequireNewProviderTestGivenEnabledConnection()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var requestEventId = await host.RequestTestAsync("enable-check", 1);
        Assert.True((await host.RecordResultAsync(requestEventId, 2, TenantIdentityProviderHost.Passed("enable-check"))).IsSuccess);

        // Act
        var rotated = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(3)), CancellationToken.None);
        var saved = await host.ProviderAsync();

        // Assert
        Assert.True(rotated.IsSuccess, rotated.Error?.Message);
        Assert.Equal("pending_test", saved.Status);
        Assert.Equal(2, saved.CredentialRevision);
        Assert.Equal(4, saved.Revision);
        Assert.Equal("rotation-2026-10", saved.LastRotationReference);
    }

    [Fact]
    public async Task ShouldReplayRotationGivenExactRetryAfterLaterRevisionsAndConflictOnChangedIntent()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var command = host.RotateCommand(1);
        Assert.True((await host.Rotate.HandleAsync(host.Context(command), CancellationToken.None)).IsSuccess);
        await host.RequestTestAsync("after-rotation", 2);

        // Act
        var retry = await host.Rotate.HandleAsync(host.Context(command), CancellationToken.None);
        var otherReference = await host.Rotate.HandleAsync(host.Context(command with
        {
            EncryptedCredentialReference = "vault://tenant/idp/credential-v3",
        }), CancellationToken.None);
        var otherRotation = await host.Rotate.HandleAsync(host.Context(command with { RotationReference = "rotation-other" }),
            CancellationToken.None);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, otherReference.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherRotation.Error?.Kind);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderCredentialsRotated>());
    }

    [Fact]
    public async Task ShouldRejectRotationGivenUnchangedCredentialStaleRevisionOrReusedRotationReference()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();

        // Act
        var unchanged = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(1,
            reference: TenantIdentityProviderHost.Reference)), CancellationToken.None);
        var stale = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(0)), CancellationToken.None);
        Assert.True((await host.Rotate.HandleAsync(host.Context(host.RotateCommand(1)), CancellationToken.None)).IsSuccess);
        var reused = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(2,
            reference: "vault://tenant/idp/credential-v3")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, unchanged.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, reused.Error?.Kind);
        Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderCredentialsRotated>());
    }

    [Fact]
    public async Task ShouldKeepAcceptingTestsAndRotationsGivenMoreThanTheFormerHundredFactCeiling()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var revision = 1L;

        // Act
        for (var cycle = 1; cycle <= 105; cycle++)
        {
            var key = $"check-{cycle}";
            var requestEventId = await host.RequestTestAsync(key, revision);
            var recorded = await host.RecordResultAsync(requestEventId, revision + 1, TenantIdentityProviderHost.Passed(key));
            Assert.True(recorded.IsSuccess, recorded.Error?.Message);
            revision += 2;
        }

        for (var rotation = 1; rotation <= 105; rotation++)
        {
            var rotated = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(revision,
                reference: $"vault://tenant/idp/credential-r{rotation}", rotationReference: $"rotation-{rotation}")),
                CancellationToken.None);
            Assert.True(rotated.IsSuccess, rotated.Error?.Message);
            revision++;
        }

        var saved = await host.ProviderAsync();
        var facts = await host.ReadEventsAsync();

        // Assert
        Assert.Equal(106, saved.CredentialRevision);
        Assert.Equal(1 + 210 + 105, saved.Revision);
        Assert.Equal(105, facts.OfType<TenantIdentityProviderTested>().Count());
        Assert.All(facts, fact => Assert.True(JsonSerializer.SerializeToUtf8Bytes(fact, fact.GetType(),
            ComplianceCoreJsonContext.Default.Options).Length < 4096));
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenProviderConfiguration()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        var context = new Cntryl.Portia.RequestContext<ConfigureTenantIdentityProvider>(host.ConfigureCommand(),
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("PUT", "/x", "/x", "x")));

        // Act
        var result = await host.Configure.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(await host.ReadEventsAsync());
    }

    [Fact]
    public async Task ShouldDisableConfiguredConnectionAndRetainAttributionGivenTenantAdministrator()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var command = new DisableTenantIdentityProvider(Uuid.CreateVersion4(), host.TenantId, host.ProviderId, 1,
            "Client no longer uses this provider.");

        // Act
        var result = await host.Disable.HandleAsync(host.Context(command), CancellationToken.None);
        var retry = await host.Disable.HandleAsync(host.Context(command), CancellationToken.None);
        var changed = await host.Disable.HandleAsync(host.Context(command with { Rationale = "Another reason." }),
            CancellationToken.None);
        var saved = await host.ProviderAsync();
        var disabled = Assert.Single((await host.ReadEventsAsync()).OfType<TenantIdentityProviderDisabled>());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal("disabled", saved.Status);
        Assert.Equal("Client no longer uses this provider.", saved.DisabledRationale);
        Assert.Equal(host.MemberId, disabled.ActorMemberId);
        Assert.Equal(command.CommandId, disabled.Operation.LogicalId);
    }

    [Fact]
    public async Task ShouldRejectEveryCommandGivenDisabledConnection()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        Assert.True((await host.Disable.HandleAsync(host.Context(new DisableTenantIdentityProvider(Uuid.CreateVersion4(),
            host.TenantId, host.ProviderId, 1, "Retired.")), CancellationToken.None)).IsSuccess);

        // Act
        var test = await host.Test.HandleAsync(host.Context(
            new TestTenantIdentityProvider(host.TenantId, host.ProviderId, 2, "after-disable")), CancellationToken.None);
        var rotate = await host.Rotate.HandleAsync(host.Context(host.RotateCommand(2)), CancellationToken.None);
        var again = await host.Disable.HandleAsync(host.Context(new DisableTenantIdentityProvider(Uuid.CreateVersion4(),
            host.TenantId, host.ProviderId, 2, "Retired again.")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, test.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, rotate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, again.Error?.Kind);
        Assert.Equal(2, (await host.ProviderAsync()).Revision);
    }

    [Fact]
    public async Task ShouldRejectStaleDisableWithoutChangingConnectionGivenConcurrentUpdate()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        await host.RequestTestAsync("racing-check", 1);

        // Act
        var result = await host.Disable.HandleAsync(host.Context(new DisableTenantIdentityProvider(Uuid.CreateVersion4(),
            host.TenantId, host.ProviderId, 1, "Racing disable.")), CancellationToken.None);
        var saved = await host.ProviderAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("pending_test", saved.Status);
        Assert.Equal(2, saved.Revision);
    }

    [Fact]
    public async Task ShouldRejectSystemActorGivenPersonalDisableCommand()
    {
        // Arrange
        await using var host = new TenantIdentityProviderHost();
        await host.ConfigureAsync();
        var context = new Cntryl.Portia.RequestContext<DisableTenantIdentityProvider>(
            new DisableTenantIdentityProvider(Uuid.CreateVersion4(), host.TenantId, host.ProviderId, 1, "System attempt."),
            new RequestDispatchContext(RequestActor.System, new HttpInvocation("PUT", "/x", "/x", "x")));

        // Act
        var result = await host.Disable.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal("pending_test", (await host.ProviderAsync()).Status);
        Assert.Equal(1, (await host.ProviderAsync()).Revision);
    }
}
