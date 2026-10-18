using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ApplyMethodologyTemplateHandler(IAggregateExecutor executor, IAggregateReader reader,
    TimeProvider clock) : IRequestHandler<ApplyMethodologyTemplate>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ApplyMethodologyTemplate> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TemplateId == Uuid.Empty || request.TemplateVersion <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, program, template, and published version are required."));

        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId, request.ProgramId), ct)
            .ConfigureAwait(false);
        if (!program.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The client program was not found."));

        var template = await reader.HydrateAsync(new MethodologyTemplate(request.TemplateId), ct)
            .ConfigureAwait(false);
        var version = template.Current?.Versions.FirstOrDefault(item => item.VersionNumber == request.TemplateVersion);
        if (version is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The methodology-template version was not found."));
        if (version.Status != "published")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only an approved published methodology-template version can be applied."));

        var actor = ClientServiceActor.Snapshot(context);
        return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId,
                TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey)),
            application => CommandFailureRequestAdapter.ToOutcome(application.Apply(version,
                request.ApplicationKey, actor.MemberId, actor.Display, clock.GetUtcNow())), context, ct)
            .ConfigureAwait(false);
    }
}
