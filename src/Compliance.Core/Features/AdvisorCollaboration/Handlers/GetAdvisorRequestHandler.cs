using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>Reads one handoff from its owning stream and verifies the requested program scope.</summary>
public sealed class GetAdvisorRequestHandler(AdvisorHandoffReader handoffs)
    : IRequestHandler<GetAdvisorRequest, AdvisorRequestView>
{
    public ValueTask<Result<AdvisorRequestView>> HandleAsync(
        IRequestContext<GetAdvisorRequest> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<AdvisorRequestView>> HandleCoreAsync(
        IRequestContext<GetAdvisorRequest> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.AdvisorRequestId == Uuid.Empty)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.Validation,
                "An advisor handoff read requires tenant, program, and handoff identities."));

        var handoff = await handoffs.ReadAsync(request.TenantId, request.ProgramId,
            request.AdvisorRequestId, ct).ConfigureAwait(false);
        if (!handoff.IsCreated || handoff.ProgramId != request.ProgramId)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The advisor handoff was not found in the requested program."));

        if (handoff.Purpose is not { } purpose || handoff.CreatedBy is not { } createdBy ||
            handoff.CreatedAt is not { } createdAt || handoff.UpdatedAt is not { } updatedAt)
            return Result<AdvisorRequestView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The advisor handoff history is incomplete."));

        return Result<AdvisorRequestView>.Success(new AdvisorRequestView(request.TenantId,
            request.ProgramId, request.AdvisorRequestId, handoff.Selection, purpose,
            handoff.ExpiresOn, handoff.Status, handoff.Revision, createdBy, createdAt,
            updatedAt, handoff.ComparedToHandoffId));
    }
}
