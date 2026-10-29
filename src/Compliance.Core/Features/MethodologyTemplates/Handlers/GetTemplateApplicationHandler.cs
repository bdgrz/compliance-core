using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Queries;
using Bdgrz.Compliance.Features.MethodologyTemplates.Readers;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Handlers;

public sealed class GetTemplateApplicationHandler(TemplateApplicationReader applications)
    : IRequestHandler<GetTemplateApplication, TemplateApplicationView>
{
    public async ValueTask<Result<TemplateApplicationView>> HandleAsync(IRequestContext<GetTemplateApplication> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.TemplateApplicationId == Uuid.Empty)
            return Result<TemplateApplicationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A template application read requires its tenant, program and application identity."));
        try
        {
            var snapshot = await applications.ReadAsync(request.TenantId, request.ProgramId,
                request.TemplateApplicationId, ct).ConfigureAwait(false);
            return snapshot.View is { } view ? Result<TemplateApplicationView>.Success(view)
                : Result<TemplateApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The template application was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TemplateApplicationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The template application's immutable content cannot be verified.",
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }
}
