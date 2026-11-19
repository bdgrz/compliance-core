using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.UserIdentities.Services;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Admits one normalized global name and freezes the original platform authoring response through existing native execution.</summary>
public sealed class CreateMethodologyTemplateV3Handler(NativeOperationExecutor operations, IImmutableContentStore content,
    TimeProvider clock) : IRequestHandler<CreateMethodologyTemplateV3>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplateV3> context, CancellationToken ct)
    {
        var request = context.Request;
        if (RequestActor.IsSystem(context.Actor) || !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Template authoring requires a personal platform operator."));
        var intent = new MethodologyTemplateCreationIntent(request.CommandId, request.TemplateId,
            request.Name?.Trim() ?? string.Empty, request.Purpose?.Trim() ?? string.Empty, user);
        if (!MethodologyTemplateNameClaim.ValidIntent(intent))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A template requires exact identities, original name and purpose."));
        var owner = new MethodologyTemplateNameClaim(intent.Name, request.CommandId);
        var target = new MethodologyTemplate(request.TemplateId).Stream;
        var provenance = new OperationProvenance(request.CommandId, request.CommandId, null,
            nameof(CreateMethodologyTemplateV3), ActorReference.ForPlatformOperator(user, user.ToString()), owner.Stream,
            request.TemplateId.ToString(), null, ContentDigest.OfJson(intent), clock.GetUtcNow(), target, target);
        try
        {
            ImmutableContentRef? snapshot = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<MethodologyTemplateNameClaim>(
                AcceptedOperationBinding.Of(provenance), () => new MethodologyTemplateNameClaim(intent.Name, request.CommandId),
                current => current.Acceptance,
                async (current, token) =>
                {
                    snapshot = await content.WriteJsonAsync(MethodologyTemplate.Realm,
                        Uuid.CreateVersion5(request.CommandId, MethodologyTemplateNameClaim.CreationSchema),
                        MethodologyTemplateNameClaim.CreationSchema, intent, token).ConfigureAwait(false);
                    var valid = current.PrepareCreation(provenance, intent, snapshot);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var response = await UnitResponses.FreezeAsync(content, MethodologyTemplate.Realm, token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(response with { HttpStatus = 202 });
                },
                (current, first) => current.ClaimCreation(provenance, intent, snapshot!, first)), context, ct).ConfigureAwait(false);
            return UnitResponses.ToResult(accepted);
        }
        catch (ImmutableContentConflictException)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The creation command identity is already bound to another original intent."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The original template creation content cannot be verified.", isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
