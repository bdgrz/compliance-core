using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Records the commands an adapter forwards without executing them.</summary>
sealed class CapturingBus : IRequestBus
{
    public List<IRequestBase> Forwarded { get; } = [];

    public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
        new(actor);

    public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
        CancellationToken ct = default) => ValueTask.FromResult(Result.Success);

    public ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
        CancellationToken ct = default)
    {
        Forwarded.Add(request);
        return ValueTask.FromResult(Result.Success);
    }

    public ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request, RequestDispatchContext context,
        CancellationToken ct = default)
    {
        Forwarded.Add(request);
        return ValueTask.FromResult(Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, "Captured.")));
    }

    public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default) => throw new NotSupportedException();
}
