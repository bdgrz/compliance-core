using System.Collections.Concurrent;
using System.Diagnostics;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
namespace Bdgrz.Compliance.Features.Tenants.Rules;

/// <summary>
///     The failure policy of the per-tenant activation reactors (#430). A transient failure, such
///     as a permission projection that has not caught up, is retried locally for
///     <see cref="LocalRetryWindow" /> and then returned to Portia's durable replay with the
///     checkpoint unchanged. Those workloads register <see cref="WorkloadFailureAttemptLimit" />,
///     so a prolonged outage for one tenant never exhausts a failed-pass limit and never stops the
///     worker host or other tenants' workloads. A permanent (non-transient) failure is replayed
///     up to <see cref="PermanentFailureLimit" /> consecutive times in this process and then
///     abandoned: the reactor logs <c>LogActivationAbandoned</c> at error level and advances its
///     checkpoint, leaving the tenant inactive (fail closed) until an operator issues a new
///     administrator invitation.
/// </summary>
public sealed partial class TenantActivationPolicy
{
    /// <summary>Activation workloads never fault the host for consecutive failed passes.</summary>
    public const int WorkloadFailureAttemptLimit = int.MaxValue;

    readonly ConcurrentDictionary<(string Reactor, Uuid EventId), int> _permanentFailures = new();

    public TenantActivationPolicy(TimeSpan? localRetryWindow = null, int permanentFailureLimit = 20)
    {
        LocalRetryWindow = localRetryWindow ?? TimeSpan.FromSeconds(8);
        ArgumentOutOfRangeException.ThrowIfLessThan(LocalRetryWindow, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(permanentFailureLimit, 1);
        PermanentFailureLimit = permanentFailureLimit;
    }

    internal static TenantActivationPolicy Default { get; } = new();

    public TimeSpan LocalRetryWindow { get; }
    public int PermanentFailureLimit { get; }

    /// <summary>
    ///     Runs one activation reaction under the policy. A transient failure propagates for
    ///     durable replay; a permanent failure propagates until its budget is spent.
    /// </summary>
    public async ValueTask<Result> ReactAsync(string reactor, Uuid tenantId, DomainEvent trigger,
        ILogger logger, Func<CancellationToken, ValueTask<Result>> reaction, CancellationToken ct)
    {
        var key = (reactor, trigger.Metadata.EventId);
        Result result;
        try
        {
            result = await reaction(ct).ConfigureAwait(false);
        }
        catch (ReactionCommandFailedException failure)
        {
            result = Result.Failure(failure.Error);
        }

        if (result.IsSuccess)
        {
            _ = _permanentFailures.TryRemove(key, out _);
            return result;
        }
        if (result.Error.IsTransient)
            return result;

        var attempts = _permanentFailures.AddOrUpdate(key, 1, static (_, count) => count + 1);
        if (attempts < PermanentFailureLimit)
            return result;
        _ = _permanentFailures.TryRemove(key, out _);
        LogActivationAbandoned(logger, tenantId, reactor, attempts, result.Error.Kind,
            result.Error.Message);
        return Result.Success;
    }

    /// <summary>Sends the activation, retrying a transient failure within the local window.</summary>
    public async ValueTask<Result> SendActivationAsync(IRequestBus bus, ActivateTenant activation,
        IReactorContext context, ILogger logger, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var result = await bus.SendReactionAsync(activation, context, ct).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                if (attempts > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    LogActivationRecovered(logger, activation.TenantId, attempts + 1,
                        elapsedMilliseconds);
                }
                return result;
            }
            if (!result.Error.IsTransient)
                return result;

            attempts++;
            if (attempts == 1)
                LogActivationWaiting(logger, activation.TenantId, result.Error.Kind);
            if (Stopwatch.GetElapsedTime(started) >= LocalRetryWindow)
            {
                LogActivationDeferred(logger, activation.TenantId, attempts,
                    result.Error.Kind);
                return result;
            }

            // Projection materialization can lag the source reaction. An extended lag
            // returns to Portia's durable pass retry instead of holding this pass forever.
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 41501, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation is waiting for provisioning after {ErrorKind}.")]
    static partial void LogActivationWaiting(ILogger logger, Uuid tenantId, RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41502, Level = LogLevel.Warning,
        Message = "Tenant {TenantId} activation remains pending after {Attempts} attempts and {ErrorKind}; the reaction will replay.")]
    static partial void LogActivationDeferred(ILogger logger, Uuid tenantId, int attempts,
        RequestErrorKind errorKind);

    [LoggerMessage(EventId = 41503, Level = LogLevel.Information,
        Message = "Tenant {TenantId} activation recovered on attempt {Attempt} after {ElapsedMilliseconds} ms.")]
    static partial void LogActivationRecovered(ILogger logger, Uuid tenantId, int attempt,
        double elapsedMilliseconds);

    [LoggerMessage(EventId = 41504, Level = LogLevel.Error,
        Message = "Tenant {TenantId} activation by {Reactor} was abandoned after {Attempts} permanent failures ({ErrorKind}: {Reason}); the tenant stays inactive until an operator issues a new administrator invitation.")]
    static partial void LogActivationAbandoned(ILogger logger, Uuid tenantId, string reactor,
        int attempts, RequestErrorKind errorKind, string reason);
}
