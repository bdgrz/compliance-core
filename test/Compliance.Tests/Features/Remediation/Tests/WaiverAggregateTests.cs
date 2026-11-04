using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class WaiverAggregateTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly ActorReference Requester = ActorReference.ForMember(Uuid.CreateVersion4(), "Contributor");
    static readonly ActorReference Reviewer = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequestExactProductRuleExceptionGivenValidTerms()
    {
        // Arrange
        var request = Request();
        var owner = new Waiver(Tenant, request.WaiverId);

        // Act
        var result = owner.Request(request, Requester, Now);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(request.WaiverId, result.Value.WaiverId);
        var fact = Assert.IsType<WaiverRequested>(Assert.Single(Pending(owner)));
        Assert.Equal(request.Rule, fact.Rule);
        Assert.Equal(request.Scope, fact.Scope);
        Assert.Equal(Requester, fact.RequestedBy);
        Assert.Equal(request.CompensatingAction, fact.CompensatingAction);
        var view = Assert.IsType<WaiverView>(owner.ViewOrNull(Now));
        Assert.Equal("requested", view.State);
        Assert.False(view.IsEffective);
    }

    [Theory]
    [InlineData("rule")]
    [InlineData("scope")]
    [InlineData("record")]
    [InlineData("revision")]
    [InlineData("duplicate")]
    [InlineData("rationale")]
    [InlineData("compensation")]
    [InlineData("expires_now")]
    [InlineData("expired")]
    public void ShouldRefuseWithoutFactGivenInvalidRequest(string fault)
    {
        // Arrange
        var request = Request();
        request = fault switch
        {
            "rule" => request with { Rule = "separation_of_duties" },
            "scope" => request with { Scope = [] },
            "record" => request with { Scope = [request.Scope[0] with { RecordId = Uuid.Empty }] },
            "revision" => request with { Scope = [request.Scope[0] with { Revision = 0 }] },
            "duplicate" => request with { Scope = [request.Scope[0], request.Scope[0]] },
            "rationale" => request with { Rationale = " " },
            "compensation" => request with { CompensatingAction = " " },
            "expires_now" => request with { ExpiresAt = Now },
            _ => request with { ExpiresAt = Now.AddTicks(-1) },
        };
        var owner = new Waiver(Tenant, request.WaiverId);

        // Act
        var result = owner.Request(request, Requester, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Null(owner.ViewOrNull(Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseSelfReviewWithoutFactGivenRequesterAsReviewer(bool approve)
    {
        // Arrange
        var (owner, request) = Requested();
        var review = new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, approve);

        // Act
        var result = owner.Review(review, Requester, Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Empty(Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldCoverOnlyExactApprovedScopeGivenIndependentApproval()
    {
        // Arrange
        var (owner, request) = Requested();
        var approvedAt = Now.AddMinutes(1);
        var review = new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, true);

        // Act
        var result = owner.Review(review, Reviewer, approvedAt);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.IsType<WaiverApproved>(Assert.Single(Pending(owner)));
        Assert.True(owner.Covers(request.Rule, request.Scope, approvedAt));
        Assert.False(owner.Covers(request.Rule, request.Scope, approvedAt.AddTicks(-1)));
        Assert.False(owner.Covers("control_occurrence", request.Scope, approvedAt));
        Assert.False(owner.Covers(request.Rule, [request.Scope[0] with { RecordId = Uuid.CreateVersion4() }], approvedAt));
        Assert.False(owner.Covers(request.Rule, [request.Scope[0] with { Revision = 2 }], approvedAt));
        Assert.False(owner.Covers(request.Rule, request.Scope, request.ExpiresAt));
        Assert.Equal("expired", owner.ViewOrNull(request.ExpiresAt)!.State);
        Assert.Single(Pending(owner));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    public void ShouldRefuseReviewWithoutFactGivenExpiryReached(bool approve, int ticksAfter)
    {
        // Arrange
        var (owner, request) = Requested();
        var review = new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, approve);

        // Act
        var result = owner.Review(review, Reviewer, request.ExpiresAt.AddTicks(ticksAfter));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldRemainUncoveredAndTerminalGivenIndependentRejection()
    {
        // Arrange
        var (owner, request) = Requested();
        var review = new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, false, "Use the ordinary rule.");
        Assert.True(owner.Review(review, Reviewer, Now.AddMinutes(1)).IsSuccess);
        Commit(owner);

        // Act
        var approval = owner.Review(review with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, Approve = true },
            Reviewer, Now.AddMinutes(2));
        var revocation = owner.Revoke(new RevokeWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 2, "Reason"),
            Reviewer, Now.AddMinutes(2));

        // Assert
        Assert.False(approval.IsSuccess);
        Assert.False(revocation.IsSuccess);
        Assert.False(owner.Covers(request.Rule, request.Scope, Now.AddMinutes(3)));
        Assert.Equal("rejected", owner.ViewOrNull(Now.AddMinutes(3))!.State);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldStopCoverageGivenRevocationWithReason()
    {
        // Arrange
        var (owner, request, _) = Approved();
        var revoke = new RevokeWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 2, "Compensating action ended.");

        // Act
        var result = owner.Revoke(revoke, Reviewer, Now.AddMinutes(2));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<WaiverRevoked>(Assert.Single(Pending(owner)));
        Assert.Equal(revoke.Reason, fact.Reason);
        Assert.False(owner.Covers(request.Rule, request.Scope, Now.AddMinutes(2)));
        Assert.Equal("revoked", owner.ViewOrNull(Now.AddMinutes(2))!.State);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("expiry")]
    [InlineData("stale")]
    public void ShouldRefuseRevocationWithoutFactGivenInvalidCurrentDecision(string fault)
    {
        // Arrange
        var (owner, request, _) = Approved();
        var revoke = new RevokeWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId,
            fault == "stale" ? 1 : 2, fault == "reason" ? " " : "Reason");
        var at = fault == "expiry" ? request.ExpiresAt : Now.AddMinutes(2);

        // Act
        var result = owner.Revoke(revoke, Reviewer, at);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldKeepScopeImmutableGivenCallerCollectionMutation()
    {
        // Arrange
        var request = Request();
        var original = request.Scope.ToArray();
        var callerScope = request.Scope.ToList();
        request = request with { Scope = callerScope };
        var owner = new Waiver(Tenant, request.WaiverId);
        Assert.True(owner.Request(request, Requester, Now).IsSuccess);

        // Act
        callerScope.Clear();

        // Assert
        Assert.Equal(original, owner.ViewOrNull(Now)!.Scope);
        Assert.Equal(original, Assert.IsType<WaiverRequested>(Assert.Single(Pending(owner))).Scope);
    }

    [Fact]
    public void ShouldReplayAcceptedCommandsWithoutFactGivenRevocationAndLaterExpiry()
    {
        // Arrange
        var (owner, request, review) = Approved();
        var revoke = new RevokeWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 2, "Reason");
        Assert.True(owner.Revoke(revoke, Reviewer, Now.AddMinutes(2)).IsSuccess);
        Commit(owner);

        // Act
        var requested = owner.Request(request, Requester, request.ExpiresAt.AddDays(1));
        var reviewed = owner.Review(review, Reviewer, request.ExpiresAt.AddDays(1));
        var revoked = owner.Revoke(revoke, Reviewer, request.ExpiresAt.AddDays(1));

        // Assert
        Assert.True(requested.IsSuccess);
        Assert.True(reviewed.IsSuccess);
        Assert.True(revoked.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Equal(3, owner.Revision);
    }

    [Theory]
    [InlineData("rationale")]
    [InlineData("scope")]
    [InlineData("actor")]
    public void ShouldRefuseChangedRequestWithoutFactGivenSameWaiverIdentity(string fault)
    {
        // Arrange
        var (owner, request) = Requested();
        var changed = fault switch
        {
            "rationale" => request with { Rationale = "Another reason" },
            "scope" => request with { Scope = [request.Scope[0] with { Revision = 2 }] },
            _ => request,
        };

        // Act
        var result = owner.Request(changed, fault == "actor" ? Reviewer : Requester, Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldRefuseChangedDecisionWithoutFactGivenAcceptedReviewIdentity()
    {
        // Arrange
        var (owner, _, review) = Approved();

        // Act
        var result = owner.Review(review with { Approve = false }, Reviewer, Now.AddMinutes(2));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldPreserveCoverageAndHistoryGivenEventReplay()
    {
        // Arrange
        var request = Request();
        var owner = new Waiver(Tenant, request.WaiverId);
        Assert.True(owner.Request(request, Requester, Now).IsSuccess);
        var requested = Assert.Single(Pending(owner));
        Commit(owner);
        Assert.True(owner.Review(new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, true),
            Reviewer, Now.AddMinutes(1)).IsSuccess);
        var approved = Assert.Single(Pending(owner));

        // Act
        var replayed = new AggregateScenario<Waiver>(new Waiver(Tenant, request.WaiverId)).Given(requested, approved).Aggregate;

        // Assert
        Assert.True(replayed.Covers(request.Rule, request.Scope, Now.AddMinutes(2)));
        Assert.Equal(2, replayed.Revision);
        Assert.Empty(Pending(replayed));
    }

    [Fact]
    public void ShouldRefuseSelfReviewGivenEquivalentMemberUuidSpelling()
    {
        // Arrange
        var (owner, request) = Requested();
        var reviewer = Requester with { Id = Requester.Id.ToUpperInvariant() };

        // Act
        var result = owner.Review(new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, true),
            reviewer, Now.AddMinutes(1));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(Pending(owner));
    }

    [Fact]
    public void ShouldRefuseStaleReviewWithoutFactGivenCurrentRequestRevision()
    {
        // Arrange
        var (owner, request) = Requested();

        // Act
        var result = owner.Review(new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 0, true),
            Reviewer, Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(Pending(owner));
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRejectForeignTenantFactGivenReplayIntoAnotherOwner()
    {
        // Arrange
        var request = Request();
        var owner = new Waiver(Tenant, request.WaiverId);
        Assert.True(owner.Request(request, Requester, Now).IsSuccess);
        var fact = Assert.IsType<WaiverRequested>(Assert.Single(Pending(owner)));
        var foreign = fact with { TenantId = Uuid.CreateVersion4() };
        var replay = new AggregateScenario<Waiver>(new Waiver(Tenant, request.WaiverId));

        // Act
        var failure = Record.Exception(() => replay.Given(foreign));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    static RequestWaiver Request() => new(Uuid.CreateVersion4(), Tenant, "data_flow_encryption",
        [new WaiverScopeRecord("data_flow", Uuid.CreateVersion4(), 1)], "Temporary approved exception.",
        "An isolated network provides compensating protection.", Now.AddDays(1));

    static (Waiver Owner, RequestWaiver Request) Requested()
    {
        var request = Request();
        var owner = new Waiver(Tenant, request.WaiverId);
        var result = owner.Request(request, Requester, Now);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Commit(owner);
        return (owner, request);
    }

    static (Waiver Owner, RequestWaiver Request, ReviewWaiver Review) Approved()
    {
        var (owner, request) = Requested();
        var review = new ReviewWaiver(Uuid.CreateVersion4(), Tenant, request.WaiverId, 1, true);
        var result = owner.Review(review, Reviewer, Now.AddMinutes(1));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Commit(owner);
        return (owner, request, review);
    }

    static IReadOnlyList<DomainEvent> Pending(Waiver owner) => new AggregateScenario<Waiver>(owner).PendingEvents;

    static void Commit(Waiver owner) => new AggregateScenario<Waiver>(owner).Given();
}
