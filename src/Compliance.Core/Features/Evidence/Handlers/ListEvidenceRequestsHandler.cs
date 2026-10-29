using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Evidence.Handlers;

/// <summary>Lists the program's evidence requests in the order opened, composed from their own owner streams.</summary>
public sealed class ListEvidenceRequestsHandler(EvidenceRequestReader requests)
    : IRequestHandler<ListEvidenceRequests, Page<EvidenceRequestView>>
{
    public async ValueTask<Result<Page<EvidenceRequestView>>> HandleAsync(
        IRequestContext<ListEvidenceRequests> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Status is not (null or EvidenceRequest.Open or EvidenceRequest.Fulfilled or
            EvidenceRequest.Cancelled))
            return Result<Page<EvidenceRequestView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The status filter must be open, fulfilled, or cancelled."));
        var snapshot = await requests.ReadAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        var items = snapshot.ReadAll().Where(item => request.Status is null || item.Status == request.Status)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "evidence requests");
    }
}
