using Bdgrz.Compliance.Features.MethodologyTemplates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class ApplyMethodologyTemplateHandler(TemplateApplicationOperations operations,
    MethodologyTemplateReader templates) : IRequestHandler<ApplyMethodologyTemplate>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ApplyMethodologyTemplate> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TemplateVersion <= 0 || string.IsNullOrWhiteSpace(request.ApplicationKey))
            return ValueTask.FromResult(Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An exact published template version and application key are required.")));
        var applicationId = TemplateApplicationId.For(request.ProgramId, request.TemplateId, request.ApplicationKey);
        return operations.ExecuteAsync(context, request.TenantId, request.ProgramId, request.TemplateId,
            request.ApplicationKey, applicationId, nameof(ApplyMethodologyTemplate), null,
            async (rules, actor, display, at, token) =>
            {
                var program = await operations.Sources.HydrateAsync(new ComplianceProgram(request.TenantId,
                    request.ProgramId), token).ConfigureAwait(false);
                if (!program.IsCreated)
                    return CommandFailure.MissingRecord("The client program was not found.");
                var version = await templates.VersionAsync(request.TemplateId, request.TemplateVersion, token)
                    .ConfigureAwait(false);
                if (version is null)
                    return CommandFailure.MissingRecord("The methodology-template version was not found.");
                if (version.Status != "published")
                    return CommandFailure.StateConflict("Only an approved published methodology-template version can be applied.");
                return rules.Apply(version, request.ApplicationKey, actor, display, at);
            }, ct);
    }
}
