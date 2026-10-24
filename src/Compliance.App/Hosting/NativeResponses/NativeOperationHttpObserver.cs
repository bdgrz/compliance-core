using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Captures only the matching authenticated top-level identified HTTP admission. It performs no reads.</summary>
public sealed class NativeOperationHttpObserver(IHttpContextAccessor accessor) : INativeOperationTransportObserver
{
    public void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted)
    {
        var http = accessor.HttpContext;
        if (!Matches(http, binding, context, out var user))
            return;
        NativeFirstResponseCapture? capture = accepted switch
        {
            AcceptedAppendResult.Committed value => new(binding, value.FirstFact, value.FirstResponse, value.Response),
            AcceptedAppendResult.Replayed value => new(binding, value.FirstFact, value.FirstResponse, value.Response),
            _ => null,
        };
        if (capture is null)
            return;
        var hasMemberTenant = Uuid.TryParse(binding.Realm, out var memberTenant) ||
            binding.FirstOwner.Realm == "bdgrz" && binding.FirstOwner.Area == "tenants" &&
            Uuid.TryParse(binding.FirstOwner.Resource, out memberTenant);
        var actorMatches = binding.OriginalActor.Kind switch
        {
            "member" => hasMemberTenant && binding.OriginalActor.Id == RbacIds.Member(memberTenant, user).ToString(),
            "user" or "platform_operator" or "firm_staff" => binding.OriginalActor.Id == user.ToString(),
            _ => false,
        };
        var realmMatches = !http.Request.RouteValues.TryGetValue("tenant_id", out var tenantValue) ||
            Uuid.TryParse(tenantValue?.ToString(), out var routeTenant) &&
            (Uuid.TryParse(binding.Realm, out var realmTenant) && routeTenant == realmTenant ||
             binding.FirstOwner.Realm == "bdgrz" && binding.FirstOwner.Area == "tenants" &&
             binding.FirstOwner.Resource == routeTenant.ToString());
        if (!actorMatches || !realmMatches || capture.Fact.Stream != binding.FirstOwner || binding.FirstOwner.Realm != binding.Realm)
            capture = capture with { Ambiguous = true };
        var previous = http.Features.Get<NativeFirstResponseCapture>();
        if (previous is not null && previous != capture)
            capture = previous with { Ambiguous = true };
        http.Features.Set(capture);
    }
    public RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
        ImmutableContentIntegrityException exception) => Matches(accessor.HttpContext, binding, context, out _)
        ? new RequestError(RequestErrorKind.Conflict, "The original native response cannot be verified.", exception is ImmutableContentUnavailableException)
        : null;

    static bool Matches([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] HttpContext? http,
        AcceptedOperationBinding binding, IExecutionContext context, out Uuid user)
    {
        user = Uuid.Empty;
        if (http is null || context is not IRequestContext<object> request ||
            request.Request is not IIdentifiedRequest identified || request.Invocation is not HttpInvocation invocation ||
            http.GetEndpoint()?.Metadata.GetMetadata<NativeFirstResponseEndpoint>() is not { } endpoint ||
            endpoint.RequestType != request.Request.GetType() || request.Request.GetType().Name != binding.CommandKind ||
            identified.RequestId != binding.LogicalId || request.RequestId != binding.LogicalId || binding.LogicalId == Uuid.Empty ||
            !StringComparer.Ordinal.Equals(invocation.Method, http.Request.Method) ||
            !StringComparer.Ordinal.Equals(invocation.Path, http.Request.Path.Value) ||
            !StringComparer.Ordinal.Equals(invocation.RequestIdentifier, http.TraceIdentifier) ||
            RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out user) ||
            !UserIdentityClaims.TryGetBdgrzSubject(http.User, out var ingressUser) || user != ingressUser)
            return false;
        return true;
    }
}
