using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures application revisions included by the exact approved Type I boundary.</summary>
public sealed class ApplicationTypeIBaselineSourceContributor(
    IApplicationDirectoryReader applications, IAggregateReader reader,
    RestrictedApplicationVisibility visibility, SystemInstanceReadConsistency consistency,
    IBoundaryDirectoryReader boundaries, ProgramSetupWorkReadConsistency boundaryConsistency)
    : ITypeIBaselineSourceContributor
{
    const int PageSize = 200;
    const string RevisionDigestDomain = "bdgrz.snapshot.source.application_revision.v1\0";
    const string RetirementDigestDomain = "bdgrz.snapshot.source.application_retirement.v1\0";

    public string Family => "applications";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation, "An application baseline read requires its engagement and source context."));

        var priorBoundary = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "approved_boundary").ToArray();
        if (priorBoundary.Length != 1 || priorBoundary[0].RecordId != engagement.BoundaryId)
            return Success([], ["Applications were not read from the engagement's exact approved boundary."], []);

        var boundaryFence = await boundaryConsistency.CaptureAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryFence.Error);
        var applicationFence = await consistency.CaptureApplicationListAsync(engagement.TenantId, ct)
            .ConfigureAwait(false);
        if (!applicationFence.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(applicationFence.Error);

        var boundary = await boundaries.GetAsync(engagement.TenantId, engagement.BoundaryId, ct)
            .ConfigureAwait(false);
        var boundaryVersion = await boundaries.GetEffectiveVersionAsync(engagement.TenantId,
            engagement.BoundaryId, engagement.PeriodStart, ct).ConfigureAwait(false);
        if (boundary is null || boundary.TenantId != engagement.TenantId ||
            boundary.ProgramId != engagement.ProgramId || boundary.BoundaryId != engagement.BoundaryId ||
            boundaryVersion is null || boundaryVersion.TenantId != engagement.TenantId ||
            boundaryVersion.ProgramId != engagement.ProgramId ||
            boundaryVersion.BoundaryId != engagement.BoundaryId ||
            boundaryVersion.VersionId != priorBoundary[0].VersionId ||
            boundaryVersion.Revision != priorBoundary[0].Revision ||
            boundaryVersion.Status != "approved" ||
            boundaryVersion.EffectiveFrom is not { } effectiveFrom ||
            effectiveFrom > engagement.PeriodStart)
            return BoundaryChanged();

        string boundaryDigest;
        try
        {
            boundaryDigest = SnapshotContentIdentity.ApprovedBoundaryVersion(boundaryVersion);
        }
        catch (SnapshotContentException)
        {
            return Success([], ["The application scope has invalid approved boundary content."], []);
        }
        if (!StringComparer.Ordinal.Equals(boundaryDigest, priorBoundary[0].ContentSha256))
            return BoundaryChanged();

        var effectiveThrough = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        var through = context.RecordedThrough ?? effectiveThrough;
        var applicationIds = boundaryVersion.Content.Entries
            .Where(static entry => entry.Kind == "inclusion" && entry.SubjectType == "application" &&
                                   !entry.Unresolved && entry.GovernedRecordId is not null)
            .Select(static entry => entry.GovernedRecordId!.Value)
            .Distinct()
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal)
            .ToArray();
        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();

        foreach (var applicationId in applicationIds)
        {
            var current = await applications.GetAsync(engagement.TenantId, applicationId, ct)
                .ConfigureAwait(false);
            if (current is null)
            {
                blockers.Add("An application included by the approved boundary has no source history.");
                continue;
            }
            if (current.TenantId != engagement.TenantId || current.ApplicationId != applicationId ||
                current.Revision < 1)
                return InvalidDirectory();

            var aggregate = await reader.HydrateApplicationAsync(engagement.TenantId,
                applicationId, ct).ConfigureAwait(false);
            if (!aggregate.IsCreated)
                return SourceConflict();
            if (aggregate.Revision != current.Revision)
                return SourceConflict();

            var history = await ReadHistoryAsync(engagement.TenantId, applicationId, ct)
                .ConfigureAwait(false);
            if (!history.IsSuccess)
                return Result<TypeIBaselineSourceContribution>.Failure(history.Error);
            if (history.Value.Count != current.Revision ||
                history.Value[^1].Revision != current.Revision)
                return SourceConflict();

            var retirementRows = history.Value.Where(static revision =>
                revision.ChangeKind == "retired").ToArray();
            if (retirementRows.Length > 1 || retirementRows.Any(static revision =>
                    revision.Lifecycle != "retired" || revision.Retirement is null ||
                    revision.Retirement.EffectiveAt == default ||
                    string.IsNullOrWhiteSpace(revision.Retirement.Reason) ||
                    revision.Retirement.MergedIntoApplicationId == Uuid.Empty ||
                    revision.Retirement.MergedIntoApplicationRevision is < 1 ||
                    (revision.Retirement.MergedIntoApplicationId is null) !=
                    (revision.Retirement.MergedIntoApplicationRevision is null)) ||
                history.Value.Any(static revision => revision.ChangeKind != "retired" &&
                    (revision.Lifecycle != "active" || revision.Retirement is not null)) ||
                history.Value.Any(static revision => revision.ChangeKind is
                    not ("declared" or "imported" or "revised" or
                        "system_instance_declared" or "retired")) ||
                history.Value[0].ChangeKind is not ("declared" or "imported") ||
                history.Value.Skip(1).Any(static revision => revision.ChangeKind is "declared" or "imported") ||
                retirementRows.Length == 1 && history.Value[^1].ChangeKind != "retired")
                return InvalidDirectory();

            var applicationVersion = history.Value.Where(revision =>
                    revision.ChangeKind != "retired" && revision.LastChangedAt <= through)
                .OrderBy(static revision => revision.Revision).LastOrDefault();
            if (applicationVersion is null)
            {
                blockers.Add("An application included by the approved boundary has no version recorded by the Type I date.");
                continue;
            }

            var retirement = retirementRows.SingleOrDefault(revision =>
                revision.LastChangedAt <= through || revision.Retirement!.EffectiveAt <= effectiveThrough);
            var authorized = true;
            if (applicationVersion.IsRestricted || retirement?.IsRestricted == true)
                authorized = context.RequesterUserId != Uuid.Empty &&
                    await visibility.HasRestrictedApplicationPermissionAsync(engagement.TenantId,
                        context.RequesterUserId, applicationId, ct).ConfigureAwait(false);
            if (!authorized)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Forbidden,
                    "The requester is not authorized to read every restricted application in the Type I boundary."));

            try
            {
                sources.Add(new TypeIBaselineSourceReference("application_revision", applicationId,
                    RevisionVersionId(applicationId, applicationVersion.Revision),
                    applicationVersion.Revision, RevisionDigest(applicationVersion)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException)
            {
                blockers.Add($"Application {applicationId} has invalid canonical source content.");
                scopeDifferences.Add($"Application {applicationId} has invalid canonical source content.");
            }
            foreach (var unresolved in applicationVersion.Unresolved.Distinct(StringComparer.Ordinal)
                         .OrderBy(static value => value, StringComparer.Ordinal))
                scopeDifferences.Add($"Application {applicationId} has unresolved source field {unresolved} on the Type I date.");

            if (retirement is not null)
            {
                try
                {
                    sources.Add(new TypeIBaselineSourceReference("application_retirement", applicationId,
                        RetirementVersionId(applicationId, retirement.Revision), retirement.Revision,
                        RetirementDigest(retirement)));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or
                                                   InvalidOperationException or NotSupportedException)
                {
                    blockers.Add($"Application {applicationId} has invalid canonical retirement content.");
                    scopeDifferences.Add($"Application {applicationId} has invalid canonical retirement content.");
                }
                if (retirement.Retirement!.EffectiveAt > effectiveThrough)
                    scopeDifferences.Add($"Application {applicationId} has a retirement scheduled after the Type I date.");
                else if (retirement.LastChangedAt > through)
                    scopeDifferences.Add($"Application {applicationId} was retired retroactively after the Type I date.");
            }
        }

        var applicationsConfirmed = await consistency.ConfirmApplicationListUnchangedAndCaughtUpAsync(
            engagement.TenantId, applicationFence.Value, ct).ConfigureAwait(false);
        if (!applicationsConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(applicationsConfirmed.Error);
        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            engagement.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(boundaryConfirmed.Error);

        return Success(sources, blockers, scopeDifferences);
    }

    async ValueTask<Result<IReadOnlyList<ApplicationRevisionView>>> ReadHistoryAsync(Uuid tenantId,
        Uuid applicationId, CancellationToken ct)
    {
        var history = new List<ApplicationRevisionView>();
        var revisions = new HashSet<long>();
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            Page<ApplicationRevisionView>? page;
            try
            {
                page = await applications.ListRevisionsAsync(tenantId, applicationId, PageSize,
                    cursor, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is KvDirectoryQueryException or
                                               ApplicationImportVisibilityChangedException)
            {
                return Result<IReadOnlyList<ApplicationRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application revision history changed while the Type I baseline was read. Retry the preview.",
                    isTransient: true));
            }
            if (page is null || page.Items is null || page.Items.Count > PageSize)
                return Result<IReadOnlyList<ApplicationRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application revision history is unavailable or invalid."));
            foreach (var revision in page.Items)
            {
                if (revision is null || revision.TenantId != tenantId ||
                    revision.ApplicationId != applicationId || revision.Revision < 1 ||
                    revision.LastChangedAt == default || revision.Unresolved is null ||
                    revision.Unresolved.Any(static value => value is null) ||
                    string.IsNullOrWhiteSpace(revision.Name) ||
                    string.IsNullOrWhiteSpace(revision.Purpose) ||
                    string.IsNullOrWhiteSpace(revision.SourceKind) ||
                    string.IsNullOrWhiteSpace(revision.SourceIdentifier) ||
                    revision.LastChangedByMemberId == Uuid.Empty ||
                    string.IsNullOrWhiteSpace(revision.LastChangedByDisplay) ||
                    revision.ChangeKind == "system_instance_declared" &&
                    (revision.SystemInstanceId is null || revision.SystemInstance is null ||
                     revision.SystemInstance.SystemInstanceId != revision.SystemInstanceId ||
                     revision.SystemInstance.TenantId != tenantId ||
                     revision.SystemInstance.ApplicationId != applicationId) ||
                    !revisions.Add(revision.Revision))
                    return Result<IReadOnlyList<ApplicationRevisionView>>.Failure(new RequestError(
                        RequestErrorKind.Conflict,
                        "The application revision history returned invalid or duplicate rows."));
                history.Add(revision);
            }
            var next = page.NextCursor;
            if (next is not null && (next == cursor || !cursors.Add(next)))
                return Result<IReadOnlyList<ApplicationRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application revision history returned an invalid cursor."));
            cursor = next;
        } while (cursor is not null);

        history.Sort(static (left, right) => left.Revision.CompareTo(right.Revision));
        for (var index = 0; index < history.Count; index++)
            if (history[index].Revision != index + 1)
                return Result<IReadOnlyList<ApplicationRevisionView>>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The application revision history is incomplete."));
        return Result<IReadOnlyList<ApplicationRevisionView>>.Success(history.AsReadOnly());
    }

    static Uuid RevisionVersionId(Uuid applicationId, long revision) =>
        Uuid.CreateVersion5(applicationId, $"application_revision_v1:{revision}");

    static Uuid RetirementVersionId(Uuid applicationId, long revision) =>
        Uuid.CreateVersion5(applicationId, $"application_retirement_v1:{revision}");

    static string RevisionDigest(ApplicationRevisionView revision)
    {
        var content = new ApplicationRevisionSourceContent(revision.TenantId,
            revision.ApplicationId, revision.Revision, revision.Name, revision.Purpose,
            revision.OwnerReference, revision.SourceKind, revision.SourceIdentifier,
            revision.HasSystemInstances, revision.Unresolved, revision.LastChangedByMemberId,
            revision.LastChangedByDisplay, revision.Actor, revision.LastChangedAt,
            revision.ChangeKind, revision.SystemInstanceId, revision.SystemInstance,
            revision.Classification, revision.ApplicationKind,
            revision.Criticality, revision.IsRestricted, revision.SystemOwnerPersonId,
            revision.AccessOwnerPersonId);
        return Digest(RevisionDigestDomain, content,
            ComplianceCoreJsonContext.Default.ApplicationRevisionSourceContent);
    }

    static string RetirementDigest(ApplicationRevisionView revision)
    {
        var retirement = revision.Retirement!;
        var content = new ApplicationRetirementSourceContent(revision.TenantId,
            revision.ApplicationId, revision.Revision, retirement.EffectiveAt, retirement.Reason,
            retirement.MergedIntoApplicationId, retirement.MergedIntoApplicationRevision,
            revision.LastChangedByMemberId, revision.LastChangedByDisplay, revision.Actor,
            revision.LastChangedAt);
        return Digest(RetirementDigestDomain, content,
            ComplianceCoreJsonContext.Default.ApplicationRetirementSourceContent);
    }

    static string Digest<T>(string domain, T content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var element = JsonSerializer.SerializeToElement(content, typeInfo);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
            WriteCanonical(writer, element);
        var prefix = Encoding.UTF8.GetBytes(domain);
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
                        "Application snapshot numbers must be signed 64-bit integers.");
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
                    "Unsupported content entered an application snapshot.");
        }
    }

    static Result<TypeIBaselineSourceContribution> BoundaryChanged() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The approved boundary changed while application scope was being read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> InvalidDirectory() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The application directory returned invalid or inconsistent baseline source data."));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "The application source or revision history changed during the baseline read. Retry the preview.",
            isTransient: true));

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources,
        IReadOnlyList<string> blockers, IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new TypeIBaselineSourceContribution(
            "applications", sources, blockers, scopeDifferences));
}
