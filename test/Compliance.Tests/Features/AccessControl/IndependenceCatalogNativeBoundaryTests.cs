using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceCatalogNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyCurrentBusinessAuthorityGivenLongCatalogAndPersonalRatificationHistory()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, ratifier, actor) = Authority();
        var pureRules = new IndependenceRuleCatalogRules();
        var pureDecisions = new IndependenceRuleRatificationRules();
        for (var index = 0; index < 105; index++)
        {
            var value = new IndependenceRuleContent(12, [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Rules " + index);
            Assert.True(pureRules.ReviseRules(Uuid.CreateVersion4(), pureRules.Sequence, value, actor, DateTimeOffset.UtcNow).IsSuccess);
            Assert.True(pureDecisions.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), index + 1, index + 1, pureDecisions.Sequence,
                IndependenceSourceDigest.RuleContent(value), "Personal evidence", staff, duty, ratifier, DateTimeOffset.UtcNow).IsSuccess);
        }
        await fixture.SeedAsync(pureRules);
        await fixture.SeedAsync(pureDecisions);

        // Act
        var rules = await fixture.Reader.HydrateAsync(new IndependenceRuleCatalog());
        var decisions = await fixture.Reader.HydrateAsync(new IndependenceRuleRatificationCatalog());
        var history = new object[] { rules, decisions }.SelectMany(owner => owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 })).ToArray();
        var versions = await fixture.Rules.ReadVersionsAsync();
        var ratifications = await fixture.Ratifications.ReadHistoryAsync();

        // Assert
        Assert.Empty(history);
        Assert.Equal(105, rules.Sequence);
        Assert.Equal(105, decisions.Sequence);
        Assert.Equal(105, versions.Count);
        Assert.Equal(105, ratifications.Count);
        Assert.Equal(105, (await fixture.Ratifications.ReadAsync()).Active!.RuleVersion);
        Assert.All(await fixture.FactsAsync(rules.Stream), fact => Assert.True(DomainEventPayload.Size(fact) < 12 * 1024));
        Assert.All(await fixture.FactsAsync(decisions.Stream), fact => Assert.True(DomainEventPayload.Size(fact) < 12 * 1024));
    }

    [Fact]
    public async Task ShouldAcceptCompleteMaximumClassifiedRuleValueGivenImmutableRuleStorage()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var user = Uuid.CreateVersion4();
        var content = new IndependenceRuleContent(12, Enumerable.Range(0, 100).Select(index =>
            new IndependenceServiceRuleContent(index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + new string('界', 97),
                "conditionally_compatible", "impairing")).ToArray(), new string('界', 2000));
        var request = new ReviseIndependenceRules(Uuid.CreateVersion4(), 0, content);

        // Act
        var result = await fixture.Operations().ReviseAsync(IndependenceCatalogSourceFixture.Context(request, user), CancellationToken.None);
        var source = await fixture.Rules.ReadAsync();
        var fact = Assert.IsType<IndependenceRulesRevised>(Assert.Single(await fixture.FactsAsync(source.Owner.Stream)));
        var response = await FirstResponses.ReplayAsync(fixture.Content, IndependenceCatalogContent.Realm, fact.FirstResponse);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, source.Sequence);
        Assert.Equal(content.SourceReference, source.Current!.Content.SourceReference);
        Assert.Equal(content.ServiceRules[99], source.Current.Content.ServiceRules[99]);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(source.Content, ComplianceCoreJsonContext.Default.IndependenceRuleVersionContent).Length > DomainEventPayload.MaximumBytes);
        Assert.True(DomainEventPayload.Size(fact) < 12 * 1024);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(IndependenceCatalogContent.RuleResultSchema, response.ResultSchema);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseHistoricalIdentityReuseOrUnprovenAbsenceGivenNativeRatificationAppend(bool reuse)
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, actor, _) = Authority();
        var pure = new IndependenceRuleRatificationRules();
        var historicalId = Uuid.CreateVersion4();
        Assert.True(pure.Ratify(Uuid.CreateVersion4(), historicalId, 1, 1, 0, new string('a', 64), "First evidence", staff, duty, actor, DateTimeOffset.UtcNow).IsSuccess);
        Assert.True(pure.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1, new string('b', 64), "Second evidence", staff, duty, actor, DateTimeOffset.UtcNow).IsSuccess);
        await fixture.SeedAsync(pure);
        var command = Uuid.CreateVersion4();
        var ratificationId = reuse ? historicalId : Uuid.CreateVersion4();
        var owner = await fixture.Reader.HydrateAsync(new IndependenceRuleRatificationCatalog(command));
        var before = await fixture.Ratifications.ReadAsync(owner);
        var next = before.Rules;
        var accepted = next.Ratify(command, ratificationId, 3, 3, 2, new string('c', 64), "Third evidence", staff, duty, actor, DateTimeOffset.UtcNow);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var request = new RatifyIndependenceRuleVersion(command, ratificationId, 3, 3, 2, new string('c', 64), "Third evidence", 2);
        var operation = OwnerProvenance.For(owner, command, nameof(RatifyIndependenceRuleVersion), actor, owner.Id.ToString(), 2,
            AuthoredIntentDigest.Compute(request), accepted.Value.RecordedAt) with
        { FirstOwner = owner.Stream };
        var node = new IndependenceRatificationContent(owner.Stream, command, 3, accepted.Value, owner.CurrentContent);
        var reference = await fixture.Content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RatificationSchema, node);
        var result = await IndependenceCatalogContent.FreezeResultAsync(fixture.Content, IndependenceCatalogContent.RatificationResultSchema, node.Ratification);
        var first = await FirstResponses.FreezeAsync(fixture.Content, IndependenceCatalogContent.Realm, result.Response);
        var proof = await fixture.Ratifications.IdentityEvidenceAsync(before, ratificationId);
        var supplied = reuse ? proof : proof.Take(1).ToArray();

        // Act
        var rejected = owner.Append(new IndependenceRuleRatificationRecorded(IndependenceCatalogContent.Header(node), reference, owner.NativeSequence + 1,
            operation, first, result.Result), node, supplied, owner.NativeSequence, owner.CommittedStreamPosition);

        // Assert
        Assert.False(rejected.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, rejected.Error?.Kind);
        Assert.False(rejected.Error!.IsTransient);
        Assert.Equal(2, owner.Sequence);
        Assert.Empty(new AggregateScenario<IndependenceRuleRatificationCatalog>(owner).PendingEvents);
        Assert.Equal(2, (await fixture.FactsAsync(owner.Stream)).Count);
    }

    [Fact]
    public async Task ShouldRefuseAuthoredIntentMismatchGivenPreparedNativeRuleBody()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var user = Uuid.CreateVersion4();
        var command = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForPlatformOperator(user, "Operator");
        var owner = new IndependenceRuleCatalog(command);
        var original = new IndependenceRuleContent(12, [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")], "Authored source");
        var request = new ReviseIndependenceRules(command, 0, original);
        var operation = OwnerProvenance.For(owner, command, nameof(ReviseIndependenceRules), actor, owner.Id.ToString(), 0,
            AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = owner.Stream };
        var changed = new IndependenceRuleVersionView(1, original with { SourceReference = "Different stored source" }, actor, now, false);
        var node = new IndependenceRuleVersionContent(owner.Stream, command, 1, changed, null);
        var reference = await fixture.Content.WriteJsonAsync(IndependenceCatalogContent.Realm, Uuid.CreateVersion4(), IndependenceCatalogContent.RuleSchema, node);
        var response = await IndependenceCatalogContent.FreezeResultAsync(fixture.Content, IndependenceCatalogContent.RuleResultSchema, changed);
        var first = await FirstResponses.FreezeAsync(fixture.Content, IndependenceCatalogContent.Realm, response.Response);

        // Act
        var outcome = owner.Append(new IndependenceRulesRevised(IndependenceCatalogContent.Header(node), reference, 1, operation, first,
            response.Result), node, 0, 0);

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, outcome.Error?.Kind);
        Assert.False(outcome.Error!.IsTransient);
        Assert.Equal(0, owner.Sequence);
        Assert.Empty(new AggregateScenario<IndependenceRuleCatalog>(owner).PendingEvents);
    }

    internal static (FirmStaffMemberView Staff, FirmProfessionalDutyDesignationView Duty, ActorReference Ratifier, ActorReference Operator) Authority()
    {
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator");
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest", "Staff source", true, 1, actor, now);
        var duty = new FirmProfessionalDutyDesignationView(Uuid.CreateVersion4(), staff.StaffMemberId, staff.UserId, FirmProfessionalDuty.RuleRatifier,
            null, "Duty evidence", 1, true, 1, actor, now, actor, now, null);
        return (staff, duty, ActorReference.ForFirmStaff(staff.UserId, "Ratifier"), actor);
    }
}
