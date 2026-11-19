using System.Globalization;
using System.Security.Cryptography;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Fitz;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Tenants.Services;

/// <summary>Resumably initializes independent operator owners from trusted genesis configuration.</summary>
public sealed class PlatformOperatorBootstrap(IServiceScopeFactory scopes, PlatformOperatorAuthority configuration,
    ILogger<PlatformOperatorBootstrap> logger) : BackgroundService
{
    static readonly ActorAttribution SystemActor = new("portia:system", "Portia");
    static readonly Action<ILogger, Exception?> WaitingForSource = LoggerMessage.Define(LogLevel.Warning,
        new EventId(1, nameof(WaitingForSource)), "Platform operator bootstrap is waiting for its source or participant.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await TickAsync(scope.ServiceProvider, stoppingToken).ConfigureAwait(false))
                    return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is FitzException or IOException or TimeoutException or EventStreamConcurrencyException)
            {
                WaitingForSource(logger, ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }

    internal async ValueTask<bool> TickAsync(IServiceProvider services, CancellationToken ct)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var seats = await reader.HydrateAsync(new PrimaryPlatformOperators(), ct).ConfigureAwait(false);
        if (seats.IsBootstrapComplete)
            return true;
        if (configuration.BootstrapUserIds.Count == 0)
            return configuration.DeveloperAuthentication;
        var plan = Plan(configuration.BootstrapUserIds);
        if (seats.BootstrapPlanId != Uuid.Empty && seats.BootstrapPlanId != plan)
            throw new InvalidOperationException("Platform operator genesis configuration changed before bootstrap completed.");
        var context = new RequestDispatchContext(RequestActor.System);
        if (!seats.HasConfirmedInitialPrimary)
        {
            var generation = seats.OccupiedSeats == 0 ? checked(seats.LastGeneration + 1) : seats.LastGeneration;
            var firstUser = configuration.BootstrapUserIds[0];
            var operation = AggregateIdentity.Create("PlatformInitialPrimary", plan, firstUser,
                generation.ToString(CultureInfo.InvariantCulture));
            var grant = AggregateIdentity.Create("PlatformInitialGrant", plan, firstUser);
            if (seats.OccupiedSeats == 0)
            {
                var expected = seats.Version;
                var reserved = await executor.ExecuteAsync(new PrimaryPlatformOperators(operation), owner =>
                    AggregateOutcome.CommitOnSuccess(owner.ReserveInitial(operation, expected, firstUser, grant,
                        plan, plan, SystemActor)), context, ct).ConfigureAwait(false);
                if (!reserved.IsSuccess)
                    return false;
            }
            var selected = await reader.HydrateAsync(new PrimaryPlatformOperators(operation), ct).ConfigureAwait(false);
            if (selected.SelectedCommand is not PrimaryPlatformOperatorSeatReserved reservation || reservation.UserId != firstUser ||
                reservation.GrantEpisodeId != grant || reservation.BootstrapPlanId != plan)
                return false;
            if (selected.SelectedOutcome is not null)
                return false;
            var intake = services.GetRequiredService<PlatformPrimarySignalIntake>();
            var sourcePosition = await FindReservationPositionAsync(services.GetRequiredService<IDomainEventReader>(),
                reservation.Metadata.EventId, ct).ConfigureAwait(false);
            if (sourcePosition is null)
                return false;
            if (!(await intake.AcceptAsync(PrimaryPlatformOperators.Address, sourcePosition.Value, reservation, ct).ConfigureAwait(false)).IsSuccess)
                return false;
            var source = NativeFactReference.Of(PrimaryPlatformOperators.Address, reservation);
            var key = ProcessKey.ForRoot(PlatformPrimarySignalIntake.ProcessName, reservation.Metadata.EventId, source);
            var record = await services.GetRequiredService<IProcessRecordStore>().ReadAsync(key, ct).ConfigureAwait(false);
            if (record is null || record.Targets.Count != 1 ||
                !PlatformPrimarySignalIntake.TrySlot(record.Targets[0], out var sourceAt, out var targetAt, out var targetRevision) ||
                sourceAt != sourcePosition.Value)
                return false;
            var participant = services.GetRequiredService<PlatformPrimaryParticipant>();
            if (!(await participant.ApplyReservationAsync(reservation.Metadata.EventId, sourcePosition.Value, targetAt,
                    targetRevision, context, ct).ConfigureAwait(false)).IsSuccess)
                return false;
            var targetFact = await participant.FindTargetOutcomeAsync(firstUser, operation, targetAt, ct).ConfigureAwait(false);
            if (targetFact is null || !(await participant.RecordOutcomeAsync(firstUser, targetFact.Value.Position,
                    targetFact.Value.Fact.Metadata.EventId, sourcePosition.Value, reservation.Metadata.EventId, context, ct).ConfigureAwait(false)).IsSuccess)
                return false;
            return false;
        }

        for (var index = 1; index < configuration.BootstrapUserIds.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var user = configuration.BootstrapUserIds[index];
            var operation = AggregateIdentity.Create("PlatformOrdinaryBootstrapGrant", plan, user);
            var result = await executor.ExecuteAsync(new PlatformOperator(user, operation), owner =>
                AggregateOutcome.CommitOnSuccess(owner.GrantBootstrap(operation, 0, plan, SystemActor)), context, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return false;
        }
        var completion = await executor.ExecuteAsync(new PrimaryPlatformOperators(), owner =>
            AggregateOutcome.CommitOnSuccess(owner.CompleteBootstrap(plan)), context, ct).ConfigureAwait(false);
        return completion.IsSuccess;
    }

    static Uuid Plan(IReadOnlyList<Uuid> users)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var user in users)
            hash.AppendData(user.ToGuid().ToByteArray(bigEndian: true));
        return AggregateIdentity.Create("PlatformOperatorGenesisConfiguration", Convert.ToHexString(hash.GetHashAndReset()));
    }

    static async ValueTask<ulong?> FindReservationPositionAsync(IDomainEventReader events, Uuid eventId, CancellationToken ct)
    {
        ulong next = 0;
        await foreach (var record in events.ReadAsync(PrimaryPlatformOperators.Address, 0, ct).ConfigureAwait(false))
        {
            if (record.Stream != PrimaryPlatformOperators.Address || record.ResourceOffset != next++ ||
                record.Event.Metadata.AggregateId != PrimaryPlatformOperators.OwnerId)
                return null;
            if (record.Event.Metadata.EventId == eventId)
                return !record.Event.Metadata.IsAudit && record.Event is PrimaryPlatformOperatorSeatReserved ? record.ResourceOffset : null;
        }
        return null;
    }
}
