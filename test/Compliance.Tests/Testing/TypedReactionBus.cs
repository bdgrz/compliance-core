using Bdgrz.Compliance.Features.NativeOperations;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Lets a reactor under <see cref="ReactorScenario" /> send commands that answer with a typed response. The
///     scenario's own bus records only requests without a result, so each typed command is recorded as a
///     <see cref="TypedRequest" /> in the scenario's ordered list and answered here; <see cref="Sent" /> reads the list
///     back with the original commands in place.
/// </summary>
sealed class TypedReactionBus(IRequestBus inner) : IRequestBus
{
#pragma warning disable IDE0028 // ConditionalWeakTable cannot take a collection expression.
    static readonly ConditionalWeakTable<ReactorScenario, TypedReactionBus> Buses = new();
#pragma warning restore IDE0028
    readonly Dictionary<Type, RequestError> _failures = [];

    /// <summary>The recording wrapper a typed command takes in the scenario's list of sent requests.</summary>
    public sealed record TypedRequest([property: JsonIgnore] IRequestBase Request) : IRequest;

    /// <summary>The bus a reactor under test is constructed with.</summary>
    public static IRequestBus For(ReactorScenario scenario) => Buses.GetValue(scenario, s => new TypedReactionBus(s.Requests));

    /// <summary>Scripts a failure for every later <typeparamref name="TRequest" />.</summary>
    public static void FailWith<TRequest>(ReactorScenario scenario, RequestError error) =>
        ((TypedReactionBus)For(scenario))._failures[typeof(TRequest)] = error;

    public static void Succeed<TRequest>(ReactorScenario scenario) =>
        ((TypedReactionBus)For(scenario))._failures.Remove(typeof(TRequest));

    /// <summary>Every command the reactor sent, in order, with typed commands unwrapped.</summary>
    public static IReadOnlyList<IRequestBase> Sent(ReactorScenario scenario) => scenario.SentRequests
        .Select(request => request is TypedRequest typed ? typed.Request : request).ToList();

    public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
        inner.CreateContext(actor, metadata);

    public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
        CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

    public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
        CancellationToken ct = default) => inner.DispatchAsync(request, context, ct);

    public async ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default)
    {
        // The recording wrapper never leaves the in-memory scenario bus, so it needs no serializer root.
        await inner.DispatchAsync(new TypedRequest(request), context, ct);
        if (_failures.TryGetValue(request.GetType(), out var error))
            return Result<TOut>.Failure(error);
        if (typeof(TOut) == typeof(UnitResult))
            return Result<TOut>.Success((TOut)(object)new UnitResult());
        if (typeof(TOut) != typeof(AdministratorTransitionResponse))
            throw new NotSupportedException($"No scripted response for {typeof(TOut).Name}.");
        return Result<TOut>.Success((TOut)(object)new AdministratorTransitionResponse(Uuid.CreateVersion4(),
            AdministratorTransitionResponseState.Completed, null, DateTimeOffset.UnixEpoch));
    }

    public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default) =>
        inner.DispatchStreamAsync(request, context, ct);
}
