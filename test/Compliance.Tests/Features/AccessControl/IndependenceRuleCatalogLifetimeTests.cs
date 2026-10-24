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
    public void ShouldAcceptVersionsBeyondFormerCapGivenNoLifetimeLimit()
    {
        // Arrange
        var catalog = new IndependenceRuleCatalog();

        // Act
        var results = Enumerable.Range(1, 105).Select(index => catalog.ReviseRules(Uuid.CreateVersion4(),
            catalog.Sequence, Rules($"Synthetic source {index}"), Operator, Now.AddMinutes(index))).ToArray();

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(105, catalog.Sequence);
        Assert.Equal(105, catalog.Versions.Count);
        Assert.Equal(105, catalog.Current!.Version);
    }

    [Fact]
    public void ShouldAcceptRatificationsBeyondFormerCapGivenNoLifetimeLimit()
    {
        // Arrange
        var (staff, duty, actor) = Ratifier();
        var catalog = new IndependenceRuleRatificationCatalog();

        // Act
        var results = Enumerable.Range(1, 105).Select(version => catalog.Ratify(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), version, version, catalog.Sequence, new string('a', 64), $"Synthetic source {version}",
            staff, duty, actor, Now.AddMinutes(version))).ToArray();

        // Assert
        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error?.Message));
        Assert.Equal(105, catalog.Sequence);
        Assert.Equal(105, catalog.Ratifications.Count);
        Assert.Equal(105, catalog.Active!.RuleVersion);
    }

    [Fact]
    public void ShouldRefuseNewVersionGivenHeldUseAndAcceptAfterSettlement()
    {
        // Arrange
        var catalog = new IndependenceRuleCatalog();
        Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 0, Rules("Synthetic source"), Operator, Now).IsSuccess);
        var root = Uuid.CreateVersion4();

        // Act
        var granted = catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        var refused = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, Rules("Another"), Operator, Now);
        var settled = catalog.SettleEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        var accepted = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, Rules("Another"), Operator, Now);

        // Assert
        Assert.True(granted.IsSuccess, granted.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(2, catalog.Sequence);
        Assert.False(catalog.Uses.HasActiveUse);
    }

    [Fact]
    public void ShouldRefuseNewRatificationGivenHeldUseAndAcceptAfterSettlement()
    {
        // Arrange
        var (staff, duty, actor) = Ratifier();
        var catalog = new IndependenceRuleRatificationCatalog();
        Assert.True(catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1, 0, new string('a', 64),
            "Synthetic source", staff, duty, actor, Now).IsSuccess);
        var root = Uuid.CreateVersion4();

        // Act
        var granted = catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        var refused = catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1, new string('b', 64),
            "Synthetic next", staff, duty, actor, Now);
        var settled = catalog.SettleEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1);
        var accepted = catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 2, 2, 1, new string('b', 64),
            "Synthetic next", staff, duty, actor, Now);

        // Assert
        Assert.True(granted.IsSuccess, granted.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.True(refused.Error!.IsTransient);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(2, catalog.Sequence);
    }

    [Fact]
    public void ShouldFenceLateGrantGivenSettlementBeforeAnyGrant()
    {
        // Arrange
        var catalog = new IndependenceRuleCatalog();
        Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 0, Rules("Synthetic source"), Operator, Now).IsSuccess);
        var root = Uuid.CreateVersion4();
        var settle = Provenance(catalog.Stream, Uuid.CreateVersion4(), root);

        // Act
        var settled = catalog.SettleEngagementUse(settle, Intent, 1);

        // Assert
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.False(catalog.Uses.HasActiveUse);
        Assert.Throws<InvalidOperationException>(() =>
            catalog.AuthorizeEngagementUse(Provenance(catalog.Stream, Uuid.CreateVersion4(), root), Intent, 1));
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
