using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceRuleCatalogLifetimeTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Operator = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
    static readonly ImmutableContentRef Intent = new(Uuid.CreateVersion4(), new string('1', 64), 2, "application/json",
        "source-use-authorization-intent.v1");

    [Fact]
    public async Task ShouldAcceptVersionsBeyondFormerCapGivenNoLifetimeLimit()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var rules = new IndependenceRuleCatalogRules();

        // Act
        var results = Enumerable.Range(1, 105).Select(index => rules.ReviseRules(Uuid.CreateVersion4(),
            rules.Sequence, Rules($"Synthetic source {index}"), Operator, Now.AddMinutes(index))).ToArray();
        await fixture.SeedAsync(rules);
        var owner = await fixture.Reader.HydrateAsync(new IndependenceRuleCatalog());
        var versions = await fixture.Rules.ReadVersionsAsync();
        var current = await fixture.Rules.ReadAsync();

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(105, owner.Sequence);
        Assert.Equal(105, versions.Count);
        Assert.Equal(105, current.Current!.Version);
    }

    [Fact]
    public async Task ShouldAcceptRatificationsBeyondFormerCapGivenNoLifetimeLimit()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, actor) = Ratifier();
        var rules = new IndependenceRuleRatificationRules();

        // Act
        var results = Enumerable.Range(1, 105).Select(version => rules.Ratify(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), version, version, rules.Sequence, new string('a', 64), $"Synthetic source {version}",
            staff, duty, actor, Now.AddMinutes(version))).ToArray();
        await fixture.SeedAsync(rules);
        var owner = await fixture.Reader.HydrateAsync(new IndependenceRuleRatificationCatalog());
        var history = await fixture.Ratifications.ReadHistoryAsync();
        var current = await fixture.Ratifications.ReadAsync();

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(105, owner.Sequence);
        Assert.Equal(105, history.Count);
        Assert.Equal(105, current.Active!.RuleVersion);
    }

    [Fact]
    public async Task ShouldRefuseNewVersionGivenHeldUseAndAcceptAfterSettlement()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var pure = new IndependenceRuleCatalogRules();
        Assert.True(pure.ReviseRules(Uuid.CreateVersion4(), 0, Rules("Synthetic source"), Operator, Now).IsSuccess);
        await fixture.SeedAsync(pure);
        var catalog = await fixture.Reader.HydrateAsync(new IndependenceRuleCatalog());
        var root = Uuid.CreateVersion4();

        // Act
        var granted = catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        await fixture.Writer.SaveAsync(catalog, new RequestDispatchContext(RequestActor.System));
        var refused = catalog.PrepareRevision(catalog.Sequence);
        var settled = catalog.SettleEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        await fixture.Writer.SaveAsync(catalog, new RequestDispatchContext(RequestActor.System));
        var request = new ReviseIndependenceRules(Uuid.CreateVersion4(), catalog.Sequence, Rules("Another"));
        var accepted = await fixture.Operations().ReviseAsync(IndependenceCatalogSourceFixture.Context(request, Uuid.Parse(Operator.Id, System.Globalization.CultureInfo.InvariantCulture)), CancellationToken.None);
        var persisted = await fixture.Reader.HydrateAsync(new IndependenceRuleCatalog());

        // Assert
        Assert.True(granted.IsSuccess, granted.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(2, persisted.Sequence);
        Assert.False(persisted.Uses.HasActiveUse);
        Assert.Equal(4, persisted.NativeSequence);
        Assert.Equal(4UL, persisted.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRefuseNewRatificationGivenHeldUseAndAcceptAfterSettlement()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var (staff, duty, actor) = Ratifier();
        var pure = new IndependenceRuleRatificationRules();
        Assert.True(pure.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, 0, new string('a', 64),
            "Synthetic source", staff, duty, actor, Now).IsSuccess);
        await fixture.SeedAsync(pure);
        var catalog = await fixture.Reader.HydrateAsync(new IndependenceRuleRatificationCatalog());
        var root = Uuid.CreateVersion4();

        // Act
        var granted = catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        await fixture.Writer.SaveAsync(catalog, new RequestDispatchContext(RequestActor.System));
        var refused = catalog.PrepareRatification(1, 2);
        var settled = catalog.SettleEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        await fixture.Writer.SaveAsync(catalog, new RequestDispatchContext(RequestActor.System));
        var accepted = catalog.PrepareRatification(1, 2);
        var next = (await fixture.Ratifications.ReadAsync()).Rules;
        Assert.True(next.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1, new string('b', 64),
            "Synthetic next", staff, duty, actor, Now).IsSuccess);
        await fixture.SeedAsync(next);
        var persisted = await fixture.Reader.HydrateAsync(new IndependenceRuleRatificationCatalog());

        // Assert
        Assert.True(granted.IsSuccess, granted.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(2, persisted.Sequence);
        Assert.Equal(4, persisted.NativeSequence);
        Assert.Equal(4UL, persisted.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldFenceLateGrantGivenSettlementBeforeAnyGrant()
    {
        // Arrange
        await using var fixture = new IndependenceCatalogSourceFixture();
        var pure = new IndependenceRuleCatalogRules();
        Assert.True(pure.ReviseRules(Uuid.CreateVersion4(), 0, Rules("Synthetic source"), Operator, Now).IsSuccess);
        await fixture.SeedAsync(pure);
        var catalog = await fixture.Reader.HydrateAsync(new IndependenceRuleCatalog());
        var root = Uuid.CreateVersion4();
        var settle = Provenance(catalog.Stream, Uuid.CreateVersion4(), root);

        // Act
        var settled = catalog.SettleEngagementUse(settle, Intent, 1);
        await fixture.Writer.SaveAsync(catalog, new RequestDispatchContext(RequestActor.System));

        // Assert
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.False(catalog.Uses.HasActiveUse);
        Assert.Throws<InvalidOperationException>(() =>
            catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1));
        Assert.Equal(2, (await fixture.FactsAsync(catalog.Stream)).Count);
    }

    static IndependenceRuleContent Rules(string source) => new(12,
        [new IndependenceServiceRuleContent("design", "impairing", "impairing")], source);

    static (FirmStaffMemberView Staff, FirmProfessionalDutyDesignationView Duty, ActorReference Actor) Ratifier()
    {
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest",
            "Synthetic staff source", true, 4, Operator, Now);
        var duty = new FirmProfessionalDutyDesignationView(Uuid.CreateVersion4(), staff.StaffMemberId, staff.UserId,
            FirmProfessionalDuty.RuleRatifier, null, "Synthetic duty evidence", staff.Revision, true, 1, Operator, Now,
            Operator, Now, null);
        return (staff, duty, new ActorReference("firm_staff", staff.UserId.ToString(), "Synthetic ratifier"));
    }

    static OperationProvenance Provenance(EventStreamAddress owner, Uuid commandId, Uuid root) => new(commandId, root,
        null, "AuthorizeEngagementUse", Operator, owner, "initial", null, new string('a', 64), Now, owner, owner);
}
