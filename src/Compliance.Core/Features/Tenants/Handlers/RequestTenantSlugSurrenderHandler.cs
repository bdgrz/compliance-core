using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Handlers;

/// <summary>
///     Starts the tenant's surrender of its confirmed slug.
/// </summary>
public sealed class RequestTenantSlugSurrenderHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<RequestTenantSlugSurrender>
{
    const string CommandKind = "RequestTenantSlugSurrender";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<RequestTenantSlugSurrender> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant is required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null, selected => new Tenant(request.TenantId, selected),
            tenant => tenant.Acceptance, tenant => tenant.PrepareRequestSlugSurrender(request.Slug),
            (Tenant tenant, OperationProvenance operation, ImmutableContentRef response) => tenant.RequestSlugSurrender(operation, response, request.Slug));
    }
}
