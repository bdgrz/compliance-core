using System.Globalization;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.SourceFacts.Readers;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Selected tenant/person/operation identities come only from canonical route segments.</summary>
static class PersonHttp
{
    public sealed record RecordBody(string DisplayName, string? WorkEmail = null, PersonalContactDetails? PersonalContact = null);
    public sealed record ReviseBody(long ExpectedRevision, string DisplayName, string? WorkEmail = null, PersonalContactDetails? PersonalContact = null);
    public sealed record CorrelateBody(long ExpectedRevision, Uuid? UserId = null);
    public static async ValueTask<RecordPerson> BindRecordAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var tenant = Identity(context, body, "tenant_id");
        var person = Identity(context, body, "person_id");
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new RecordPerson(person, tenant,
            PortiaHttpBinding.ReadBody<RecordPerson, string>(body, options, 2, "DisplayName", "display_name", false, false, default!),
            PortiaHttpBinding.ReadBody<RecordPerson, string?>(body, options, 3, "WorkEmail", "work_email", true, true, default),
            PortiaHttpBinding.ReadBody<RecordPerson, PersonalContactDetails?>(body, options, 4, "PersonalContact", "personal_contact", true, true, default));
    }
    public static async ValueTask<RevisePerson> BindReviseAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var tenant = Identity(context, body, "tenant_id");
        var person = Identity(context, body, "person_id");
        var command = Identity(context, body, "command_id");
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new RevisePerson(command, tenant, person,
            PortiaHttpBinding.ReadBody<RevisePerson, long>(body, options, 3, "ExpectedRevision", "expected_revision", false, false, default),
            PortiaHttpBinding.ReadBody<RevisePerson, string>(body, options, 4, "DisplayName", "display_name", false, false, default!),
            PortiaHttpBinding.ReadBody<RevisePerson, string?>(body, options, 5, "WorkEmail", "work_email", true, true, default),
            PortiaHttpBinding.ReadBody<RevisePerson, PersonalContactDetails?>(body, options, 6, "PersonalContact", "personal_contact", true, true, default));
    }
    public static async ValueTask<CorrelatePersonMembership> BindCorrelateAsync(HttpContext context, CancellationToken ct)
    {
        using var document = await PortiaHttpBinding.ReadJsonBodyAsync(context, true, ct).ConfigureAwait(false);
        var body = document.RootElement;
        var tenant = Identity(context, body, "tenant_id");
        var person = Identity(context, body, "person_id");
        var command = Identity(context, body, "command_id");
        var options = PortiaHttpBinding.GetJsonOptions(context);
        return new CorrelatePersonMembership(command, tenant, person,
            PortiaHttpBinding.ReadBody<CorrelatePersonMembership, long>(body, options, 3, "ExpectedRevision", "expected_revision", false, false, default),
            PortiaHttpBinding.ReadBody<CorrelatePersonMembership, Uuid?>(body, options, 4, "UserId", "user_id", true, true, default));
    }
    static Uuid Identity(HttpContext context, JsonElement body, string name)
    {
        if (!Uuid.TryParse(context.Request.RouteValues[name]?.ToString(), CultureInfo.InvariantCulture, out var value) || value == Uuid.Empty)
            throw new BadHttpRequestException("Invalid person route identity.");
        if (body.TryGetProperty(name, out var supplied) && (supplied.ValueKind != JsonValueKind.String ||
            !Uuid.TryParse(supplied.GetString(), CultureInfo.InvariantCulture, out var parsed) || parsed != value))
            throw new BadHttpRequestException("The body identity conflicts with the canonical person route.");
        return value;
    }
}
