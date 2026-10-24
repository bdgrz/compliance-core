using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationMailboxClaimTests
{
    static readonly DateTimeOffset At = TenantInvitationFacts.At;

    [Fact]
    public void ShouldKeepOneStreamPerMailboxGivenEquivalentEmailSpellings()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();

        // Act
        var plain = new TenantInvitationMailboxClaim(tenantId, "member@example.com");
        var spelled = new TenantInvitationMailboxClaim(tenantId, " MEMBER@example.com ");
        var other = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");

        // Assert
        Assert.Equal(plain.Stream, spelled.Stream);
        Assert.NotEqual(plain.Stream, other.Stream);
    }

    [Fact]
    public void ShouldClaimWithFirstGenerationGivenFreeMailbox()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var scenario = new AggregateScenario<TenantInvitationMailboxClaim>(claim);
        var invitationId = Uuid.CreateVersion4();

        // Act
        var result = claim.Claim(invitationId, At);

        // Assert
        Assert.Equal(1, result.Value);
        Assert.Equal(invitationId, claim.Holder);
        var claimed = Assert.IsType<TenantInvitationMailboxClaimed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(1, claimed.Generation);
        Assert.Equal("member@example.com", claimed.EmailAddress);
    }

    [Fact]
    public void ShouldKeepGenerationAndAppendNothingGivenHolderClaimsAgain()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var scenario = new AggregateScenario<TenantInvitationMailboxClaim>(claim);
        var invitationId = Uuid.CreateVersion4();
        Assert.True(claim.Claim(invitationId, At).IsSuccess);

        // Act
        var again = claim.Claim(invitationId, At.AddMinutes(1));

        // Assert
        Assert.Equal(1, again.Value);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldConflictGivenAnotherInvitationHoldsTheMailbox()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var scenario = new AggregateScenario<TenantInvitationMailboxClaim>(claim);
        var holder = Uuid.CreateVersion4();
        Assert.True(claim.Claim(holder, At).IsSuccess);

        // Act
        var other = claim.Claim(Uuid.CreateVersion4(), At.AddMinutes(1));
        var wrongGeneration = claim.Claim(Uuid.CreateVersion4(), At.AddMinutes(1), holder, 7);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, other.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongGeneration.Error?.Kind);
        Assert.Equal(holder, claim.Holder);
        Assert.Single(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReleaseOldHolderAndClaimNextGenerationGivenExactSupersession()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var scenario = new AggregateScenario<TenantInvitationMailboxClaim>(claim);
        var holder = Uuid.CreateVersion4();
        var next = Uuid.CreateVersion4();
        Assert.True(claim.Claim(holder, At).IsSuccess);

        // Act
        var result = claim.Claim(next, At.AddDays(8), holder, 1);

        // Assert
        Assert.Equal(2, result.Value);
        Assert.Equal(next, claim.Holder);
        Assert.Equal(2, claim.HolderGeneration);
        Assert.Collection(scenario.PendingEvents,
            first => Assert.IsType<TenantInvitationMailboxClaimed>(first),
            second => Assert.Equal(TenantInvitationMailboxClaim.SupersededReason,
                Assert.IsType<TenantInvitationMailboxReleased>(second).Reason),
            third => Assert.Equal(2, Assert.IsType<TenantInvitationMailboxClaimed>(third).Generation));
    }

    [Fact]
    public void ShouldAllowLaterInvitationGivenAcceptedInvitationReleased()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var accepted = Uuid.CreateVersion4();
        Assert.True(claim.Claim(accepted, At).IsSuccess);
        Assert.True(claim.Release(accepted, 1, TenantInvitationMailboxClaim.AcceptedReason, At.AddMinutes(1)).IsSuccess);

        // Act
        var later = claim.Claim(Uuid.CreateVersion4(), At.AddMinutes(2));

        // Assert
        Assert.Equal(2, later.Value);
    }

    [Fact]
    public void ShouldIgnoreReleaseGivenOldGenerationOrOtherInvitation()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");
        var scenario = new AggregateScenario<TenantInvitationMailboxClaim>(claim);
        var old = Uuid.CreateVersion4();
        var current = Uuid.CreateVersion4();
        Assert.True(claim.Claim(old, At).IsSuccess);
        Assert.True(claim.Claim(current, At.AddDays(8), old, 1).IsSuccess);

        // Act
        var lateOld = claim.Release(old, 1, TenantInvitationMailboxClaim.AcceptedReason, At.AddDays(9));
        var wrongGeneration = claim.Release(current, 1, TenantInvitationMailboxClaim.AcceptedReason, At.AddDays(9));
        var again = claim.Release(Uuid.CreateVersion4(), 2, TenantInvitationMailboxClaim.AcceptedReason, At.AddDays(9));

        // Assert
        Assert.True(lateOld.IsSuccess);
        Assert.True(wrongGeneration.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Equal(current, claim.Holder);
        Assert.Equal(3, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectReleaseGivenMissingReasonOrGeneration()
    {
        // Arrange
        var claim = new TenantInvitationMailboxClaim(Uuid.CreateVersion4(), "member@example.com");

        // Act
        var noReason = claim.Release(Uuid.CreateVersion4(), 1, " ", At);
        var noGeneration = claim.Release(Uuid.CreateVersion4(), 0, "accepted", At);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, noReason.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, noGeneration.Error?.Kind);
    }
}
