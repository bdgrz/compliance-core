using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class IndependenceRuleRatificationCatalogOwnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAcceptOnlyOwnRootGivenPreparedFirstRatification(bool foreignCause)
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, actor, _) = IndependenceCatalogNativeBoundaryTests.Authority();
        var command = Uuid.CreateVersion4();
        var ratificationId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var pure = new IndependenceRuleRatificationRules();
        var digest = new string('a', 64);
        var preparedResult = pure.Ratify(command, ratificationId, 1, 1, 0, digest, "Evidence", staff, duty, actor, now);
        Assert.True(preparedResult.IsSuccess);
        var owner = new IndependenceRuleRatificationCatalog(command);
        var scenario = new AggregateScenario<IndependenceRuleRatificationCatalog>(owner);
        var request = new RatifyIndependenceRuleVersion(command, ratificationId, 1, 1, 0, digest, "Evidence", 0);
        var operation = OwnerProvenance.For(owner, command, nameof(RatifyIndependenceRuleVersion), actor,
            owner.Id.ToString(), 0, AuthoredIntentDigest.Compute(request), now) with
        {
            FirstOwner = owner.Stream,
            CausationEventId = foreignCause ? Uuid.CreateVersion4() : null,
        };
        var content = new IndependenceRatificationContent(owner.Stream, command, 1, preparedResult.Value, null);
        var reference = await fixture.Content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(),
            IndependenceCatalogContent.RatificationSchema, content);
        var frozen = await IndependenceCatalogContent.FreezeResultAsync(fixture.Content,
            IndependenceCatalogContent.RatificationResultSchema, content.Ratification);
        var first = await FirstResponses.FreezeAsync(fixture.Content, IndependenceCatalogContent.Realm, frozen.Response);
        var history = Array.Empty<IndependenceRatificationHistoryRead>();
        var fact = new IndependenceRuleRatificationRecorded(IndependenceCatalogContent.Header(content), reference, 1,
            operation, first, frozen.Result);

        // Act
        var result = owner.Append(fact, content, history, 0, 0);

        // Assert
        Assert.Equal(!foreignCause, result.IsSuccess);
        if (foreignCause)
            Assert.Empty(scenario.PendingEvents);
        else
            Assert.Single(scenario.PendingEvents);
        Assert.Equal(foreignCause ? 0L : 1L, owner.Sequence);
    }
}
