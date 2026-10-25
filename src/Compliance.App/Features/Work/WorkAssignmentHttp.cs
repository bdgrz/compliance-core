using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Canonical selected work assignment route scope; no body or query identity may override it.</summary>
static class WorkAssignmentHttp
{
    public sealed record ClaimBody(long ExpectedRevision);
    public sealed record AssignBody(long ExpectedRevision, Uuid AssigneeMemberId, string? Reason = null);
    public sealed record DelegateBody(long ExpectedRevision, Uuid AssigneeMemberId, string Reason);
    public sealed record EscalateBody(long ExpectedRevision, string Reason);

    public static async ValueTask<ClaimWorkItem> BindClaimAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var (command, tenant, program, item) = Scope(context, document.RootElement);
        return new ClaimWorkItem(command, tenant, program, item, Revision<ClaimWorkItem>(context, document.RootElement));
    }
    public static async ValueTask<AssignWorkItem> BindAssignAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var (command, tenant, program, item) = Scope(context, document.RootElement);
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new AssignWorkItem(command, tenant, program, item, Revision<AssignWorkItem>(context, document.RootElement),
            PortiaHttpBinding.ReadBody<AssignWorkItem, Uuid>(document.RootElement, options, 5, "AssigneeMemberId", "assignee_member_id", false, false, default),
            PortiaHttpBinding.ReadBody<AssignWorkItem, string?>(document.RootElement, options, 6, "Reason", "reason", true, true, default));
    }
    public static async ValueTask<DelegateWorkItem> BindDelegateAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var (command, tenant, program, item) = Scope(context, document.RootElement);
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new DelegateWorkItem(command, tenant, program, item, Revision<DelegateWorkItem>(context, document.RootElement),
            PortiaHttpBinding.ReadBody<DelegateWorkItem, Uuid>(document.RootElement, options, 5, "AssigneeMemberId", "assignee_member_id", false, false, default),
            PortiaHttpBinding.ReadBody<DelegateWorkItem, string>(document.RootElement, options, 6, "Reason", "reason", false, false, default!));
    }
    public static async ValueTask<EscalateWorkItem> BindEscalateAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var (command, tenant, program, item) = Scope(context, document.RootElement);
        return new EscalateWorkItem(command, tenant, program, item, Revision<EscalateWorkItem>(context, document.RootElement),
            PortiaHttpBinding.ReadBody<EscalateWorkItem, string>(document.RootElement, PortiaHttpBinding.GetJsonOptions(context), 5, "Reason", "reason", false, false, default!));
    }
    static long Revision<T>(HttpContext context, JsonElement body) where T : IRequestBase => PortiaHttpBinding.ReadBody<T, long>(body,
        PortiaHttpBinding.GetJsonOptions(context), 4, "ExpectedRevision", "expected_revision", false, false, default);

    static (Uuid Command, Uuid Tenant, Uuid Program, Uuid Item) Scope(HttpContext context, JsonElement body)
    {
        var values = new List<Uuid>(4);
        foreach (var name in new[] { "command_id", "tenant_id", "program_id", "work_item_id" })
        {
            if (!Uuid.TryParse(context.Request.RouteValues[name]?.ToString(), CultureInfo.InvariantCulture, out var value) || value == Uuid.Empty)
                throw new BadHttpRequestException("Invalid work assignment route identity.");
            if (body.TryGetProperty(name, out var supplied) && (supplied.ValueKind != JsonValueKind.String ||
                !Uuid.TryParse(supplied.GetString(), CultureInfo.InvariantCulture, out var parsed) || parsed != value))
                throw new BadHttpRequestException("The body identity conflicts with the canonical work assignment route.");
            values.Add(value);
        }
        return (values[0], values[1], values[2], values[3]);
    }
}
