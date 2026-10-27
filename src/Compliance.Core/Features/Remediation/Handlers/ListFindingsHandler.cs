using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Queries;
using Bdgrz.Compliance.Features.Remediation.Readers;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Lists findings with their readiness status evaluated at read time.</summary>
public sealed class ListFindingsHandler(FindingReader findings, TimeProvider clock)
    : IRequestHandler<ListFindings, Page<FindingView>>
{
    public async ValueTask<Result<Page<FindingView>>> HandleAsync(IRequestContext<ListFindings> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.ReadinessStatus is not (null or FindingStates.Closed or FindingStates.Remediated or
            FindingStates.Accepted or FindingStates.Overdue or FindingStates.Unresolved))
            return Result<Page<FindingView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The readiness status filter must be closed, remediated, accepted, overdue, or unresolved."));
        var source = await findings.ReadAsync(request.TenantId, request.ProgramId, clock.GetUtcNow(), ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<Page<FindingView>>.Failure(source.Error);
        var items = source.Value.Findings.Where(finding =>
            request.ReadinessStatus is null || finding.ReadinessStatus == request.ReadinessStatus).ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "findings");
    }
}
