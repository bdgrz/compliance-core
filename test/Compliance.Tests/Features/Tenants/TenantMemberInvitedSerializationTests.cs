using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantMemberInvitedSerializationTests
{
    [Fact]
    public void ShouldPreserveGuestScopeAndInvitationAttributionGivenEventRoundTrip()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var invitedBy = Uuid.CreateVersion4();
        var programs = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var invitedAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var original = new TenantMemberInvited(tenantId, "guest@example.com", "guest", false,
            new string('A', 64), invitedAt.AddDays(7), invitedBy,
            GuestProgramIds: programs, GuestExpiresOn: new DateOnly(2026, 11, 30),
            InvitedAt: invitedAt, InvitedByDisplay: "Org Admin");
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Act
        var replayed = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal("guest", replayed.Affiliation);
        Assert.Equal(programs, replayed.GuestProgramIds);
        Assert.Equal(new DateOnly(2026, 11, 30), replayed.GuestExpiresOn);
        Assert.Equal(invitedAt, replayed.InvitedAt);
        Assert.Equal(invitedBy, replayed.InvitedBy);
        Assert.Equal("Org Admin", replayed.InvitedByDisplay);
    }

    [Fact]
    public void ShouldPreserveGuestTermsGivenAcceptanceEventRoundTrip()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var invitedBy = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var acceptedAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var original = new TenantInvitationAccepted(tenantId, userId, "guest@example.com",
            "guest", false, GuestProgramIds: [programId],
            GuestExpiresOn: new DateOnly(2026, 11, 30), AcceptedAt: acceptedAt,
            InvitedBy: invitedBy, InvitedByDisplay: "Org Admin");
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.TenantInvitationAccepted);

        // Act
        var replayed = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.TenantInvitationAccepted);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal("guest", replayed.Affiliation);
        Assert.Equal([programId], replayed.GuestProgramIds);
        Assert.Equal(new DateOnly(2026, 11, 30), replayed.GuestExpiresOn);
        Assert.Equal(acceptedAt, replayed.AcceptedAt);
        Assert.Equal(invitedBy, replayed.InvitedBy);
        Assert.Equal("Org Admin", replayed.InvitedByDisplay);
    }

    [Fact]
    public void ShouldReplayHistoricalInvitationGivenMissingDeliveryFields()
    {
        // Arrange
        var original = new TenantMemberInvited(Uuid.CreateVersion4(), "member@example.com",
            "client_personnel", false, new string('A', 64), DateTimeOffset.UtcNow.AddDays(7),
            Uuid.CreateVersion4());
        var json = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.TenantMemberInvited);
        var document = JsonNode.Parse(json)!.AsObject();
        Assert.True(document.Remove("delivery_attempt_id"));
        Assert.True(document.Remove("token_key_id"));

        // Act
        var replayed = JsonSerializer.Deserialize(document.ToJsonString(),
            ComplianceCoreJsonContext.Default.TenantMemberInvited);

        // Assert
        Assert.NotNull(replayed);
        Assert.Equal(original, replayed);
        Assert.Null(replayed.DeliveryAttemptId);
        Assert.Null(replayed.TokenKeyId);
        Assert.NotEqual(Uuid.Empty, TenantInvitation.DeliveryAttemptFor(replayed));
    }
}
