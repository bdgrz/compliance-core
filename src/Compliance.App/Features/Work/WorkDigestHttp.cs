using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Canonical platform route scope; query/body identities cannot override the selected owner or command.</summary>
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
        Uuid.TryParse(context.Request.RouteValues[name]?.ToString(), CultureInfo.InvariantCulture, out var value) && value != Uuid.Empty
            ? value : throw new BadHttpRequestException("Invalid digest route identity.");
    static DateOnly Week(HttpContext context) =>
        DateOnly.TryParseExact(context.Request.RouteValues["week_of"]?.ToString(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var week)
            ? week : throw new BadHttpRequestException("Invalid digest route week.");
}
