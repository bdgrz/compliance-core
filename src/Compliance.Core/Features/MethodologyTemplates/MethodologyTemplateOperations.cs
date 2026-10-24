using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Admits authored template operations, writing one immutable version delta and original result per native fact.</summary>
public sealed class MethodologyTemplateOperations(NativeOperationExecutor operations, MethodologyTemplateReader views,
    IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result> ExecuteAsync<T>(IRequestContext<T> context, Uuid templateId, Uuid commandId,
        string kind, long? expectedRevision, Func<MethodologyTemplate, Uuid, Result> guard,
        Func<MethodologyTemplateRules, Uuid, string, DateTimeOffset, CommandFailure?> prepare, CancellationToken ct)
        where T : IRequestBase
    {
        if (templateId == Uuid.Empty || commandId == Uuid.Empty ||
            !UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A template needs its stable identity and personal actor."));
        try
        {
            var actor = MethodologyTemplateCommandActor.For(context, Uuid.Empty);
            var provenance = OwnerProvenance.For(new MethodologyTemplate(templateId), commandId, kind,
                ActorReference.ForPlatformOperator(userId, userId.ToString()), templateId.ToString(),
                expectedRevision is { } expected ? unchecked((ulong)expected) : null,
                AuthoredIntentDigest.Compute(context.Request), clock.GetUtcNow());
            long preparedRevision = 0;
            MethodologyTemplateVersionView? version = null;
            ImmutableContentRef? change = null;
            ImmutableContentRef? result = null;
            var accepted = await operations.ExecuteAsync(new NativeOperation<MethodologyTemplate>(
                AcceptedOperationBinding.Of(provenance), () => new MethodologyTemplate(templateId, commandId),
                owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var valid = guard(owner, actor.MemberId);
                    if (!valid.IsSuccess)
                        return Result<FirstResponseDescriptor>.Failure(valid.Error!);
                    var current = await views.ReadAsync(owner, token).ConfigureAwait(false);
                    var rules = new MethodologyTemplateRules(templateId, current);
                    if (prepare(rules, actor.MemberId, actor.Display, provenance.AcceptedAt) is { } failure)
                        return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                    preparedRevision = owner.Revision;
                    var prepared = rules.Current ?? throw new ImmutableContentIntegrityException("The template prepared no version.");
                    version = prepared.Versions[^1];
                    var delta = prepared with { Versions = kind == nameof(RetireMethodologyTemplate) ? [] : [version] };
                    change = await content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                        MethodologyTemplateReader.Schema, delta, token).ConfigureAwait(false);
                    var body = await content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                        UnitResult.Schema, new UnitResult(), token).ConfigureAwait(false);
                    result = await content.WriteJsonAsync(MethodologyTemplate.Realm, Uuid.CreateVersion4(),
                        "mutation-result-content.v1", new MutationResultContent(UnitResult.Schema,
                            new StandardResponseBody(body)), token).ConfigureAwait(false);
                    return Result<FirstResponseDescriptor>.Success(new FirstResponseDescriptor(200,
                        ImmutableContentStoreExtensions.JsonMediaType, UnitResult.Schema, new StandardResponseBody(body)));
                },
                (owner, first) => owner.Append(kind, preparedRevision, provenance, actor.MemberId,
                    version!, change!, first, result!)), context, ct).ConfigureAwait(false);
            if (accepted.IsSuccess && accepted.Value is AcceptedAppendResult.Committed or AcceptedAppendResult.Replayed)
            {
                var descriptor = accepted.Value switch
                {
                    AcceptedAppendResult.Committed done => done.Response,
                    AcceptedAppendResult.Replayed replay => replay.Response,
                    _ => throw new ImmutableContentIntegrityException("A template has another original outcome."),
                };
                if (descriptor.ResultSchema != UnitResult.Schema || descriptor.Body is not StandardResponseBody body)
                    throw new ImmutableContentIntegrityException("A template operation has another original result schema.");
                _ = await content.ReadJsonAsync<UnitResult>(MethodologyTemplate.Realm, body.Content, ct).ConfigureAwait(false);
            }
            return UnitResponses.ToResult(accepted);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The template's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
