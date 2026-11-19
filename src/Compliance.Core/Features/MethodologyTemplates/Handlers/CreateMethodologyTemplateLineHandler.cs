using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Creates the admitted idle line or records a definite source-bound immutable identity refusal.</summary>
public sealed class CreateMethodologyTemplateLineHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content, MethodologyTemplateLineReader lines, IRequestBus bus) : IRequestHandler<CreateMethodologyTemplateLine>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<CreateMethodologyTemplateLine> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a name admission reaction may create its line."));
        if (request.TemplateId == Uuid.Empty || !MethodologyTemplateCreationSources.IsClaimReference(request.Claim) ||
            request.CommandId != Uuid.CreateVersion5(request.Claim.EventId, nameof(CreateMethodologyTemplateLine)))
            return MethodologyTemplateCreationSources.Invalid();
        var admission = await MethodologyTemplateCreationSources.ResolveAsync(sources, content, request.TemplateId,
            request.Claim, request.ClaimPosition, ct).ConfigureAwait(false);
        if (admission is null)
            return MethodologyTemplateCreationSources.Invalid();
        var created = await executor.ExecuteAsync(new MethodologyTemplate(request.TemplateId),
            line => AggregateOutcome.CommitOnSuccess(line.CreateLineFromClaim(admission.Claim, admission.Intent, admission.Proof)),
            context, ct).ConfigureAwait(false);
        if (created.IsSuccess || created.Error.Kind != RequestErrorKind.Conflict)
            return created;
        var existing = await lines.CreationSourceAsync(request.TemplateId, ct).ConfigureAwait(false);
        if (existing is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The conflicting immutable template identity is not visible at the verified source cut yet.", isTransient: true));
        return await bus.SendAsync(new RefuseMethodologyTemplateCreation(request.TemplateId, request.Claim, request.ClaimPosition,
            NativeFactReference.Of(existing.Locator.Source, existing.Event), existing.Locator.ResourcePosition,
            MethodologyTemplateCreationSources.RefusalId(request.Claim.EventId, existing.Event.Metadata.EventId)), context, ct).ConfigureAwait(false);
    }
}
