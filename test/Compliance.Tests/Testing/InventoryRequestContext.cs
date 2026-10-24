using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>A direct request context whose logical id is the request's own identity when it has one.</summary>
sealed class InventoryRequestContext<TRequest>(TRequest request, ClaimsPrincipal actor)
    : IRequestContext<TRequest> where TRequest : notnull
{
    readonly Uuid _requestId = request is IIdentifiedRequest identified ? identified.RequestId : Uuid.CreateVersion4();

    public TRequest Request => request;
    public ClaimsPrincipal Actor => actor;
    public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
    public Uuid RequestId => _requestId;
    public Uuid CorrelationId => _requestId;
    public Uuid? CausationId => null;
    public Uuid CauseId => _requestId;
    public DateTimeOffset StartedAt { get; } = ApplicationInventoryHost.Now;
    public RequestInvocation Invocation { get; } = new DirectInvocation();
}
