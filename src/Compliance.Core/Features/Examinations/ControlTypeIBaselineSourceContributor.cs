using Bdgrz.Compliance.Features.Snapshots;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures approved control versions effective on the Type I examination date.</summary>
public sealed class ControlTypeIBaselineSourceContributor(
    IControlDraftDirectoryReader directory, ControlReader controlSources,
    ControlDraftListReadConsistency consistency) : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const string DigestDomain = "bdgrz.snapshot.source.control_version.v1\0";

    public string Family => "controls";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => ReadCoreAsync(engagement, ct));

    async ValueTask<Result<TypeIBaselineSourceContribution>> ReadCoreAsync(
        ExaminationEngagementView engagement, CancellationToken ct)
    {
        var fence = await consistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!fence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(fence.Error);

        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        var seenControlIds = new HashSet<Uuid>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await directory.ListProgramAsync(engagement.TenantId, engagement.ProgramId,
                PageSize, cursor, ct).ConfigureAwait(false);
            if (page.Items is null || page.Items.Count > PageSize)
                return InvalidDirectory();

            foreach (var item in page.Items)
            {
                if (item is null || item.TenantId != engagement.TenantId ||
                    item.ProgramId != engagement.ProgramId || item.ControlId == Uuid.Empty ||
                    item.Revision < 1 || string.IsNullOrWhiteSpace(item.Identifier) ||
                    !seenControlIds.Add(item.ControlId))
                    return InvalidDirectory();

                var control = await controlSources.ReadAsync(engagement.TenantId, engagement.ProgramId, item.ControlId, ct)
                    .ConfigureAwait(false);
                if (!control.IsCreated || !control.IsVisible ||
                    control.TenantId != engagement.TenantId || control.Id != item.ControlId ||
                    control.ProgramId != engagement.ProgramId || control.Revision != item.Revision)
                    return SourceConflict();

                var version = control.EffectiveVersion(engagement.PeriodStart);
                if (version is null)
                {
                    if (control.IsApproved)
                        scopeDifferences.Add(
                            $"Control {item.Identifier} has no approved version effective on the Type I date.");
                    continue;
                }

                if (!IsValidEffectiveVersion(version, engagement, control, item.Identifier))
                    return SourceConflict();
                try
                {
                    sources.Add(new TypeIBaselineSourceReference("control_version", control.Id,
                        version.VersionId, version.Revision, Digest(version)));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                                                   InvalidOperationException or NotSupportedException)
                {
                    var difference = $"Control {item.Identifier} has invalid canonical source content.";
                    blockers.Add(difference);
                    scopeDifferences.Add(difference);
                }
            }

            var next = page.NextCursor;
            if (next is not null && (next == cursor || !seenCursors.Add(next)))
                return InvalidDirectory();
            cursor = next;
        } while (cursor is not null);

        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(engagement.TenantId,
            fence.Value, ct).ConfigureAwait(false);
        if (!confirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(confirmed.Error);

        return Result<TypeIBaselineSourceContribution>.Success(
            new TypeIBaselineSourceContribution(Family, sources.AsReadOnly(),
                blockers.AsReadOnly(),
                scopeDifferences.AsReadOnly()));
    }

    static bool IsValidEffectiveVersion(ControlVersionView version,
        ExaminationEngagementView engagement, ControlReadSnapshot control, string identifier) =>
        version.TenantId == engagement.TenantId && version.ProgramId == engagement.ProgramId &&
        version.ControlId == control.Id && version.Identifier == identifier &&
        version.VersionId != Uuid.Empty && version.Revision >= 1 &&
        version.Status is "approved" or "superseded" or "retired" &&
        version.EffectiveFrom <= engagement.PeriodStart &&
        (version.EffectiveUntil is null || version.EffectiveUntil > engagement.PeriodStart);

    static string Digest(ControlVersionView version)
    {
        // Status and EffectiveUntil are derived from later lifecycle events. The version content
        // and approval facts remain immutable when a successor or retirement is approved.
        var content = new ControlVersionSourceContent(version.TenantId, version.ProgramId,
            version.ControlId, version.Identifier, version.VersionId, version.Revision,
            version.ContentOrigin, version.Content, version.EffectiveFrom,
            version.PredecessorVersionId, version.OwnerAssignmentId, version.OwnerMemberId,
            version.OwnerPersonId, version.OwnerResolution, version.AcceptedReviewDecisionId,
            version.ApprovalDecisionId, version.ApprovedBy, version.ApprovalRationale,
            version.ApprovedAt, version.SeparationOfDutiesWaiverId);
        var element = JsonSerializer.SerializeToElement(content,
            ComplianceCoreJsonContext.Default.ControlVersionSourceContent);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name.Normalize(NormalizationForm.FormC));
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString()!.Normalize(NormalizationForm.FormC));
                break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var number))
                    throw new InvalidOperationException(
                        "Control version snapshot numbers must be signed 64-bit integers.");
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException(
                    "Unsupported content entered a control version snapshot.");
        }
    }

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The control directory returned an invalid or unstable program page."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The control source changed or did not match its program directory. Retry the preview.",
            isTransient: true));

}

sealed record ControlVersionSourceContent(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, Uuid VersionId, long Revision, string ContentOrigin,
    ControlDraftContent Content, DateOnly EffectiveFrom, Uuid? PredecessorVersionId,
    Uuid OwnerAssignmentId, Uuid OwnerMemberId, Uuid? OwnerPersonId, string OwnerResolution,
    Uuid AcceptedReviewDecisionId, Uuid ApprovalDecisionId, ActorReference ApprovedBy,
    string ApprovalRationale, DateTimeOffset ApprovedAt, Uuid? SeparationOfDutiesWaiverId);
