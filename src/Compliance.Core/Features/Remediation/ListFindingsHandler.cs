using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Lists findings with their readiness status evaluated at read time.</summary>
public sealed class ListFindingsHandler(IAggregateReader reader, IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<ListFindings, Page<FindingView>>
{
    public async ValueTask<Result<Page<FindingView>>> HandleAsync(
        IRequestContext<ListFindings> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ReadinessStatus is not (null or RemediationLedger.Closed or
            RemediationLedger.Remediated or RemediationLedger.Accepted or
            RemediationLedger.Overdue or RemediationLedger.Unresolved))
            return Result<Page<FindingView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The readiness status filter must be closed, remediated, accepted, overdue, or unresolved."));
        var source = await new FindingReadSource(reader, events).ReadAsync(request.TenantId,
            request.ProgramId, clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result<Page<FindingView>>.Failure(source.Error);
        var items = source.Value.Findings.Where(finding =>
            request.ReadinessStatus is null || finding.ReadinessStatus == request.ReadinessStatus)
            .ToArray();
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor, "findings");
    }
}
