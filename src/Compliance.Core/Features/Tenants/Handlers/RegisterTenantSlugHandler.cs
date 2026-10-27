using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Claims the slug for the tenant, or answers with its refusal. The operation is bound to its command id: an exact replay returns the
///     original acceptance, and a second request by the tenant that already settled the slug appends nothing.
/// </summary>
public sealed class RegisterTenantSlugHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<RegisterTenantSlug>
{
    const string CommandKind = "RegisterTenantSlug";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<RegisterTenantSlug> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || !TenantSlugs.TryNormalizeForLookup(request.Slug, out var slug))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant and a valid slug are required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null,
            selected => new TenantSlug(slug, allowReserved: true, selected), owner => owner.Acceptance,
            owner => owner.PrepareRegister(request.TenantId),
            (TenantSlug owner, OperationProvenance operation, ImmutableContentRef response) => owner.Register(operation, response, request.TenantId),
            TenantSlug.AlreadyClaimed);
    }
}
