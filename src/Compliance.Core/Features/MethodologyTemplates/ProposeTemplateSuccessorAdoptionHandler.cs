using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ProposeTemplateSuccessorAdoptionHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ProposeTemplateSuccessorAdoption>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ProposeTemplateSuccessorAdoption> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var actor = ClientServiceActor.Snapshot(context);
        var operation = AuthoredCommandReplay.Create(new TemplateApplication(request.TenantId, request.ProgramId,
                TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey), request.CommandId), request.CommandId,
            nameof(ProposeTemplateSuccessorAdoption), actor.MemberId, request.ExpectedRevision, request, clock.GetUtcNow());
        var replayOwner = await reader.HydrateAsync(new TemplateApplication(request.TenantId, request.ProgramId,
                TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey), request.CommandId), ct).ConfigureAwait(false);
        if (AuthoredCommandReplay.Unit(replayOwner.ReplayAcceptance, operation) is { } replay)
            return replay;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.TemplateId == Uuid.Empty || request.NewTemplateVersion <= 0)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A tenant, program, template, and successor version are required."));

        var template = await reader.HydrateAsync(new MethodologyTemplate(request.TemplateId), ct)
            .ConfigureAwait(false);
        var successor = template.Current?.Versions.FirstOrDefault(item =>
            item.VersionNumber == request.NewTemplateVersion);
        if (successor is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The methodology-template successor version was not found."));
        if (successor.Status != "published")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a published methodology-template version can be proposed to an existing application."));


        return await executor.ExecuteAsync(new TemplateApplication(request.TenantId, request.ProgramId,
                TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey), request.CommandId),
            application => CommandFailureRequestAdapter.ToOutcome(application.ProposeSuccessor(
                request.ExpectedRevision, successor, request.Disposition, actor.MemberId, actor.Display,
                clock.GetUtcNow(), operation: operation)), context, ct).ConfigureAwait(false);
    }
}
