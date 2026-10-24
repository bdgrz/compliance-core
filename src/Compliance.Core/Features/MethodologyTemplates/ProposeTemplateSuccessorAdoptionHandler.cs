using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

public sealed class ProposeTemplateSuccessorAdoptionHandler(TemplateApplicationOperations operations,
    MethodologyTemplateReader templates) : IRequestHandler<ProposeTemplateSuccessorAdoption>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ProposeTemplateSuccessorAdoption> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.NewTemplateVersion <= 0)
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An exact published successor version is required.")));
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.TemplateId,
            request.ApplicationKey, request.CommandId, nameof(ProposeTemplateSuccessorAdoption), request.ExpectedRevision,
            async (rules, actor, display, at, token) =>
            {
                var successor = await templates.VersionAsync(request.TemplateId, request.NewTemplateVersion, token)
                    .ConfigureAwait(false);
                if (successor is null)
                    return CommandFailure.MissingRecord("The methodology-template successor version was not found.");
                if (successor.Status != "published")
                    return CommandFailure.StateConflict("Only a published methodology-template version can be proposed to an existing application.");
                return rules.ProposeSuccessor(request.ExpectedRevision, successor, request.Disposition, actor, display, at);
            }, ct);
    }
}
