using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.Readiness.Readers;
namespace Bdgrz.Compliance.Features.Readiness.Handlers;

/// <summary>Reads one adopted assessment composed from its own owner streams.</summary>
public sealed class GetReadinessAssessmentHandler(ReadinessReader readiness)
    : IRequestHandler<GetReadinessAssessment, ReadinessAssessmentView>
{
    public async ValueTask<Result<ReadinessAssessmentView>> HandleAsync(
        IRequestContext<GetReadinessAssessment> context, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            return await readiness.ReadViewAsync(request.TenantId, request.ProgramId, request.AssessmentId, ct)
                .ConfigureAwait(false) is { } view
                ? Result<ReadinessAssessmentView>.Success(view)
                : Result<ReadinessAssessmentView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The readiness assessment was not found."));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<ReadinessAssessmentView>.Failure(Unverified(exception));
        }
    }

    internal static RequestError Unverified(ImmutableContentIntegrityException exception) => new(
        RequestErrorKind.Conflict, $"The readiness content cannot be verified: {exception.Message}",
        exception is ImmutableContentUnavailableException);
}
