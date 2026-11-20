using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Queries;
namespace Bdgrz.Compliance.Features.Work;

/// <summary>Reads the selected tenant from its header and dispatch week from the query string.</summary>
static class WorkDigestHttp
{
    public static GetWorkDigestDispatchStatus BindStatus(HttpContext context) =>
        new(Id(context, "tenant_id"), Id(context, "member_id"), Week(context));

    public static async ValueTask<AuthorizeWorkDigestUnknownRetry> BindRetryAsync(HttpContext context,
        CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new AuthorizeWorkDigestUnknownRetry(Id(context, "command_id"), Id(context, "tenant_id"),
            Id(context, "member_id"), Week(context),
            PortiaHttpBinding.ReadBody<AuthorizeWorkDigestUnknownRetry, string>(body, options, 4,
                "EvidenceReference", "evidence_reference", false, false, default!),
            PortiaHttpBinding.ReadBody<AuthorizeWorkDigestUnknownRetry, string>(body, options, 5,
                "Rationale", "rationale", false, false, default!),
            PortiaHttpBinding.ReadBody<AuthorizeWorkDigestUnknownRetry, long>(body, options, 6,
                "ExpectedRevision", "expected_revision", false, false, default));
    }
    static Uuid Id(HttpContext context, string name) =>
        Uuid.TryParse(name == "tenant_id"
                ? PortiaHttpBinding.ReadHeader(context, "Tenant-Id")
                : context.Request.RouteValues[name]?.ToString(),
            CultureInfo.InvariantCulture, out var value) && value != Uuid.Empty
            ? value : throw new BadHttpRequestException($"Invalid digest request identity '{name}'.");
    static DateOnly Week(HttpContext context) =>
        DateOnly.TryParseExact(PortiaHttpBinding.ReadQuery(context, "week_of"), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var week)
            ? week : throw new BadHttpRequestException("Invalid digest query week.");
}
