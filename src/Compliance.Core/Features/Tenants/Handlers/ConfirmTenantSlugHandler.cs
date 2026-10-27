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
///     Answers the tenant with the slug claim the slug owner registered.
///     The answer is a native operation bound to its command id. An exact replay returns the original acceptance and an
///     answer for a tenant that already moved on is a terminal no-op, so neither can block the reaction checkpoint.
/// </summary>
public sealed class ConfirmTenantSlugHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<ConfirmTenantSlug>
{
    const string CommandKind = "ConfirmTenantSlug";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<ConfirmTenantSlug> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant is required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null, selected => new Tenant(request.TenantId, selected),
            tenant => tenant.Acceptance, tenant => tenant.PrepareConfirmSlug(request.Slug),
            (Tenant tenant, OperationProvenance operation, ImmutableContentRef response) => tenant.ConfirmSlug(operation, response, request.Slug),
            Tenant.StaleSlugOutcome);
    }
}
