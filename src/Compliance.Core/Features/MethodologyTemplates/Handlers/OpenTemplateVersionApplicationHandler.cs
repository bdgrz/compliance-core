using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Rules;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Resolves the authoritative application and opens its per-version entry outcome record.</summary>
public sealed class OpenTemplateVersionApplicationHandler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content) : IRequestHandler<OpenTemplateVersionApplication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<OpenTemplateVersionApplication> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only an application reaction may open its entry outcomes."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty ||
            request.TemplateVersion <= 0 || request.Source is null || request.Source.EventId == Uuid.Empty ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(OpenTemplateVersionApplication)))
            return Invalid();
        var (_, source, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateApplied>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (request.Source.Stream.Area != TemplateApplication.Area || source.TenantId != request.TenantId ||
            source.Header.TenantId != request.TenantId || source.Header.ProgramId != request.ProgramId ||
            source.Header.TemplateId != request.TemplateId || source.Header.TemplateVersionNumber != request.TemplateVersion ||
            source.SubjectId != source.Header.TemplateApplicationId || request.Source.Stream.Resource != source.SubjectId.ToString())
            return Invalid();
        var change = await content.ReadJsonAsync<TemplateApplicationMutationContent>(request.TenantId.ToString(),
            source.Content, ct).ConfigureAwait(false);
        if (change.Transition != "apply" || change.Application is not { } application)
            return Invalid();
        var rules = new TemplateApplicationRules(request.TenantId, request.ProgramId, source.SubjectId, application);
        if (!rules.HasValidCurrent || rules.Header() != source.Header)
            return Invalid();
        return await executor.ExecuteAsync(new TemplateVersionApplication(request.TenantId, request.ProgramId,
                request.TemplateId, request.TemplateVersion),
            owner => AggregateOutcome.CommitOnSuccess(owner.Open(request.Source.EventId,
                application.Drafts.Select(entry => entry.EntryKey).ToArray())), context, ct).ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Opening entry outcomes requires the exact committed application and its immutable entry set."));
}
