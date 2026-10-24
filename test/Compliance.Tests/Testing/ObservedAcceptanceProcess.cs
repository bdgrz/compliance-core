using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Drives the acceptance process with a driver of its own whose bus records every receiver command it sends and can
///     stop the worker, so the exact order of acquisition and the replay of a crash mid-acquire are observable.
///     <paramref name="stopBeforeReceiver" /> stops the worker before it sends the named receiver;
///     <paramref name="crashAfterSends" /> stops it after that many receivers were sent and applied but before the
///     process record advanced, as a worker that crashed before its compare-and-swap would.
/// </summary>
sealed class ObservedAcceptanceProcess(string? stopBeforeReceiver = null, int crashAfterSends = int.MaxValue)
{
    readonly List<SentCommand> _sent = [];

    /// <summary>Every receiver command sent, in order, with the target stream it named.</summary>
    public IReadOnlyList<SentCommand> Sent => _sent;

    /// <summary>Runs the process of an admitted operation; the crash is returned when the worker stopped.</summary>
    public async Task<(ProcessRecord? Record, Exception? Crash)> RunAsync(IndependenceHost host, Uuid engagement,
        Uuid operation)
    {
        var started = await AcceptanceFacts.StartAsync(host, engagement, operation);
        await using var scope = host.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var driver = new ProcessDriver(services.GetRequiredService<IProcessRecordStore>(),
            new SpyBus(services.GetRequiredService<IRequestBus>(), _sent, stopBeforeReceiver, crashAfterSends),
            services.GetRequiredService<ProcessCatalog>(), services.GetRequiredService<TimeProvider>(), services);
        try
        {
            return (await driver.RunAsync(started.Key, "test-worker", new RequestDispatchContext(RequestActor.System),
                CancellationToken.None), null);
        }
        catch (InvalidOperationException crash)
        {
            return (null, crash);
        }
    }

    /// <summary>One receiver command a driver sent.</summary>
    public sealed record SentCommand(string Receiver, Uuid CommandId, EventStreamAddress? Target);

    sealed class SpyBus(IRequestBus inner, List<SentCommand> sent, string? stopBefore, int crashAfter) : IRequestBus
    {
        public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
            inner.CreateContext(actor, metadata);

        public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

        public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
            CancellationToken ct = default) => inner.DispatchAsync(request, context, ct);

        public async ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
            CancellationToken ct = default)
        {
            var receiver = request is IEngagementAcceptanceReceiver or IEngagementPersonalDecisionReceiver;
            if (receiver && request.GetType().Name == stopBefore)
                throw new InvalidOperationException("Worker stopped before the receiver.");
            var result = await inner.DispatchAsync(request, context, ct).ConfigureAwait(false);
            if (!receiver)
                return result;
            sent.Add(new SentCommand(request.GetType().Name,
                (Uuid)request.GetType().GetProperty("CommandId")!.GetValue(request)!,
                request.GetType().GetProperty("Target")?.GetValue(request) as EventStreamAddress));
            if (sent.Count >= crashAfter)
                throw new InvalidOperationException("Worker crashed before the CAS.");
            return result;
        }

        public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
            RequestDispatchContext context, CancellationToken ct = default) =>
            inner.DispatchStreamAsync(request, context, ct);
    }
}
