using Bdgrz.Compliance.Features.Tenants.Events;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantDirectoryNotifierCompositionTests
{
    [Fact]
    public async Task ShouldWakeTenantDiscoveryOnCommittedEventGivenNotifierBackedEventStore()
    {
        // Arrange
        var events = new TenantLifecycleReader();
        await using var provider = Compose(events, registerEventStore: true);
        var directory = provider.GetRequiredService<ITenantDirectory>();
        using var cancellation = new CancellationTokenSource();
        await using var changes = directory.WatchAsync(cancellation.Token).GetAsyncEnumerator();
        var nextChange = changes.MoveNextAsync().AsTask();

        try
        {
            await events.InitialEmptyRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var subscribedPattern = await events.SubscriptionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(EventStreamPattern.ForPattern("bdgrz", "tenants"), subscribedPattern);

            // Act
            var tenantId = Uuid.CreateVersion4();
            await events.AppendAsync(new EventStreamAddress("bdgrz", "tenants", "tenant"), 0,
                [new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme")]);
            var moved = await nextChange.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            Assert.True(moved);
            Assert.Equal(TenantLifecycleChangeKind.Added, changes.Current.Kind);
            Assert.Equal(new TenantId(tenantId.ToString()), changes.Current.TenantId);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await nextChange;
            }
            catch (OperationCanceledException)
            {
                // Expected when a missing notifier leaves the watcher on its polling backstop.
            }
        }
    }

    [Fact]
    public async Task ShouldDiscoverTenantWithinPollingBackstopGivenNoNotifierRegistered()
    {
        // Arrange
        var events = new TenantLifecycleReader();
        using var clock = new ManualTimeProvider();
        await using var provider = Compose(events, registerEventStore: false, timeProvider: clock);
        var directory = provider.GetRequiredService<ITenantDirectory>();
        using var cancellation = new CancellationTokenSource();
        await using var changes = directory.WatchAsync(cancellation.Token).GetAsyncEnumerator();
        var nextChange = changes.MoveNextAsync().AsTask();

        try
        {
            await events.InitialEmptyRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await clock.TimerScheduled.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Act
            var tenantId = Uuid.CreateVersion4();
            events.Register(tenantId);
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(1, clock.TimerFires);
            var moved = await nextChange.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            Assert.True(moved);
            Assert.Equal(TenantLifecycleChangeKind.Added, changes.Current.Kind);
            Assert.Equal(new TenantId(tenantId.ToString()), changes.Current.TenantId);
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await nextChange;
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing the watcher after a successful change.
            }
        }
    }

    static ServiceProvider Compose(TenantLifecycleReader events, bool registerEventStore,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            })
            .Build();
        _ = services.AddCompliance(configuration);
        services.RemoveAll<IDomainEventReader>();
        services.RemoveAll<IDomainEventNotifier>();
        services.RemoveAll<IEventStore>();
        services.AddSingleton<IDomainEventReader>(events);
        if (registerEventStore)
            services.AddSingleton<IEventStore>(events);
        if (timeProvider is not null)
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);
        }
        return services.BuildServiceProvider();
    }

    sealed class TenantLifecycleReader : IEventStore, IDomainEventNotifier
    {
        readonly object _gate = new();
        readonly Channel<bool> _commits = Channel.CreateUnbounded<bool>();
        DomainEventRecord? _record;

        public TaskCompletionSource<EventStreamPattern> SubscriptionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> InitialEmptyRead { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Register(Uuid tenantId)
        {
            var registered = new TenantRegistered(tenantId, Uuid.CreateVersion4(), "Acme", "acme");
            lock (_gate)
            {
                _record = new DomainEventRecord(new EventStreamAddress("bdgrz", "tenants", "tenant"),
                    registered, 0, new EventCursor("registered"));
            }
        }

        public ValueTask AppendAsync(EventStreamAddress stream, ulong expectedStreamPosition,
            IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (events is not [TenantRegistered registered])
                throw new ArgumentException("The lifecycle test store accepts one registration event.", nameof(events));
            lock (_gate)
                _record = new DomainEventRecord(stream, registered, expectedStreamPosition,
                    new EventCursor("registered"));
            _ = _commits.Writer.TryWrite(true);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream,
            ulong fromOffset, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern,
            EventCursor after, [EnumeratorCancellation] CancellationToken ct)
        {
            DomainEventRecord? record;
            lock (_gate)
                record = after == EventCursor.Start ? _record : null;

            if (record is null)
            {
                InitialEmptyRead.TrySetResult(true);
                yield break;
            }

            await Task.CompletedTask;
            yield return record;
        }

        public ValueTask<IDomainEventSubscription> SubscribeAsync(EventStreamPattern pattern,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            SubscriptionStarted.TrySetResult(pattern);
            return ValueTask.FromResult<IDomainEventSubscription>(new TestSubscription(_commits));
        }

        sealed class TestSubscription(Channel<bool> commits) : IDomainEventSubscription
        {
            public async ValueTask WaitAsync(CancellationToken ct = default) =>
                _ = await commits.Reader.ReadAsync(ct);

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    sealed class ManualTimeProvider : TimeProvider, IDisposable
    {
        readonly object _gate = new();
        readonly List<ManualTimer> _timers = [];
        long _timestampTicks;
        int _timerFires;

        public TaskCompletionSource<bool> TimerScheduled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public int TimerFires => Volatile.Read(ref _timerFires);

        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

        public override long GetTimestamp() => Interlocked.Read(ref _timestampTicks);

        public override ITimer CreateTimer(TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (_gate)
                _timers.Add(timer);
            _ = timer.Change(dueTime, period);
            TimerScheduled.TrySetResult(true);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            _ = Interlocked.Add(ref _timestampTicks, duration.Ticks);
            ManualTimer[] timers;
            lock (_gate)
                timers = [.. _timers];
            var now = GetTimestamp();
            foreach (var timer in timers)
                timer.FireIfDue(now);
        }

        public void Dispose()
        {
            ManualTimer[] timers;
            lock (_gate)
                timers = [.. _timers];
            foreach (var timer in timers)
                timer.Dispose();
        }

        void Remove(ManualTimer timer)
        {
            lock (_gate)
                _ = _timers.Remove(timer);
        }

        void RecordTimerFire() => _ = Interlocked.Increment(ref _timerFires);

        sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            readonly object _gate = new();
            long _dueTimestamp = long.MaxValue;
            long _periodTicks;
            bool _disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_gate)
                {
                    if (_disposed)
                        return false;
                    _dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                        ? long.MaxValue
                        : checked(owner.GetTimestamp() + dueTime.Ticks);
                    _periodTicks = period == Timeout.InfiniteTimeSpan || period <= TimeSpan.Zero
                        ? 0
                        : period.Ticks;
                    return true;
                }
            }

            public void FireIfDue(long now)
            {
                lock (_gate)
                {
                    if (_disposed || now < _dueTimestamp)
                        return;
                    _dueTimestamp = _periodTicks == 0
                        ? long.MaxValue
                        : checked(now + _periodTicks);
                }
                owner.RecordTimerFire();
                callback(state);
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                        return;
                    _disposed = true;
                    _dueTimestamp = long.MaxValue;
                }
                owner.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
