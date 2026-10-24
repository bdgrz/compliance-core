using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Answers the tenant with the slug claim the slug owner refused.
///     The answer is a native operation bound to its command id. An exact replay returns the original acceptance and an
///     answer for a tenant that already moved on is a terminal no-op, so neither can block the reaction checkpoint.
/// </summary>
public sealed class RejectTenantSlugHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<RejectTenantSlug>
{
    const string CommandKind = "RejectTenantSlug";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<RejectTenantSlug> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant is required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null, selected => new Tenant(request.TenantId, selected),
            tenant => tenant.Acceptance, tenant => tenant.PrepareRejectSlug(request.Slug),
            (Tenant tenant, OperationProvenance operation, ImmutableContentRef response) => tenant.RejectSlug(operation, response, request.Slug),
            Tenant.StaleSlugOutcome);
    }
}
