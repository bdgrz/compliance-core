using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Handlers;

public sealed class RegisterMemberHandler(NativeOperationExecutor operations, IAggregateReader reader,
    MemberAuthorityCleanup cleanup, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<RegisterMember>
{
    const string CommandKind = "RegisterMember";
    const string InitialEpisode = "initial";

    public async ValueTask<Result> HandleAsync(IRequestContext<RegisterMember> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.UserId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A command id, tenant and user are required."));
        var actor = ActorOf(context.Actor);
        if (actor is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The caller identity is not recognized."));

        var current = await reader.HydrateAsync(Hydrate(request), ct).ConfigureAwait(false);
        var provenance = Provenance(request, actor, clock.GetUtcNow());
        var binding = AcceptedOperationBinding.Of(provenance);
        if (current.Acceptance.Decide(binding) == NativeAcceptanceDecision.None &&
            current.IsDeprovisioned && !current.IsDeprovisionCleanupComplete)
        {
            var cleanupResult = await cleanup.CompleteAsync(context, current.TerminationProof ??
                throw new InvalidOperationException("Retirement requires its source proof."), ct)
                .ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
                return cleanupResult;
        }

        var accepted = await operations.ExecuteAsync(new NativeOperation<Member>(binding, () => Hydrate(request),
            member => member.Acceptance,
            (member, token) => PrepareAsync(member, request, token),
            (Member member, ImmutableContentRef frozen) => member.Register(provenance, frozen, request.Affiliation,
                request.MembershipExpiresOn)), context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result.Failure(accepted.Error!);
        return accepted.Value switch
        {
            AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed => Result.Success,
            _ => Result.Failure(AcceptedAppendResult.ConflictError)
        };
    }

    /// <summary>The provenance a registration carries; the binding of a retry is derived from it.</summary>
    internal static OperationProvenance Provenance(RegisterMember request, ActorReference actor,
        DateTimeOffset acceptedAt)
    {
        var owner = new Member(request.TenantId, request.UserId).Stream;
        return new OperationProvenance(request.CommandId, request.CommandId, null, CommandKind, actor, owner,
            InitialEpisode, null, AuthoredIntentDigest.Compute(request), acceptedAt, owner, owner);
    }

    async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(Member member, RegisterMember request,
        CancellationToken ct)
    {
        var prepared = member.PrepareRegister(request.Affiliation, request.MembershipExpiresOn);
        if (!prepared.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
        var result = await content.WriteJsonAsync(request.TenantId.ToString(), Uuid.CreateVersion4(),
            UnitResult.Schema, new UnitResult(), ct).ConfigureAwait(false);
        return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200,
            ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(result)));
    }

    static Member Hydrate(RegisterMember request) =>
        new(request.TenantId, request.UserId, Uuid.Empty, request.CommandId);

    internal static ActorReference? ActorOf(System.Security.Claims.ClaimsPrincipal actor)
    {
        if (RequestActor.IsSystem(actor))
            return ActorReference.ForSystemProcess("system", "system");
        return UserIdentityClaims.TryGetBdgrzSubject(actor, out var userId)
            ? new ActorReference("user", userId.ToString(), UserIdentityClaims.BdgrzDisplay(actor, userId))
            : null;
    }
}
