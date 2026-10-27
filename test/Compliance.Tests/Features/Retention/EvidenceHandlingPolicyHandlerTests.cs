using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Retention.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Retention.Handlers;
using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class EvidenceHandlingPolicyHandlerTests : IAsyncDisposable
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    readonly Uuid _tenantId = Uuid.CreateVersion4();
    readonly Uuid _artifactId = Uuid.CreateVersion4();
    readonly Uuid _userId = Uuid.CreateVersion4();
    readonly InMemoryEventStore _events = new();
    readonly ServiceProvider _provider;

    public EvidenceHandlingPolicyHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(_events);
        services.AddSingleton(TimeProvider.System);
        services.AddPortia();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public async Task ShouldAssignReviewedPolicyToExactArtifactIdentityGivenCurrentPolicyVersion()
    {
        // Arrange
        await using var scope = _provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var artifact = new EvidenceArtifact(_tenantId, _artifactId);
        var bytes = "evidence"u8.ToArray();
        var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        Assert.True(artifact.Register(new EvidenceArtifactContent("Evidence", null, "export", "Manual",
            Now, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "internal"), digest,
            bytes.Length, ActorReference.ForMember(_userId, "Uploader"), Now).IsSuccess);
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now.AddMinutes(1)));
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(artifact,
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var request = new AssignArtifactHandlingPolicy(_tenantId, _artifactId, 0, "restricted",
            EvidenceHandlingPolicy.CurrentPolicyVersion, "The report includes customer-specific identifiers.");
        var context = new FixedContext<AssignArtifactHandlingPolicy>(request, Actor(_userId));
        var handler = new AssignArtifactHandlingPolicyHandler(services.GetRequiredService<IAggregateExecutor>(),
            services.GetRequiredService<IAggregateReader>(), TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var policy = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new EvidenceHandlingPolicy(_tenantId, _artifactId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, policy.Revision);
        Assert.Equal("restricted", policy.Current!.HandlingClass);
        Assert.Equal("The report includes customer-specific identifiers.", policy.Current.Rationale);
        Assert.Equal(2UL, policy.Current.SourcePosition);
        Assert.Equal(EvidenceArtifactStates.Available, policy.Current.SourceState);
    }

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString()),
            new Claim("email", "admin@example.test")], "BdgrzSession"));

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    sealed class FixedContext<T>(T request, ClaimsPrincipal actor) : IRequestContext<T>
    {
        public T Request => request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = Uuid.CreateVersion4();
        public Uuid CorrelationId => RequestId;
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = Now;
        public RequestInvocation Invocation { get; } = new HttpInvocation("POST", "/evidence/policies",
            "/evidence/policies", "test");
    }
}
