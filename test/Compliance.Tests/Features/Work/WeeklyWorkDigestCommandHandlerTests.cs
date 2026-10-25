using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WeeklyWorkDigestCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly WeekStarting = new(2026, 10, 12);

    [Fact]
    public async Task ShouldScheduleBoundedPageGivenActiveOptedInMembersWithVerifiedEmail()
    {
        // Arrange
        await using var fixture = new Fixture();
        var subscribedUser = Uuid.CreateVersion4();
        var optedOutUser = Uuid.CreateVersion4();
        await fixture.SeedRecipientAsync(subscribedUser, verifiedEmail: true);
        await fixture.SeedRecipientAsync(optedOutUser, verifiedEmail: true, optedOut: true);
        var memberships = new MembershipDirectory(fixture.TenantId,
        [
            new TenantMembershipView(subscribedUser, fixture.TenantId),
            new TenantMembershipView(optedOutUser, fixture.TenantId),
        ], nextCursor: "page-2");
        var handler = fixture.ScheduleHandler(memberships);

        // Act
        var result = await handler.HandleAsync(Fixture.ScheduleContext(new ScheduleWeeklyWorkDigest(
            fixture.TenantId, WeekStarting, "schedule-2026-10-12", Limit: 2)), CancellationToken.None);
        var digestId = WeeklyWorkDigest.IdFor(fixture.TenantId,
            RbacIds.Member(fixture.TenantId, subscribedUser), WeekStarting);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);
        var retry = await handler.HandleAsync(Fixture.ScheduleContext(new ScheduleWeeklyWorkDigest(
            fixture.TenantId, WeekStarting, "schedule-2026-10-12", Limit: 2)), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new WorkDigestSchedulePageView(2, 1, 1, "page-2"), result.Value);
        Assert.Equal("scheduled", digest.View.Status);
        Assert.Equal("scheduled", (await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId)).View.Status);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(0, retry.Value.DigestsScheduled);
    }

    [Fact]
    public async Task ShouldRejectUntrustedSchedulerAndInvalidWeekGivenScheduleCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var handler = fixture.ScheduleHandler(new MembershipDirectory(fixture.TenantId, []));
        var request = new ScheduleWeeklyWorkDigest(fixture.TenantId, WeekStarting,
            "schedule-2026-10-12");

        // Act
        var wrongWorker = await handler.HandleAsync(Fixture.ScheduleContext(request,
            "scheduler:Other"), CancellationToken.None);
        var wrongWeek = await handler.HandleAsync(Fixture.ScheduleContext(request with
        {
            WeekStarting = WeekStarting.AddDays(-7),
        }), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, wrongWorker.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, wrongWeek.Error?.Kind);
    }

    [Fact]
    public async Task ShouldBindDeliveryAndOutcomeToExactEventsGivenScheduledDigest()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        var digestId = await fixture.ScheduleOneAsync(userId);
        var scheduleEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digestId);
        var deliveryHandler = fixture.DeliveryHandler();
        var deliveryKey = WeeklyWorkDigest.DeliveryKeyFor(digestId, 1);
        var deliveryCommand = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 1, 1, deliveryKey);

        // Act
        var requested = await deliveryHandler.HandleAsync(Fixture.DeliveryContext(deliveryCommand,
            scheduleEventId), CancellationToken.None);
        var duplicate = await deliveryHandler.HandleAsync(Fixture.DeliveryContext(deliveryCommand,
            scheduleEventId), CancellationToken.None);
        var requestEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digestId);
        var recordHandler = fixture.RecordHandler();
        var recordCommand = new RecordWorkDigestDelivery(fixture.TenantId, digestId,
            requestEventId, 2, 1, deliveryKey,
            "delivered", fixture.Clock.GetUtcNow().AddMinutes(1), null);
        fixture.Clock.Set(recordCommand.AttemptedAt.AddMinutes(1));
        var wrongCause = await recordHandler.HandleAsync(Fixture.RecordContext(recordCommand,
            Uuid.CreateVersion4()), CancellationToken.None);
        var recorded = await recordHandler.HandleAsync(Fixture.RecordContext(recordCommand,
            requestEventId), CancellationToken.None);
        var retry = await recordHandler.HandleAsync(Fixture.RecordContext(recordCommand,
            requestEventId), CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);

        // Assert
        Assert.True(requested.IsSuccess, requested.Error?.Message);
        Assert.True(duplicate.IsSuccess, duplicate.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, wrongCause.Error?.Kind);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal("delivered", digest.View.Status);
        var attempt = Assert.Single(digest.View.DeliveryAttempts);
        Assert.Equal("delivered", attempt.Status);
        Assert.Equal(deliveryKey, attempt.DeliveryKey);
        Assert.Equal("system_process", attempt.RequestedBy.Kind);
        Assert.Equal(WeeklyWorkDigest.DeliveryProcessId, attempt.RecordedBy?.Id);
    }

    [Fact]
    public async Task ShouldAcceptCallbackReplayGivenTheNextDeliveryAttemptIsPending()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        var digestId = await fixture.ScheduleOneAsync(userId);
        var scheduleEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digestId);
        var firstKey = WeeklyWorkDigest.DeliveryKeyFor(digestId, 1);
        var deliveryHandler = fixture.DeliveryHandler();
        var firstDelivery = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 1, 1, firstKey);
        Assert.True((await deliveryHandler.HandleAsync(Fixture.DeliveryContext(firstDelivery,
            scheduleEventId), CancellationToken.None)).IsSuccess);
        var firstRequestEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestDeliveryRequested>(digestId);
        var recordHandler = fixture.RecordHandler();
        var failedCallback = new RecordWorkDigestDelivery(fixture.TenantId, digestId,
            firstRequestEventId, 2, 1, firstKey, "failed", fixture.Clock.GetUtcNow().AddMinutes(1),
            "provider_unavailable");
        fixture.Clock.Set(failedCallback.AttemptedAt.AddMinutes(1));
        Assert.True((await recordHandler.HandleAsync(Fixture.RecordContext(failedCallback,
            firstRequestEventId), CancellationToken.None)).IsSuccess);

        var secondDelivery = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 3, 2, WeeklyWorkDigest.DeliveryKeyFor(digestId, 2));
        Assert.True((await deliveryHandler.HandleAsync(Fixture.DeliveryContext(secondDelivery,
            scheduleEventId), CancellationToken.None)).IsSuccess);

        // Act
        var replay = await recordHandler.HandleAsync(Fixture.RecordContext(failedCallback,
            firstRequestEventId), CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(4, digest.View.Revision);
        Assert.Collection(digest.View.DeliveryAttempts,
            first => Assert.Equal("failed", first.Status),
            second => Assert.Equal("requested", second.Status));
    }

    [Fact]
    public async Task ShouldSuppressScheduledDigestGivenCurrentOptOut()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        var digestId = await fixture.ScheduleOneAsync(userId);
        var scheduleEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digestId);
        var preference = new WorkDigestPreference(fixture.TenantId, memberId);
        fixture.Clock.Set(fixture.Clock.GetUtcNow().AddMinutes(1));
        preference.Set(false, ActorReference.ForMember(memberId, "Member"), fixture.Clock.GetUtcNow());
        await fixture.Writer.SaveAsync(preference, new RequestDispatchContext(RequestActor.System));
        var command = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digestId, 1));

        // Act
        var result = await fixture.DeliveryHandler().HandleAsync(
            Fixture.DeliveryContext(command, scheduleEventId), CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("suppressed", digest.View.Status);
        Assert.Equal("email_digest_opted_out", digest.View.SuppressionReason);
        Assert.Empty(digest.View.DeliveryAttempts);
    }

    [Fact]
    public async Task ShouldSuppressGuestDigestGivenGuestMembership()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        var digestId = await fixture.SeedScheduledDigestAsync(userId, "guest");
        var scheduleEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digestId);
        var command = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digestId, 1));

        // Act
        var result = await fixture.DeliveryHandler().HandleAsync(
            Fixture.DeliveryContext(command, scheduleEventId), CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("suppressed", digest.View.Status);
        Assert.Equal("guest_member", digest.View.SuppressionReason);
        Assert.Empty(digest.View.DeliveryAttempts);
    }

    [Fact]
    public async Task ShouldSkipSchedulingGivenGuestMembership()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        await fixture.SeedRecipientAsync(userId, verifiedEmail: true, affiliation: "guest");
        var handler = fixture.ScheduleHandler(new MembershipDirectory(fixture.TenantId,
            [new TenantMembershipView(userId, fixture.TenantId, "guest")]));

        // Act
        var result = await handler.HandleAsync(Fixture.ScheduleContext(new ScheduleWeeklyWorkDigest(
            fixture.TenantId, WeekStarting, "schedule-2026-10-12")), CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, WeeklyWorkDigest.IdFor(fixture.TenantId, memberId, WeekStarting));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new WorkDigestSchedulePageView(1, 0, 1, null), result.Value);
        Assert.Equal("missing", digest.View.Status);
    }

    [Fact]
    public async Task ShouldKeepDeliveryRequestIdempotentGivenOptOutAfterPendingAttempt()
    {
        // Arrange
        await using var fixture = new Fixture();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(fixture.TenantId, userId);
        var digestId = await fixture.ScheduleOneAsync(userId);
        var scheduleEventId = await fixture.FindEventIdAsync<WeeklyWorkDigestScheduled>(digestId);
        var command = new DeliverWeeklyWorkDigest(fixture.TenantId, memberId, digestId,
            scheduleEventId, 1, 1, WeeklyWorkDigest.DeliveryKeyFor(digestId, 1));
        var handler = fixture.DeliveryHandler();
        var requested = await handler.HandleAsync(Fixture.DeliveryContext(command, scheduleEventId),
            CancellationToken.None);
        fixture.Clock.Set(fixture.Clock.GetUtcNow().AddMinutes(1));
        var preference = new WorkDigestPreference(fixture.TenantId, memberId);
        preference.Set(false, ActorReference.ForMember(memberId, "Member"), fixture.Clock.GetUtcNow());
        await fixture.Writer.SaveAsync(preference, new RequestDispatchContext(RequestActor.System));

        // Act
        var duplicate = await handler.HandleAsync(Fixture.DeliveryContext(command, scheduleEventId),
            CancellationToken.None);
        var digest = await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId);

        // Assert
        Assert.True(requested.IsSuccess, requested.Error?.Message);
        Assert.True(duplicate.IsSuccess, duplicate.Error?.Message);
        Assert.Equal("delivery_requested", digest.View.Status);
        Assert.Single(digest.View.DeliveryAttempts);
    }

    [Fact]
    public async Task ShouldRejectForgedDeliveryCallbackGivenNoExactRequestEvent()
    {
        // Arrange
        await using var fixture = new Fixture();
        var digestId = await fixture.ScheduleOneAsync(Uuid.CreateVersion4());
        var command = new RecordWorkDigestDelivery(fixture.TenantId, digestId,
            Uuid.CreateVersion4(), 2, 1, WeeklyWorkDigest.DeliveryKeyFor(digestId, 1),
            "delivered", Now.AddMinutes(2), null);

        // Act
        var result = await fixture.RecordHandler().HandleAsync(
            Fixture.RecordContext(command, command.DeliveryRequestEventId), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal("scheduled", (await new WorkDigestHistoryReader(fixture.Reader, fixture.Events).ReadWeeklyAsync(fixture.TenantId, digestId)).View.Status);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        readonly InMemoryEventStore _store;
        readonly IAggregateExecutor _executor;

        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public InMemoryEventStore Events => _store;
        public MutableTimeProvider Clock { get; } = new(Now.AddMinutes(30));
        readonly EmailDirectory _emails = new();

        public Fixture()
        {
            _store = new InMemoryEventStore();
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(_store);
            services.AddSingleton<IDomainEventReader>(_store);
            services.AddPortia();
            services.AddVerifiedWorkDigestReader();
            _provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
            });
            _scope = _provider.CreateAsyncScope();
            var scoped = _scope.ServiceProvider;
            Reader = scoped.GetRequiredService<IAggregateReader>();
            Writer = scoped.GetRequiredService<IAggregateWriter>();
            _executor = scoped.GetRequiredService<IAggregateExecutor>();
        }

        public ScheduleWeeklyWorkDigestHandler ScheduleHandler(ITenantMembershipDirectoryReader memberships) =>
            new(_executor, memberships, RecipientResolver(), Clock);

        public DeliverWeeklyWorkDigestHandler DeliveryHandler() =>
            new(Reader, _executor, Events, RecipientResolver(), Clock);

        public RecordWorkDigestDeliveryHandler RecordHandler() => new(_executor, Events, Clock);

        WeeklyWorkDigestRecipientResolver RecipientResolver() => new(Reader, _emails);

        public async Task SeedRecipientAsync(Uuid userId, bool verifiedEmail, bool optedOut = false,
            string affiliation = "client_personnel")
        {
            var memberId = RbacIds.Member(TenantId, userId);
            var member = new Member(TenantId, userId);
            DateOnly? membershipExpiresOn = affiliation == "guest"
                ? DateOnly.FromDateTime(Now.UtcDateTime).AddDays(30)
                : null;
            Assert.True(member.Register(affiliation, membershipExpiresOn).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));

            if (optedOut)
            {
                var preference = new WorkDigestPreference(TenantId, memberId);
                preference.Set(false, ActorReference.ForMember(memberId, "Member"), Now.AddMinutes(-2));
                await Writer.SaveAsync(preference, new RequestDispatchContext(RequestActor.System));
            }

            if (!verifiedEmail)
                return;
            var emailAddress = $"{userId}@example.test";
            var email = new EmailAddress(emailAddress);
            Assert.True(email.Reserve(userId).IsSuccess);
            var challengeId = Uuid.CreateVersion4();
            var token = "verified-token";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
            Assert.True(email.IssueChallenge(userId, challengeId, hash, Now.AddHours(1), Now)
                .IsSuccess);
            Assert.True(email.CompleteChallenge(userId, token, Now.AddMinutes(1)).IsSuccess);
            await Writer.SaveAsync(email, new RequestDispatchContext(RequestActor.System));
            _emails.Add(new EmailAddressView(userId, emailAddress, true));
        }

        public async Task<Uuid> ScheduleOneAsync(Uuid userId,
            string affiliation = "client_personnel")
        {
            await SeedRecipientAsync(userId, verifiedEmail: true, affiliation: affiliation);
            var handler = ScheduleHandler(new MembershipDirectory(TenantId,
                [new TenantMembershipView(userId, TenantId, affiliation)]));
            var result = await handler.HandleAsync(ScheduleContext(new ScheduleWeeklyWorkDigest(
                TenantId, WeekStarting, "schedule-2026-10-12")), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            return WeeklyWorkDigest.IdFor(TenantId, RbacIds.Member(TenantId, userId), WeekStarting);
        }

        public async Task<Uuid> SeedScheduledDigestAsync(Uuid userId, string affiliation)
        {
            await SeedRecipientAsync(userId, verifiedEmail: true, affiliation: affiliation);
            var memberId = RbacIds.Member(TenantId, userId);
            var digestId = WeeklyWorkDigest.IdFor(TenantId, memberId, WeekStarting);
            var digest = new WeeklyWorkDigest(TenantId, digestId);
            Assert.True(digest.Schedule(memberId, WeekStarting, "schedule-2026-10-12",
                ActorReference.ForSystemProcess(WeeklyWorkDigest.SchedulerProcessId,
                    "Weekly work digest scheduler"), Now).IsSuccess);
            await Writer.SaveAsync(digest, new RequestDispatchContext(RequestActor.System));
            return digestId;
        }

        public async Task<Uuid> FindEventIdAsync<TEvent>(Uuid digestId) where TEvent : DomainEvent
        {
            var stream = new WeeklyWorkDigest(TenantId, digestId).Stream;
            await foreach (var record in Events.ReadAsync(stream, 0, CancellationToken.None))
                if (record.Event is TEvent)
                    return record.Event.Metadata.EventId;
            throw new InvalidOperationException($"The {typeof(TEvent).Name} event was not persisted.");
        }

        public static Cntryl.Portia.RequestContext<ScheduleWeeklyWorkDigest> ScheduleContext(
            ScheduleWeeklyWorkDigest request, string processId = WeeklyWorkDigest.SchedulerProcessId) =>
            new(request, Dispatch(RequestActor.CreateSystem(processId, "bdgrz.system")));

        public static Cntryl.Portia.RequestContext<DeliverWeeklyWorkDigest> DeliveryContext(
            DeliverWeeklyWorkDigest request, Uuid causationId,
            string processId = WeeklyWorkDigest.DeliveryProcessId) =>
            new(request, Dispatch(RequestActor.CreateSystem(processId, "bdgrz.system"), causationId));

        public static Cntryl.Portia.RequestContext<RecordWorkDigestDelivery> RecordContext(
            RecordWorkDigestDelivery request, Uuid causationId,
            string processId = WeeklyWorkDigest.DeliveryProcessId) =>
            new(request, Dispatch(RequestActor.CreateSystem(processId, "bdgrz.system"), causationId));

        static RequestDispatchContext Dispatch(System.Security.Claims.ClaimsPrincipal actor,
            Uuid? causationId = null) => new(actor, new DirectInvocation(),
            new RequestMetadata(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                causationId ?? Uuid.CreateVersion4()));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class MembershipDirectory : ITenantMembershipDirectoryReader
    {
        readonly Uuid _tenantId;
        readonly IReadOnlyList<TenantMembershipView> _members;
        readonly string? _nextCursor;

        public MembershipDirectory(Uuid tenantId, IReadOnlyList<TenantMembershipView> members,
            string? nextCursor = null)
        {
            _tenantId = tenantId;
            _members = members;
            _nextCursor = nextCursor;
        }

        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(_members.FirstOrDefault(member =>
            member.TenantId == _tenantId && member.UserId == userId));

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(_members.Any(member =>
            member.TenantId == _tenantId && member.UserId == userId && !member.IsSuspended &&
            !member.IsDeprovisioned));

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>(_members.Take(limit).ToArray(), _nextCursor));
    }

    sealed class EmailDirectory : IEmailAddressDirectoryReader
    {
        readonly List<EmailAddressView> _addresses = [];

        public void Add(EmailAddressView address) => _addresses.Add(address);

        public ValueTask<EmailAddressView?> GetAsync(string emailAddress,
            CancellationToken ct = default) => ValueTask.FromResult(_addresses.FirstOrDefault(address =>
            address.EmailAddress == emailAddress));

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<EmailAddressView>(
            _addresses.Where(address => address.UserId == userId).Take(limit ?? 50).ToArray(), null));
    }

    sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Set(DateTimeOffset value) => _utcNow = value;
    }
}
