using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Queries;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

/// <summary>Reads the applicability line holding one proposal from its own owner stream, its authoritative source.</summary>
public sealed class GetCriterionApplicabilityHandler(ApplicabilityReader applicability)
    : IRequestHandler<GetCriterionApplicability, CriterionApplicabilityView>
{
    public ValueTask<Result<CriterionApplicabilityView>> HandleAsync(
        IRequestContext<GetCriterionApplicability> context, CancellationToken ct) =>
        MappingLineFailures.ExecuteAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<CriterionApplicabilityView>> HandleCoreAsync(
        IRequestContext<GetCriterionApplicability> context, CancellationToken ct)
    {
        var request = context.Request;
        return await applicability.ReadViewAsync(request.TenantId, request.ProgramId, request.DecisionId, ct)
                .ConfigureAwait(false) is { } view
            ? Result<CriterionApplicabilityView>.Success(view)
            : Result<CriterionApplicabilityView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The criterion applicability decision was not found."));
    }
}
