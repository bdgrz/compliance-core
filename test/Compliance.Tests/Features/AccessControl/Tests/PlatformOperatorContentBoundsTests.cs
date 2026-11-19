using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class PlatformOperatorContentBoundsTests
{
    [Fact]
    public void ShouldRejectGrantGivenTheIssuerIsOversized()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner);
        var actor = new ActorAttribution(actorId.ToString(), new string('i', PlatformOperatorContentBounds.EventPayloadBytes + 1));

        // Act
        var result = owner.Grant(operation, 0, actorId, actor, "reason");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectRevocationGivenTheIssuerIsOversized()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var grantId = Uuid.CreateVersion4();
        var granted = new PlatformOperator(user, grantId);
        var grantScenario = new AggregateScenario<PlatformOperator>(granted);
        Assert.True(granted.Grant(grantId, 0, actorId, new ActorAttribution(actorId.ToString(), "bdgrz"), "grant").IsSuccess);
        var revokeId = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, revokeId);
        var scenario = new AggregateScenario<PlatformOperator>(owner).Given(grantScenario.PendingEvents.ToArray());
        var actor = new ActorAttribution(actorId.ToString(), new string('i', PlatformOperatorContentBounds.EventPayloadBytes + 1));

        // Act
        var result = owner.Revoke(revokeId, 1, grantId, actorId, actor, "reason");

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.True(owner.IsActive);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectBusinessPayloadGivenEscapingExceedsTheLimit()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution(actorId.ToString(), new string('\u0001', 1300));
        var reason = new string('\u0001', 200);
        var candidate = new PlatformOperatorAccessGranted(user, operation, 0, actorId, actor, reason);
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner);

        // Act
        var result = owner.Grant(operation, 0, actorId, actor, reason);

        // Assert
        Assert.True(Encoding.UTF8.GetByteCount(actor.Issuer) < PlatformOperatorContentBounds.EventPayloadBytes);
        Assert.True(PlatformOperatorContentBounds.ActorFields(actor));
        Assert.True(DomainEventPayload.Size(candidate) > PlatformOperatorContentBounds.EventPayloadBytes);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldRejectBusinessPayloadGivenReplayContainsOversizedContent()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var actor = new ActorAttribution(actorId.ToString(), new string('\u0001', 1300));
        var fact = new PlatformOperatorAccessGranted(user, operation, 0, actorId, actor, new string('\u0001', 200));
        fact.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), PlatformOperator.IdentityOf(user), 1,
            new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)));
        var scenario = new AggregateScenario<PlatformOperator>(new PlatformOperator(user, operation));

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(() => scenario.Given(fact));
    }

    [Fact]
    public void ShouldPreserveTrimmedReasonSemanticsGivenLongSurroundingWhitespace()
    {
        // Arrange
        var user = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var operation = Uuid.CreateVersion4();
        var owner = new PlatformOperator(user, operation);
        var scenario = new AggregateScenario<PlatformOperator>(owner);
        var reason = new string(' ', 10_000) + new string('r', 500) + new string(' ', 10_000);

        // Act
        var result = owner.Grant(operation, 0, actorId, new ActorAttribution(actorId.ToString(), "bdgrz"), reason);

        // Assert
        Assert.True(result.IsSuccess);
        var fact = Assert.IsType<PlatformOperatorAccessGranted>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(new string('r', 500), fact.Reason);
        Assert.True(DomainEventPayload.Size(fact) <= PlatformOperatorContentBounds.EventPayloadBytes);
    }
}
