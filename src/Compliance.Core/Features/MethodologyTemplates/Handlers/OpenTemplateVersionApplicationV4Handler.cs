using Cntryl.Portia;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

/// <summary>Opens entry outcomes only from the exact canonical application's committed plan.</summary>
public sealed class OpenTemplateVersionApplicationV4Handler(IAggregateExecutor executor, ISourceFactResolver sources,
    IImmutableContentStore content) : IRequestHandler<OpenTemplateVersionApplicationV4>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<OpenTemplateVersionApplicationV4> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "Only a committed application reaction opens entry outcomes."));
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateId == Uuid.Empty ||
            request.TemplateVersion <= 0 || request.Source is null || request.Source.EventId == Uuid.Empty ||
            request.Source.Stream != new TemplateApplication(request.TenantId, request.ProgramId, request.TemplateId, true).Stream ||
            request.CommandId != Uuid.CreateVersion5(request.Source.EventId, nameof(OpenTemplateVersionApplicationV4)))
            return Invalid();
        var (_, applied, _) = await SourceFactProofs.BuildAsync<MethodologyTemplateAppliedV4>(sources, content,
            request.TenantId.ToString(), request.Source, request.SourcePosition, ct).ConfigureAwait(false);
        if (applied.TenantId != request.TenantId || applied.ProgramId != request.ProgramId ||
            applied.TemplateId != request.TemplateId || applied.TemplateVersion != request.TemplateVersion || applied.CommandId == Uuid.Empty)
            return Invalid();
        return await executor.ExecuteAsync(new TemplateVersionApplication(request.TenantId, request.ProgramId,
            request.TemplateId, request.TemplateVersion), owner => AggregateOutcome.CommitOnSuccess(owner.Open(
                request.Source.EventId, applied.EntriesToCreate.Concat(applied.EntriesToPropose).ToArray(), applied.CommandId)), context, ct)
            .ConfigureAwait(false);
    }

    static Result Invalid() => Result.Failure(new RequestError(RequestErrorKind.Validation,
        "Opening outcomes requires the exact committed canonical application plan."));
}
