using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Hosting.Persistence;

/// <summary>Returns expected pre-append size rejection through the shared transport-independent result.</summary>
sealed class PersistenceValidationRequestBus(IRequestBus inner) : IRequestBus
{
    public RequestDispatchContext CreateContext(ClaimsPrincipal actor, RequestMetadata? metadata = null) =>
        inner.CreateContext(actor, metadata);

    public ValueTask<Result> AuthorizeAsync(IRequestBase request, RequestDispatchContext context,
        CancellationToken ct = default) => inner.AuthorizeAsync(request, context, ct);

    public async ValueTask<Result> DispatchAsync(IRequest request, RequestDispatchContext context,
        CancellationToken ct = default)
    {
        try
        {
            return await inner.DispatchAsync(request, context, ct).ConfigureAwait(false);
        }
        catch (EventFrameSizeException ex)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Validation, ex.Message));
        }
    }

    public async ValueTask<Result<TOut>> DispatchAsync<TOut>(IRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default)
    {
        try
        {
            return await inner.DispatchAsync(request, context, ct).ConfigureAwait(false);
        }
        catch (EventFrameSizeException ex)
        {
            return Result<TOut>.Failure(new RequestError(RequestErrorKind.Validation, ex.Message));
        }
    }

    public IAsyncEnumerable<TOut> DispatchStreamAsync<TOut>(IStreamRequest<TOut> request,
        RequestDispatchContext context, CancellationToken ct = default) =>
        inner.DispatchStreamAsync(request, context, ct);
}
