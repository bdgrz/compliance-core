using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Rules;

/// <summary>Small per-fact bounds; these do not limit the number of independent entries.</summary>
static class CriteriaContentBounds
{
    internal const int IdentifierBytes = 128;
    internal const int SummaryBytes = 2000;
    internal const int DisclosureCodeBytes = 128;
    internal const int DisclosureNoteBytes = 2000;
    internal const int SeedVersionBytes = 128;
    internal const int CoverageNoteBytes = 2000;
    internal const int ContentRightsBytes = 2000;
    internal const int SourceUrlBytes = 1024;
    internal const int EventPayloadBytes = 8 * 1024;

    static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static bool Identifier([NotNullWhen(true)] string? value) => Text(value, IdentifierBytes);
    internal static bool Summary([NotNullWhen(true)] string? value) => Text(value, SummaryBytes);

    internal static bool Text([NotNullWhen(true)] string? value, int maxBytes)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxBytes)
            return false;
        try
        {
            // The actual escaped JSON size is checked for the complete event by Payload.
            return StrictUtf8.GetByteCount(value) <= maxBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    internal static bool StructuralIntent(CriterionChangeIntent? intent) => intent is not null &&
        Identifier(intent.Identifier) && (intent.Action is "create" or "replace" or "remove") &&
        (intent.Kind is null or "criterion" or "point_of_focus") &&
        (intent.Category is null or "security" or "availability" or "confidentiality" or "processing_integrity" or "privacy") &&
        (intent.ParentIdentifier is null || Identifier(intent.ParentIdentifier)) &&
        (intent.Summary is null || Summary(intent.Summary));

    // Shape validation must run before an operation ID is durably claimed.
    // State-dependent rules such as revision and existence remain with the entry owner.
    internal static bool StructuralShape(CriterionChangeIntent? intent)
    {
        if (!StructuralIntent(intent) || intent!.EditionId == Uuid.Empty || intent.OperationId == Uuid.Empty ||
            intent.DraftGeneration == Uuid.Empty || intent.ExpectedRevision < 0 ||
            intent.Identifier != intent.Identifier.Trim())
            return false;
        if (intent.Action == "remove")
            return intent.Kind is null && intent.Category is null && intent.ParentIdentifier is null &&
                intent.ParentIncarnationId is null && intent.Summary is null;
        if (intent.Action is not ("create" or "replace") || !Summary(intent.Summary) ||
            intent.Category is not ("security" or "availability" or "confidentiality" or "processing_integrity" or "privacy"))
            return false;
        return intent.Kind switch
        {
            "criterion" => intent.ParentIdentifier is null && intent.ParentIncarnationId is null,
            "point_of_focus" => Identifier(intent.ParentIdentifier) &&
                intent.ParentIdentifier == intent.ParentIdentifier.Trim() &&
                intent.ParentIdentifier != intent.Identifier &&
                intent.ParentIncarnationId is { } parent && parent != Uuid.Empty,
            _ => false,
        };
    }

    internal static bool SummaryIntent(CriterionSummaryIntent? intent) => intent is not null &&
        Identifier(intent.Identifier) && Summary(intent.Summary);

    // Avoid serializing an arbitrarily large receipt into a temporary allocation.
    // Claims retain their existing formats and are limited by the total payload.
    internal static bool AdmissionFields(ActorAttribution? actor, AccessAdmissionEvidence? evidence)
    {
        if (actor is null || evidence is null)
            return false;
        var remaining = EventPayloadBytes;
        if (!Consume(actor.Subject, ref remaining) || !Consume(actor.Issuer, ref remaining) ||
            !Consume(evidence.ActorSubject, ref remaining) || !Consume(evidence.ActorIssuer, ref remaining) ||
            !Consume(evidence.Target, ref remaining) || !Consume(evidence.IntentSha256, ref remaining) ||
            !Consume(evidence.PolicyGeneration, ref remaining) ||
            evidence.PolicyContentSha256 is not null && !Consume(evidence.PolicyContentSha256, ref remaining))
            return false;
        return ConsumeSource(evidence.Identity, ref remaining) && ConsumeSource(evidence.Policy, ref remaining) &&
               ConsumeSource(evidence.Session, ref remaining) && ConsumeSource(evidence.Operator, ref remaining) &&
               ConsumeSource(evidence.Tenant, ref remaining) && ConsumeSource(evidence.Member, ref remaining) &&
               ConsumeSource(evidence.Primary, ref remaining) && ConsumeSource(evidence.TeamMember, ref remaining) &&
               ConsumeSource(evidence.Team, ref remaining) && ConsumeSource(evidence.TeamRole, ref remaining) &&
               ConsumeSource(evidence.Role, ref remaining) && ConsumeSource(evidence.RolePermission, ref remaining) &&
               ConsumeSource(evidence.Account, ref remaining) && ConsumeSource(evidence.GlobalVeto, ref remaining);
    }

    internal static bool ActorFields(ActorAttribution? actor)
    {
        if (actor is null)
            return false;
        var remaining = EventPayloadBytes;
        return Consume(actor.Subject, ref remaining) && Consume(actor.Issuer, ref remaining);
    }

    // This is the actual source-generated JSON business payload. Portia's
    // separately attributed persistence envelope is checked by the event store.
    internal static bool Payload(DomainEvent value) => DomainEventPayload.Size(value) <= EventPayloadBytes;

    static bool ConsumeSource(AccessAdmissionSourceVersion? source, ref int remaining) => source is null ||
        Consume(source.Stream, ref remaining) && Consume(source.Generation, ref remaining);

    static bool Consume(string? value, ref int remaining)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > remaining)
            return false;
        try
        {
            if (StrictUtf8.GetByteCount(value) > remaining)
                return false;
            var length = JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;
            if (length > remaining)
                return false;
            remaining -= length;
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}
