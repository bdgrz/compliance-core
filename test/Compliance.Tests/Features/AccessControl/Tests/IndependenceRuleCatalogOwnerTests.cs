using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class IndependenceRuleCatalogOwnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireRootProvenanceGivenPreparedRuleRevision(bool hasCausation)
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var command = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator");
        var owner = new IndependenceRuleCatalog(command);
        var scenario = new AggregateScenario<IndependenceRuleCatalog>(owner);
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Authored source");
        var request = new ReviseIndependenceRules(command, 0, rules);
        var operation = OwnerProvenance.For(owner, command, nameof(ReviseIndependenceRules), actor, owner.Id.ToString(),
            0, AuthoredIntentDigest.Compute(request), now) with
        {
            FirstOwner = owner.Stream,
            CausationEventId = hasCausation ? Uuid.CreateVersion4() : null,
        };
        var version = new IndependenceRuleVersionView(1, rules, actor, now, false);
        var node = new IndependenceRuleVersionContent(owner.Stream, command, 1, version, null);
        var reference = await fixture.Content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(),
            IndependenceCatalogContent.RuleSchema, node);
        var response = await IndependenceCatalogContent.FreezeResultAsync(fixture.Content,
            IndependenceCatalogContent.RuleResultSchema, version);
        var first = await FirstResponses.FreezeAsync(fixture.Content, IndependenceCatalogContent.Realm, response.Response);
        var fact = new IndependenceRulesRevised(IndependenceCatalogContent.Header(node), reference, 1, operation,
            first, response.Result);

        // Act
        var result = owner.Append(fact, node, 0, 0);

        // Assert
        if (hasCausation)
        {
            Assert.False(result.IsSuccess);
            Assert.Empty(scenario.PendingEvents);
            Assert.Equal(0, owner.Sequence);
        }
        else
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Single(scenario.PendingEvents);
            Assert.Equal(1, owner.Sequence);
        }
    }
}
