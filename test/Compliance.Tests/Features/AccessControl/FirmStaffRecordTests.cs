using System.Security.Claims;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmStaffRecordTests
{
    static readonly Uuid Operator = Uuid.CreateVersion4();

    static ClaimsPrincipal Actor => ActorFor(Operator);

    [Fact]
    public async Task ShouldKeepOneStaffRecordGivenCanonicalUserRegistrationRaceWithCrashAfterClaim()
    {
        // Arrange
        var crash = new CrashOnceStore();
        await using var host = new Host(crash);
        var user = Uuid.CreateVersion4();
        var winner = Uuid.CreateVersion4();
        var loser = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(winner, user), Actor)).IsSuccess);
        Assert.True((await host.Bus.SendAsync(Register(loser, user), Actor)).IsSuccess);

        // Act
        crash.ArmAfterReplaceCount(1); // lease lands, the advance after the claim stage crashes
        var failure = await Record.ExceptionAsync(() => FirmStaffFacts.RunProcessAsync(host.Provider, winner));
        var claimedBeforeResume = (await host.HydrateAsync(new FirmStaffIdentityClaim(user))).ClaimedByStaffMemberId;
        var pendingBeforeResume = (await host.HydrateAsync(new FirmStaffRecord(winner))).IsPending;
        var rejected = await FirmStaffFacts.RunProcessAsync(host.Provider, loser);
        var resumed = await FirmStaffFacts.RunProcessAsync(host.Provider, winner);

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(winner, claimedBeforeResume);
        Assert.True(pendingBeforeResume);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(ProcessState.Completed, rejected.State);
        Assert.Contains("superseded", rejected.Note, StringComparison.Ordinal);
        var winnerRecord = await host.HydrateAsync(new FirmStaffRecord(winner));
        var loserRecord = await host.HydrateAsync(new FirmStaffRecord(loser));
        Assert.True(winnerRecord.IsRegistered);
        Assert.Equal(1UL, winnerRecord.Revision);
        Assert.True(loserRecord.IsRejected);
        Assert.False(loserRecord.IsRegistered);
        Assert.Single(await host.EventsAsync<FirmStaffIdentityClaimed>(new FirmStaffIdentityClaim(user).Stream));
        Assert.Empty(await host.EventsAsync<FirmStaffChangeRecorded>(loserRecord.Stream));
        Assert.Single(await host.EventsAsync<FirmStaffRegistrationRejected>(loserRecord.Stream));
        var views = host.Scope.ServiceProvider.GetRequiredService<FirmStaffViews>();
        Assert.Equal(winner, (await views.FindByUserAsync(user))!.StaffMemberId);
        Assert.Null(await views.GetAsync(loser));
    }

    [Fact]
    public async Task ShouldRegisterGivenMoreThanOneThousandStaffRecords()
    {
        // Arrange
        await using var host = new Host();
        for (var index = 0; index < 1000; index++)
            await FirmStaffFacts.RegisterAsync(host.Provider, Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var staff = Uuid.CreateVersion4();

        // Act
        var admitted = await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()));
        await FirmStaffFacts.ProjectDirectoryAsync(host.Provider);
        var directory = await host.Bus.SendAsync(new GetFirmStaffDirectory(), Actor);
        var assignable = await host.Scope.ServiceProvider.GetRequiredService<IFirmStaffDirectoryReader>()
            .ListAsync(true);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.True(directory.IsSuccess, directory.Error?.Message);
        Assert.Equal(1001, directory.Value!.Staff.Count);
        Assert.Equal(1001, assignable.Staff.Count);
        Assert.Contains(directory.Value.Staff, item => item.StaffMemberId == staff && item.Revision == 1);
        Assert.Equal(directory.Value.Staff.Select(item => item.StaffMemberId.ToString()).Order(StringComparer.Ordinal),
            directory.Value.Staff.Select(item => item.StaffMemberId.ToString()));
    }

    [Fact]
    public async Task ShouldReturnOriginalAcknowledgmentAndAppendNothingGivenExactRetryAfterLaterOwnerChange()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var request = Register(staff, Uuid.CreateVersion4());
        var first = await host.Bus.SendAsync(request, Actor);
        await FirmStaffFacts.RunProcessAsync(host.Provider, staff);
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic deactivation", 1), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retry = await host.Bus.SendAsync(request, Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(staff, retry.Value!.OperationId);
        Assert.Equal($"/api/v1/platform/operations/{staff}", retry.Value.StatusUrl);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var accepted = Assert.Single(await host.EventsAsync<FirmStaffRegistrationAccepted>(stream));
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, FirmStaffContent.Realm,
            accepted.FirstResponse!)).HttpStatus);
        Assert.Equal(Operator.ToString(), accepted.Operation.Actor.Id);
    }

    [Fact]
    public async Task ShouldConflictGivenSameStaffMemberWithChangedIntentOrOperator()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(staff, user), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var otherPractice = await host.Bus.SendAsync(Register(staff, user) with { Practice = "advisory" }, Actor);
        var otherUser = await host.Bus.SendAsync(Register(staff, Uuid.CreateVersion4()), Actor);
        var otherOperator = await host.Bus.SendAsync(Register(staff, user), ActorFor(Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, otherPractice.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherUser.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, otherOperator.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenMalformedRegistrationOrStaleRevision()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();

        // Act
        var empty = await host.Bus.SendAsync(Register(Uuid.Empty, user), Actor);
        var practice = await host.Bus.SendAsync(Register(staff, user) with { Practice = "audit" }, Actor);
        var revision = await host.Bus.SendAsync(Register(staff, user) with { ExpectedRevision = 1 }, Actor);
        await using var unknownHost = new Host(unknownUsers: true);
        var unknown = await unknownHost.Bus.SendAsync(Register(staff, user), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, empty.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, practice.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, revision.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Null((await host.HydrateAsync(new FirmStaffRecord(staff))).Accepted);
    }

    [Fact]
    public async Task ShouldReturnOriginalViewAndAppendNothingGivenExactStatusRetryAfterLaterChange()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor,
            Register(staff, Uuid.CreateVersion4()))).IsSuccess);
        var command = new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false, "Synthetic deactivation", 1);
        var first = await host.Bus.SendAsync(command, Actor);
        Assert.True((await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, true,
            "Synthetic reactivation", 2), Actor)).IsSuccess);
        var stream = new FirmStaffRecord(staff).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var retry = await host.Bus.SendAsync(command, Actor);
        var changedReason = await host.Bus.SendAsync(command with { Reason = "Another reason" }, Actor);
        var changedStatus = await host.Bus.SendAsync(command with { IsActive = true }, Actor);
        var stale = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), staff, false,
            "Synthetic stale", 1), Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.False(first.Value!.IsActive);
        Assert.Equal(2, first.Value.Revision);
        Assert.Equal(RequestErrorKind.Conflict, changedReason.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedStatus.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var latest = (await host.Scope.ServiceProvider.GetRequiredService<FirmStaffViews>().GetAsync(staff))!;
        Assert.True(latest.IsActive);
        Assert.Equal(3, latest.Revision);
        var firstResponse = (await host.EventsAsync<FirmStaffChangeRecorded>(stream))
            .Single(item => item.Operation.LogicalId == command.CommandId);
        Assert.Equal(200, (await FirstResponses.ReplayAsync(host.Content, FirmStaffContent.Realm,
            firstResponse.FirstResponse!)).HttpStatus);
    }

    [Fact]
    public async Task ShouldRefuseStatusChangeGivenPendingUnknownOrRejectedRegistration()
    {
        // Arrange
        await using var host = new Host();
        var pending = Uuid.CreateVersion4();
        Assert.True((await host.Bus.SendAsync(Register(pending, Uuid.CreateVersion4()), Actor)).IsSuccess);

        // Act
        var duringPending = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), pending, false,
            "Synthetic", 0), Actor);
        var unknown = await host.Bus.SendAsync(new SetFirmStaffStatus(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            false, "Synthetic", 0), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, duringPending.Error?.Kind);
        Assert.True(duringPending.Error?.IsTransient);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReplayReceiverAndRefuseNonSystemCallerGivenCompletedRegistration()
    {
        // Arrange
        await using var host = new Host();
        var staff = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        Assert.True((await FirmStaffFacts.RegisterThroughProcessAsync(host.Provider, Actor, Register(staff, user)))
            .IsSuccess);
        var catalog = host.Provider.GetRequiredService<ProcessCatalog>().Get(FirmStaffEffects.ProcessName);
        var record = (await host.Provider.GetRequiredService<IProcessRecordStore>().ReadAsync(
            new ProcessKey(FirmStaffEffects.ProcessName, FirmStaffContent.Realm, staff)))!;
        var claim = (await catalog.CreateReceiver(new ProcessStep(record, "claim", nameof(ClaimFirmStaffIdentity),
            user.ToString(), FirmStaffEffects.ReceiverCommandId(record.Source.EventId,
                nameof(ClaimFirmStaffIdentity), user.ToString(), record.Episode)), host.Provider, CancellationToken.None))!;
        var stream = new FirmStaffIdentityClaim(user).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var replayed = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim, RequestActor.System);
        var denied = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim, Actor);
        var changed = await host.Bus.SendAsync((ClaimFirmStaffIdentity)claim with { StaffMemberId = Uuid.CreateVersion4() },
            RequestActor.System);

        // Assert
        Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.False(changed.Error?.IsTransient);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRegisterOneProcessDefinitionGivenCompositionAndRebindReevaluationReactorToStaffRecords()
    {
        // Arrange
        await using var host = new Host();

        // Act
        var definition = host.Provider.GetRequiredService<ProcessCatalog>().Get(FirmStaffEffects.ProcessName);
        var reevaluation = Assert.Single(host.Provider.GetServices<WorkloadRegistration>(),
            item => item.Name == FirmStaffStatusReevaluationReactor.WorkloadName);
        var registration = Assert.Single(host.Provider.GetServices<WorkloadRegistration>(),
            item => item.Name == FirmStaffRegistrationReactor.WorkloadName);
        var pattern = EventStreamPattern.ForPattern("bdgrz", FirmStaffRecord.Area);

        // Assert
        Assert.True(definition.Ordered);
        Assert.Equal(["FirmStaffRegistrationAccepted"], definition.RootSourceEvents);
        Assert.Equal(["claim", "complete"], definition.Stages.Select(stage => stage.Stage));
        Assert.Equal([["ClaimFirmStaffIdentity"], ["CompleteFirmStaffRegistration"]],
            definition.Stages.Select(stage => stage.Receivers));
        Assert.Equal(["RejectFirmStaffRegistration"], definition.Abort);
        Assert.Equal(pattern, ((Reactor)host.Provider.CreateAsyncScope().ServiceProvider
            .GetRequiredService(reevaluation.ComponentType)).Pattern);
        Assert.Equal(pattern, ((Reactor)host.Provider.CreateAsyncScope().ServiceProvider
            .GetRequiredService(registration.ComponentType)).Pattern);
    }

    static RegisterFirmStaff Register(Uuid staffMemberId, Uuid userId) =>
        new(staffMemberId, userId, "attest", "Synthetic staff source", 0);

    static ClaimsPrincipal ActorFor(Uuid userId) => new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    /// <summary>Decorates the store so one ReplaceAsync, chosen by count, fails after the receiver already ran.</summary>
    sealed class CrashOnceStore
    {
        int _remaining = int.MaxValue;

        public void ArmAfterReplaceCount(int successfulReplacesBeforeCrash) => _remaining = successfulReplacesBeforeCrash;

        public IProcessRecordStore Wrap(IProcessRecordStore inner) => new Decorator(inner, this);

        sealed class Decorator(IProcessRecordStore inner, CrashOnceStore owner) : IProcessRecordStore
        {
            public Task<ProcessRecord?> ReadAsync(ProcessKey key, CancellationToken ct = default) =>
                inner.ReadAsync(key, ct);

            public Task<ProcessRecord> CreateIfAbsentAsync(ProcessRecord record, CancellationToken ct = default) =>
                inner.CreateIfAbsentAsync(record, ct);

            public Task<ProcessRecord> ReplaceAsync(ProcessRecord record, long expectedVersion,
                CancellationToken ct = default)
            {
                if (owner._remaining-- == 0)
                    throw new InvalidOperationException("Worker crashed before the CAS.");
                return inner.ReplaceAsync(record, expectedVersion, ct);
            }

            public Task<IReadOnlyList<ProcessKey>> ScanPendingAsync(string processKind, DateTimeOffset dueBefore,
                int limit, CancellationToken ct = default) => inner.ScanPendingAsync(processKind, dueBefore, limit, ct);
        }
    }

    sealed class Host : IAsyncDisposable
    {
        readonly AsyncServiceScope _scope;

        public Host(CrashOnceStore? crash = null, bool unknownUsers = false)
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPlatformUserDirectoryReader>(new Users(!unknownUsers));
            services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess());
            if (crash is not null)
                services.AddSingleton<IProcessRecordStore>(provider => crash.Wrap(
                    new FitzKvProcessRecordStore(provider.GetRequiredService<IKvClient>())));
            Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = Provider.CreateAsyncScope();
        }

        public ServiceProvider Provider { get; }

        public AsyncServiceScope Scope => _scope;

        public IRequestBus Bus => _scope.ServiceProvider.GetRequiredService<IRequestBus>();

        public Bdgrz.Compliance.Features.Content.IImmutableContentStore Content =>
            _scope.ServiceProvider.GetRequiredService<Bdgrz.Compliance.Features.Content.IImmutableContentStore>();

        public async Task<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate) where TAggregate : Aggregate =>
            await _scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(aggregate);

        public async Task<List<TEvent>> EventsAsync<TEvent>(EventStreamAddress stream) where TEvent : DomainEvent
        {
            var found = new List<TEvent>();
            try
            {
                await foreach (var record in _scope.ServiceProvider.GetRequiredService<IDomainEventReader>()
                                   .ReadAsync(stream, 0))
                    if (record.Event is TEvent typed)
                        found.Add(typed);
            }
            catch (InvalidOperationException)
            {
                // The in-memory store reports an unwritten stream this way.
            }

            return found;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    sealed class Users(bool known) : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(known);
    }

    sealed class OperatorAccess : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId != Uuid.Empty);
    }
}
