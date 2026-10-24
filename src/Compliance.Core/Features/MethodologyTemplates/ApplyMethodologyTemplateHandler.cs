using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ApplyMethodologyTemplateHandler(IAggregateExecutor executor, IAggregateReader reader, MethodologyTemplateReader templates,
    TimeProvider clock) : IRequestHandler<ApplyMethodologyTemplate>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyMethodologyTemplate> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TemplateId == Uuid.Empty || request.TemplateVersion <= 0 ||
            string.IsNullOrWhiteSpace(request.ApplicationKey))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, program, template, and published version are required."));

        var actor = ClientServiceActor.Snapshot(context);
        var retained = await reader.HydrateAsync(new TemplateApplication(request.TenantId, request.ProgramId,
            TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey)), ct)
            .ConfigureAwait(false);
        if (retained.Current is { } original)
        {
            if (original.TemplateId != request.TemplateId || original.TemplateVersionNumber != request.TemplateVersion)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A template application identity cannot be reused for another published version."));
            var failure = retained.Apply(original.SourceVersion, request.ApplicationKey, actor.MemberId,
                actor.Display, clock.GetUtcNow());
            return failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
        }

        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The client program was not found."));

        try
        {
            var version = await templates.VersionAsync(request.TemplateId, request.TemplateVersion, ct)
                .ConfigureAwait(false);
            if (version is null)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The methodology-template version was not found."));
            if (version.Status != "published")
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "Only an approved published methodology-template version can be applied."));

            return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId,
                    TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey)),
                application => CommandFailureRequestAdapter.ToOutcome(application.Apply(version,
                    request.ApplicationKey, actor.MemberId, actor.Display, clock.GetUtcNow())), context, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The methodology template's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
