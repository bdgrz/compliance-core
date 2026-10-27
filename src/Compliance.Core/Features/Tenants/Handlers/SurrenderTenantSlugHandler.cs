using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>
///     Gives the slug up for good, or answers with its refusal. The operation is bound to its command id: an exact replay returns the
///     original acceptance, and a second request by the tenant that already settled the slug appends nothing.
/// </summary>
public sealed class SurrenderTenantSlugHandler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<SurrenderTenantSlug>
{
    const string CommandKind = "SurrenderTenantSlug";
    const string CurrentEpisode = "current";

    public ValueTask<Result> HandleAsync(IRequestContext<SurrenderTenantSlug> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || !TenantSlugs.TryNormalizeForLookup(request.Slug, out var slug))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant and a valid slug are required.")));
        return UnitOperations.RunAsync(operations, content, clock, context, RegisterMemberHandler.ActorOf(context.Actor),
            request.CommandId, CommandKind, CurrentEpisode, null,
            selected => new TenantSlug(slug, allowReserved: true, selected), owner => owner.Acceptance,
            owner => owner.PrepareSurrender(request.TenantId),
            (TenantSlug owner, OperationProvenance operation, ImmutableContentRef response) => owner.Surrender(operation, response, request.TenantId),
            TenantSlug.AlreadySurrendered);
    }
}
