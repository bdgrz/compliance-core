using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.SourceFacts;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestAttemptBindingTests
{
    [Fact]
    public async Task ShouldRetainLaterDurableAttemptGivenDelayedTransportResultFromPriorUnknownAttempt()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var week = new DateOnly(2026, 10, 12);
        var at = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var clock = new MutableClock(at);
        var events = new InMemoryEventStore();
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(settings);
        services.AddPortia();
        services.AddVerifiedWorkDigestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        async Task MutateAsync(Func<WorkDigestDispatch, bool> mutation)
        {
            var result = await executor.ExecuteAsync(new WorkDigestDispatch(tenant, member), dispatch =>
            {
                Assert.True(mutation(dispatch));
                return AggregateOutcome.Commit(Result.Success);
            }, new Cntryl.Portia.RequestContext<Mutation>(new Mutation(), RequestActor.System));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }
        var delivery = new DelayedDelivery(async () =>
        {
            // The first transport is still unresolved while its lease expires and another authorized attempt starts.
            await MutateAsync(dispatch => dispatch.RecordInterruptedAttempt(week, at.AddMinutes(4)));
            var interrupted = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));
            var prior = interrupted.Read(week);
            await WorkDigestFacts.AuthorizeAsync(scope.ServiceProvider, new WorkDigestUnknownRetryAuthorized(tenant, member,
                week, prior.MessageId, prior.Attempts, Uuid.CreateVersion4(), "external-receipt",
                "Confirmed retry authorization", at.AddMinutes(5)), interrupted.Revision);
            await MutateAsync(dispatch => dispatch.StartAttempt(week, at.AddMinutes(6)));
            clock.Set(at.AddMinutes(7));
        });

        var processor = new WorkDigestDispatchProcessor(reader, executor, new ReadyContent(), delivery, settings, clock,
            new WorkDigestHistoryReader(reader, events, scope.ServiceProvider.GetRequiredService<ISourceFactResolver>()));

        // Act
        await processor.ProcessMemberAsync(tenant, user, tenantActive: true, memberActive: true, CancellationToken.None);
        var current = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));
        var status = current.Read(week);

        // Assert
        Assert.Equal(1, delivery.Calls);
        Assert.Equal(2, status.Attempts);
        Assert.Equal(WorkDigestDispatch.InFlight, status.Status);
        Assert.Equal(at.AddMinutes(6), status.LastAttemptAt);
        var outcomes = new List<WorkDigestDispatchOutcomeRecorded>();
        await foreach (var record in events.ReadAsync(current.Stream, 0, CancellationToken.None))
            if (record.Event is WorkDigestDispatchOutcomeRecorded outcome)
                outcomes.Add(outcome);
        Assert.DoesNotContain(outcomes, outcome => outcome.Status == WorkDigestDispatch.Sent);
        Assert.Equal("worker_interrupted", Assert.Single(outcomes).FailureCode);
    }

    [Fact]
    public async Task ShouldNeverSendGivenClaimCommittedButAppendResponseWasLost()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var week = new DateOnly(2026, 10, 12);
        var at = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var clock = new MutableClock(at);
        var events = new InMemoryEventStore();
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(settings);
        services.AddPortia();
        services.AddVerifiedWorkDigestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var scheduled = new WorkDigestDispatch(tenant, member);
        Assert.True(scheduled.Schedule(new WorkDigestScheduleWindow(week, "UTC", at), Uuid.CreateVersion4(),
            TimeSpan.FromDays(1), at));
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>().SaveAsync(scheduled,
            new RequestDispatchContext(RequestActor.System));
        var lostResponse = new CommitThenFailOnceExecutor(scope.ServiceProvider.GetRequiredService<IAggregateExecutor>());
        var delivery = new DelayedDelivery(() => Task.CompletedTask);

        var processor = new WorkDigestDispatchProcessor(reader, lostResponse, new ReadyContent(), delivery, settings, clock,
            new WorkDigestHistoryReader(reader, events, scope.ServiceProvider.GetRequiredService<ISourceFactResolver>()));

        // Act
        await processor.ProcessMemberAsync(tenant, user, true, true, CancellationToken.None);
        await processor.ProcessMemberAsync(tenant, user, true, true, CancellationToken.None);
        var claimed = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));
        clock.Set(at.AddMinutes(4));
        await processor.ProcessMemberAsync(tenant, user, true, true, CancellationToken.None);
        var recovered = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));

        // Assert
        Assert.Equal(0, delivery.Calls);
        Assert.Equal(WorkDigestDispatch.InFlight, claimed.Read(week).Status);
        Assert.Equal(WorkDigestDispatch.Unknown, recovered.Read(week).Status);
        Assert.Equal("worker_interrupted", recovered.Read(week).FailureCode);
        Assert.Equal(1, recovered.Read(week).Attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRecoverUnknownWithoutResendingGivenTransportExceptionOrRequestedCancellation(bool cancellation)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var user = Uuid.CreateVersion4();
        var member = RbacIds.Member(tenant, user);
        var week = new DateOnly(2026, 10, 12);
        var at = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        var clock = new MutableClock(at);
        var events = new InMemoryEventStore();
        var settings = new WorkDigestDeliverySettings("https://app.example",
            "/tenants/{tenant_id}/{tenant_slug}/programs/{program_id}/work/{work_item_id}",
            3, TimeSpan.FromMinutes(5), TimeSpan.FromDays(6), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(1));
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton(settings);
        services.AddPortia();
        services.AddVerifiedWorkDigestReader();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        using var canceled = new CancellationTokenSource();
        var delivery = new DelayedDelivery(() =>
        {
            if (!cancellation)
                return Task.FromException(new IOException("Ambiguous transport outcome"));
            canceled.Cancel();
            return Task.FromCanceled(canceled.Token);
        });

        var processor = new WorkDigestDispatchProcessor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), new ReadyContent(), delivery, settings, clock,
            new WorkDigestHistoryReader(reader, events, scope.ServiceProvider.GetRequiredService<ISourceFactResolver>()));

        // Act
        var failure = await Record.ExceptionAsync(async () => await processor.ProcessMemberAsync(tenant, user, true, true,
            canceled.Token));
        var first = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));
        clock.Set(at.AddMinutes(4));
        await processor.ProcessMemberAsync(tenant, user, true, true, CancellationToken.None);
        var recovered = await reader.HydrateAsync(new WorkDigestDispatch(tenant, member));

        // Assert
        if (cancellation)
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else
            Assert.Null(failure);
        Assert.Equal(cancellation ? WorkDigestDispatch.InFlight : WorkDigestDispatch.Unknown, first.Read(week).Status);
        Assert.Equal(WorkDigestDispatch.Unknown, recovered.Read(week).Status);
        Assert.Equal(1, recovered.Read(week).Attempts);
        Assert.Equal(1, delivery.Calls);
    }

    sealed record Mutation : IRequest;
    sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset now) => _now = now;
    }
    sealed class ReadyContent : IWorkDigestContentReader
    {
        public ValueTask<WorkDigestContentRead> ReadAsync(Uuid tenant, Uuid user, WorkDigestDispatchStatusView dispatch,
            CancellationToken ct) => ValueTask.FromResult(WorkDigestContentRead.Ready(new WorkDigestDeliveryMessage(
                dispatch.MessageId, "member@example.test", "Digest", "Frozen test content")));
    }
    sealed class DelayedDelivery(Func<Task> beforeResult) : IWorkDigestDelivery
    {
        public int Calls { get; private set; }
        public async ValueTask<WorkDigestTransportOutcome> SendAsync(WorkDigestDeliveryMessage message, CancellationToken ct)
        {
            Calls++;
            await beforeResult();
            return new WorkDigestTransportOutcome(WorkDigestTransportOutcomeKind.Accepted);
        }
    }
}
