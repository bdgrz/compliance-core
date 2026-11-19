using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Services;

/// <summary>Runs a member's own lifecycle decision and freezes its original completed response.</summary>
static class MemberLifecycleOperations
{
    public static async ValueTask<Result<AdministratorTransitionResponse>> RunAsync<TRequest>(
        NativeOperationExecutor operations, IImmutableContentStore content, TimeProvider clock,
        IRequestContext<TRequest> context, Uuid commandId, Uuid tenantId, Uuid userId, Uuid episodeId,
        long expectedRevision, ActorReference actor, string commandKind, Func<Member, Result> prepare,
        Func<Member, OperationProvenance, ImmutableContentRef, ImmutableContentRef, Result> apply,
        CancellationToken ct) where TRequest : IRequest<AdministratorTransitionResponse>
    {
        var owner = new Member(tenantId, userId);
        var provenance = OwnerProvenance.For(owner, commandId, commandKind, actor, episodeId.ToString(),
            (ulong)expectedRevision, AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
        ImmutableContentRef? intent = null;
        var accepted = await operations.ExecuteAsync(new NativeOperation<Member>(AcceptedOperationBinding.Of(provenance),
            () => new Member(tenantId, userId, selectedCommandId: commandId), member => member.Acceptance,
            async (member, token) =>
            {
                var prepared = prepare(member);
                if (!prepared.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
                intent = await content.WriteJsonAsync(tenantId.ToString(), Uuid.CreateVersion4(),
                    "member-lifecycle-request.v1", context.Request, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(await TeamMemberResponses.FreezeCompletedAsync(content,
                    tenantId.ToString(), commandId, provenance.AcceptedAt, token).ConfigureAwait(false));
            }, (Member member, ImmutableContentRef frozen) => apply(member, provenance,
                intent ?? throw new InvalidOperationException("A prepared lifecycle operation must retain its authored intent."), frozen)),
            context, ct).ConfigureAwait(false);
        return await TeamMemberResponses.ReadAsync(content, tenantId.ToString(), accepted, ct).ConfigureAwait(false);
    }
}
