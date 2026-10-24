using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationDirectoryTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid InvitedBy = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReplaceEarlierInvitationAndHideTokenGivenLaterInvitationOfTheMailbox()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzTenantInvitationDirectory(client);
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var first = new TenantInvitation(TenantId, firstId);
        var second = new TenantInvitation(TenantId, secondId);
        var firstScenario = new AggregateScenario<TenantInvitation>(first);
        var secondScenario = new AggregateScenario<TenantInvitation>(second);
        Assert.True(first.Invite(firstId, TenantInvitationFacts.Member("invitee@example.com",
            role: BuiltInRbac.ComplianceParticipationRole, invitedBy: InvitedBy), "first-token", Now).IsSuccess);
        Assert.True(second.Invite(secondId, TenantInvitationFacts.Member("invitee@example.com",
            role: BuiltInRbac.ComplianceManagementRole, invitedBy: InvitedBy), "second-token", Now.AddDays(8),
            generation: 2).IsSuccess);
        var firstInvited = firstScenario.PendingEvents.OfType<TenantMemberInvited>().Single();
        var secondInvited = secondScenario.PendingEvents.OfType<TenantMemberInvited>().Single();
        var identity = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(TenantId.ToString()));
        await using (var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(firstInvited);
            await directory.ApplyAsync(secondInvited);
            await directory.ApplyAsync(secondInvited);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var page = await directory.ListAsync(TenantId, 50, null);
        var otherTenant = await directory.ListAsync(Uuid.CreateVersion4(), 50, null);

        // Assert
        var invitation = Assert.Single(page.Items);
        Assert.Equal(secondId, invitation.InvitationId);
        Assert.Equal(Now.AddDays(15), invitation.ExpiresAt);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, invitation.BuiltInRole);
        Assert.Empty(otherTenant.Items);
        var stored = JsonSerializer.Serialize(invitation,
            ComplianceCoreJsonContext.Default.TenantInvitationDirectoryEntry);
        Assert.DoesNotContain("token", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(firstInvited.TokenHash, stored, StringComparison.Ordinal);
        Assert.DoesNotContain(secondInvited.TokenHash, stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldIgnoreAcceptanceOfEarlierInvitationGivenLaterInvitationReplacedIt()
    {
        // Arrange
        var directory = new FitzTenantInvitationDirectory(new InMemoryKvClient());
        var firstId = Uuid.CreateVersion4();
        var secondId = Uuid.CreateVersion4();
        var first = new TenantInvitation(TenantId, firstId);
        var second = new TenantInvitation(TenantId, secondId);
        var firstScenario = new AggregateScenario<TenantInvitation>(first);
        var secondScenario = new AggregateScenario<TenantInvitation>(second);
        Assert.True(first.Invite(firstId, TenantInvitationFacts.Member("invitee@example.com", invitedBy: InvitedBy),
            "first-token", Now).IsSuccess);
        Assert.True(first.Accept(firstId, Uuid.CreateVersion4(), "invitee@example.com", "first-token", Now).IsSuccess);
        Assert.True(second.Invite(secondId, TenantInvitationFacts.Member("invitee@example.com", invitedBy: InvitedBy),
            "second-token", Now.AddDays(1), generation: 2).IsSuccess);
        var identity = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(TenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(firstScenario.PendingEvents.OfType<TenantMemberInvited>().Single());
            await directory.ApplyAsync(secondScenario.PendingEvents.OfType<TenantMemberInvited>().Single());
            await directory.ApplyAsync(firstScenario.PendingEvents.OfType<TenantInvitationAccepted>().Single());
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = Assert.Single((await directory.ListAsync(TenantId, 50, null)).Items);

        // Assert
        Assert.Equal(secondId, current.InvitationId);
        Assert.Null(current.AcceptedUserId);
    }

    [Fact]
    public async Task ShouldProjectGuestScopeReferenceAndRequiredExpiryGivenGuestInvitation()
    {
        // Arrange
        var directory = new FitzTenantInvitationDirectory(new InMemoryKvClient());
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var expiresOn = DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30);
        var invitationId = Uuid.CreateVersion4();
        var aggregate = new TenantInvitation(TenantId, invitationId);
        Assert.True(aggregate.Invite(invitationId, TenantInvitationFacts.Guest(programIds, expiresOn), "guest-token",
            Now).IsSuccess);
        var invited = Assert.IsType<TenantMemberInvited>(
            Assert.Single(new AggregateScenario<TenantInvitation>(aggregate).PendingEvents));
        var identity = new CheckpointIdentity("TenantInvitationDirectory",
            EventStreamPattern.ForPattern(TenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(invited);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var projected = Assert.Single((await directory.ListAsync(TenantId, 50, null)).Items);

        // Assert
        Assert.Equal("guest", projected.Affiliation);
        Assert.Equal(invited.GuestProgramIdsManifest, projected.GuestProgramIdsManifest);
        Assert.Equal(expiresOn, projected.GuestExpiresOn);
    }

    [Fact]
    public async Task ShouldBecomeActiveGivenMembershipWithoutCurrentRoleGrant()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceManagementRole,
            Now.AddDays(7), InvitedBy, userId);
        var reader = new FakeInvitationDirectory(entry);
        var membership = new FakeMembershipDirectory();
        var clock = new FixedTimeProvider(Now);
        var handler = Handler(reader, membership, clock);
        var request = new ListTenantInvitations(TenantId, EmailAddress: "Invitee@Example.com");

        // Act
        var accepted = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.Equal("accepted_pending_activation", Assert.Single(accepted.Value.Items).Status);

        membership.IsMember = true;
        var active = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);
        Assert.Equal("active", Assert.Single(active.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldReportExpiredGivenAcceptedGuestMembershipPastRequiredEndDate()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "guest@example.com",
            "guest", false, null, Now.AddDays(7), InvitedBy, userId,
            await ScopeAsync(Uuid.CreateVersion4()), DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-1));
        var membership = new FakeMembershipDirectory { IsMember = true };
        var handler = Handler(new FakeInvitationDirectory(entry),
            membership, new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            new ListTenantInvitations(TenantId, EmailAddress: entry.EmailAddress), Actor()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("expired", Assert.Single(result.Value.Items).Status);
    }

    [Fact]
    public async Task ShouldKeepAcceptedInvitationActiveGivenSuspendedRegisteredMember()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceManagementRole,
            Now.AddDays(7), InvitedBy, userId);
        var membership = new FakeMembershipDirectory
        {
            IsMember = true,
            IsSuspended = true,
        };
        var handler = Handler(new FakeInvitationDirectory(entry),
            membership, new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: entry.EmailAddress);
        Assert.False(await membership.IsMemberAsync(TenantId.ToString(), userId));
        Assert.True((await membership.GetAsync(TenantId.ToString(), userId))?.IsSuspended);

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("active", Assert.Single(result.Value.Items).Status);
    }

    [Theory]
    [InlineData(null, "pending")]
    [InlineData("pending", "pending")]
    [InlineData("delivered", "delivered")]
    [InlineData("failed", "failed")]
    public async Task ShouldReadDeliveryStatusFromTheDeliveryProcessGivenInvitation(string? stored, string expected)
    {
        // Arrange
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole, Now.AddDays(7), InvitedBy, null);
        if (stored is not null)
            await _deliveries.CreateIfAbsentAsync(TenantInvitationDeliveryRecord.Start(TenantId, entry.InvitationId)
                with
            { Status = stored });
        var handler = Handler(new FakeInvitationDirectory(entry), new FakeMembershipDirectory(), new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            new ListTenantInvitations(TenantId), Actor()), CancellationToken.None);

        // Assert
        var view = Assert.Single(result.Value.Items);
        Assert.Equal(expected, view.DeliveryStatus);
        Assert.Equal(entry.InvitationId, view.InvitationId);
    }

    [Fact]
    public async Task ShouldReadGuestProgramsFromTheVerifiedManifestGivenGuestInvitation()
    {
        // Arrange
        var programIds = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "guest@example.com", "guest", false,
            null, Now.AddDays(7), InvitedBy, null, await ScopeAsync(programIds),
            DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30));
        var handler = Handler(new FakeInvitationDirectory(entry), new FakeMembershipDirectory(), new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            new ListTenantInvitations(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.Equal(programIds, Assert.Single(result.Value.Items).GuestProgramIds);
    }

    [Fact]
    public async Task ShouldReturnEmptyGivenInvitationNotProjected()
    {
        // Arrange
        var handler = Handler(new FakeInvitationDirectory(null),
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: "invitee@example.com");

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
    }

    [Fact]
    public async Task ShouldExposeCurrentProjectionGivenReissueStillPending()
    {
        // Arrange
        var prior = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole,
            Now.AddDays(1), InvitedBy, null);
        var directory = new FakeInvitationDirectory(prior);
        var handler = Handler(directory,
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));
        var request = new ListTenantInvitations(TenantId, EmailAddress: prior.EmailAddress);

        // Act
        var beforeProjection = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);
        directory.Entry = prior with
        {
            BuiltInRole = BuiltInRbac.ComplianceManagementRole,
            ExpiresAt = Now.AddDays(7),
        };
        var afterProjection = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            request, Actor()), CancellationToken.None);

        // Assert
        var stale = Assert.Single(beforeProjection.Value.Items);
        var current = Assert.Single(afterProjection.Value.Items);
        Assert.Equal("pending", stale.Status);
        Assert.Equal("pending", current.Status);
        Assert.Equal(BuiltInRbac.ComplianceParticipationRole, stale.BuiltInRole);
        Assert.Equal(BuiltInRbac.ComplianceManagementRole, current.BuiltInRole);
    }

    [Fact]
    public async Task ShouldShowExpiredInvitationGivenClockPassesExpiration()
    {
        // Arrange
        var entry = new TenantInvitationDirectoryEntry(TenantId, Uuid.CreateVersion4(), "invitee@example.com",
            "client_personnel", false, BuiltInRbac.ComplianceParticipationRole,
            Now, InvitedBy, null);
        var handler = Handler(new FakeInvitationDirectory(entry),
            new FakeMembershipDirectory(), new FixedTimeProvider(Now));

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListTenantInvitations>(
            new ListTenantInvitations(TenantId), Actor()), CancellationToken.None);

        // Assert
        Assert.Equal("expired", Assert.Single(result.Value.Items).Status);
    }

    readonly IImmutableContentStore _content = new FitzKvImmutableContentStore(
        new PersistenceKvClient(new InMemoryKvClient()));

    readonly FitzKvTenantInvitationDeliveryStore _deliveries = new(
        new PersistenceKvClient(new InMemoryKvClient()));

    ListTenantInvitationsHandler Handler(ITenantInvitationDirectoryReader directory,
        ITenantMembershipDirectoryReader memberships, TimeProvider clock) =>
        new(directory, memberships, _deliveries, _content, clock);

    Task<ImmutableContentRef> ScopeAsync(params Uuid[] programIds) => _content.WriteJsonAsync(TenantId.ToString(),
        Uuid.CreateVersion4(), "TenantMemberInvited.GuestProgramIdsManifest.v1", (IReadOnlyList<Uuid>)programIds);

    static ClaimsPrincipal Actor() => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class FakeInvitationDirectory(TenantInvitationDirectoryEntry? entry)
        : ITenantInvitationDirectoryReader
    {
        public TenantInvitationDirectoryEntry? Entry { get; set; } = entry;

        public ValueTask<TenantInvitationDirectoryEntry?> GetAsync(Uuid tenantId,
            string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult(Entry is not null && Entry.TenantId == tenantId &&
                Entry.EmailAddress == emailAddress ? Entry : null);

        public ValueTask<Page<TenantInvitationDirectoryEntry>> ListAsync(Uuid tenantId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantInvitationDirectoryEntry>(
                Entry is not null && Entry.TenantId == tenantId ? [Entry] : [], null));
    }

    sealed class FakeMembershipDirectory : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(IsMember
            ? new TenantMembershipView(userId,
                Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture))
            {
                IsSuspended = IsSuspended,
            } : null);

        public bool IsMember { get; set; }
        public bool IsSuspended { get; set; }

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(IsMember && !IsSuspended);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

}
