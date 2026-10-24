using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Native roster admission; the request pipeline authorizes current authority before any replay.</summary>
public sealed class PlatformOperatorRosterOperations(NativeOperationExecutor operations, IImmutableContentStore content,
    IPlatformUserDirectoryReader users, TimeProvider clock)
{
    public ValueTask<Result> SeedAsync(IRequestContext<SeedPlatformOperatorRoster> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Only the trusted bootstrap producer may seed the roster.")));
        var mutation = new PlatformOperatorMutationContent(nameof(SeedPlatformOperatorRoster), request.RosterId, null,
            Uuid.Empty, Uuid.Empty, Uuid.Empty, PlatformOperatorRosterRules.CanonicalUsers(request.UserIds), string.Empty,
            RbacOperationActor.From(context.Actor), ActorReference.ForSystemProcess("platform-operator-bootstrap", "Platform operator bootstrap"),
            clock.GetUtcNow());
        return ExecuteAsync(context, mutation, ct);
    }

    public ValueTask<Result> GrantAsync(IRequestContext<GrantPlatformOperator> context, CancellationToken ct) =>
        Personal(context, (actor, attribution) => ExecuteAsync(context, new(nameof(GrantPlatformOperator),
            context.Request.CommandId, context.Request.ExpectedRevision, actor, context.Request.UserId, Uuid.Empty, [],
            context.Request.Reason?.Trim() ?? string.Empty, attribution,
            ActorReference.ForPlatformOperator(actor, UserIdentityClaims.BdgrzDisplay(context.Actor, actor)), clock.GetUtcNow()), ct));

    public ValueTask<Result> RevokeAsync(IRequestContext<RevokePlatformOperator> context, CancellationToken ct) =>
        Personal(context, (actor, attribution) => ExecuteAsync(context, new(nameof(RevokePlatformOperator),
            context.Request.CommandId, context.Request.ExpectedRevision, actor, context.Request.UserId,
            context.Request.AssignmentId, [], context.Request.Reason?.Trim() ?? string.Empty, attribution,
            ActorReference.ForPlatformOperator(actor, UserIdentityClaims.BdgrzDisplay(context.Actor, actor)), clock.GetUtcNow()), ct));

    static ValueTask<Result> Personal<T>(IRequestContext<T> context,
        Func<Uuid, ActorAttribution, ValueTask<Result>> next) where T : IRequest
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var actor))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "An authenticated platform user is required.")));
        return next(actor, RbacOperationActor.From(context.Actor));
    }

    async ValueTask<Result> ExecuteAsync(IExecutionContext context, PlatformOperatorMutationContent mutation, CancellationToken ct)
    {
        if (mutation.CommandId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A selected command identity is required."));
        var candidate = new PlatformOperatorRoster(mutation.CommandId);
        var operation = OwnerProvenance.For(candidate, mutation.CommandId, mutation.CommandKind, mutation.Actor,
            candidate.Id.ToString(), mutation.ExpectedRevision, PlatformOperatorRosterContent.IntentDigest(mutation), mutation.AcceptedAt);
        PlatformOperatorAuthorityContent? before = null;
        PlatformOperatorAuthorityContent? after = null;
        ImmutableContentRef? change = null;
        ImmutableContentRef? authority = null;
        ImmutableContentRef? result = null;
        FirstResponseDescriptor? descriptor = null;
        ulong revision = 0;
        ulong position = 0;
        var accepted = await operations.ExecuteAsync(new NativeOperation<PlatformOperatorRoster>(AcceptedOperationBinding.Of(operation),
            () => new(mutation.CommandId), static roster => roster.Acceptance, async (roster, token) =>
            {
                before = await PlatformOperatorRosterContent.ReadAsync(content, roster, token).ConfigureAwait(false);
                var evaluated = PlatformOperatorRosterRules.Apply(before, mutation);
                if (!evaluated.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(evaluated.Error!);
                if (mutation.CommandKind == nameof(GrantPlatformOperator) &&
                    !await users.ExistsAsync(mutation.SubjectUserId, token).ConfigureAwait(false))
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The platform user is not available yet.", isTransient: true));
                after = evaluated.Value!;
                revision = roster.Revision;
                position = roster.CommittedStreamPosition;
                change = await content.WriteJsonAsync(PlatformOperatorRosterContent.Realm, Uuid.CreateVersion4(),
                    PlatformOperatorRosterContent.ChangeSchema, mutation, token).ConfigureAwait(false);
                authority = await content.WriteJsonAsync(PlatformOperatorRosterContent.Realm, Uuid.CreateVersion4(),
                    PlatformOperatorRosterContent.AuthoritySchema, after, token).ConfigureAwait(false);
                descriptor = await UnitResponses.FreezeAsync(content, PlatformOperatorRosterContent.Realm, token).ConfigureAwait(false);
                result = ((StandardResponseBody)descriptor.Body).Content;
                return Result<FirstResponseDescriptor>.Success(descriptor);
            }, (roster, first) => roster.Append(PlatformOperatorRosterContent.Fact(mutation, operation, change!, authority!,
                first, result!, after!), revision, position, mutation, before!, after!, descriptor!)), context, ct).ConfigureAwait(false);
        if (!accepted.IsSuccess)
            return Result.Failure(accepted.Error!);
        var response = accepted.Value switch
        {
            AcceptedAppendResult.Committed committed => committed.Response,
            AcceptedAppendResult.Replayed replayed => replayed.Response,
            _ => null
        };
        if (response is null)
            return Result.Failure(AcceptedAppendResult.ConflictError);
        if (response.HttpStatus != 200 || response.ResultSchema != UnitResult.Schema || response.ContentType != "application/json" ||
            response.Body is not StandardResponseBody body ||
            !PlatformOperatorRosterContent.Matches(body.Content, new UnitResult(), UnitResult.Schema))
            throw new ImmutableContentIntegrityException("The roster operation has no exact original Unit response.");
        await content.ReadJsonAsync<UnitResult>(PlatformOperatorRosterContent.Realm, body.Content, ct).ConfigureAwait(false);
        return Result.Success;
    }
}
