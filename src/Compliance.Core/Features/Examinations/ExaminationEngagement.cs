using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>
///     One Type I engagement stream owns its setup and immutable baseline lineage.
///     Independent auditor requests, packages, and later Type II periods have their own roots.
/// </summary>
public sealed class ExaminationEngagement : Aggregate
{
    const int MaximumSources = 256;
    const int MaximumAmendmentDepth = 8;
    const int MaximumEventBytes = 48 * 1024;
    const int MaximumAuditFirmLength = 200;
    const int MaximumActorDisplayLength = 200;
    const int MaximumRationaleLength = 2000;
    const int MaximumScopeDifferenceLength = 1000;
    static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "security", "availability", "confidentiality", "processing_integrity", "privacy"
    };

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly List<TypeIBaselineView> _baselineHistory = [];
    ExaminationEngagementView? _current;
    TypeIBaselineView? _baseline;

    public long Revision => _current?.Revision ?? 0;

    public ExaminationEngagementView? Current => _current;

    public TypeIBaselineView? Baseline => _baseline;

    public IReadOnlyList<TypeIBaselineView> BaselineHistory =>
        Array.AsReadOnly(_baselineHistory.ToArray());

    public ExaminationEngagement(Uuid tenantId, Uuid programId, Uuid engagementId)
        : base(engagementId, new EventStreamAddress(tenantId.ToString(), "examinations",
            engagementId.ToString()))
    {
        _tenantId = tenantId;
        _programId = programId;
        On<ExaminationEngagementOpened>(Apply);
        On<ExaminationEngagementRevised>(Apply);
        On<TypeIBaselineFrozen>(Apply);
        On<TypeIBaselineAmended>(Apply);
    }

    public CommandFailure? Create(ExaminationEngagementContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset occurredAt)
    {
        if (_current is not null)
            return SameContent(_current, content)
                ? null
                : CommandFailure.StateConflict(
                    "The examination engagement already exists with different terms.");
        if (_tenantId == Uuid.Empty || _programId == Uuid.Empty || Id == Uuid.Empty)
            return CommandFailure.InvalidContent(
                "An examination engagement requires tenant, program, and engagement identities.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (NormalizeContent(content) is not { } normalized)
            return InvalidEngagement();

        var engagement = View(normalized, "draft", 1, actorMemberId, actorDisplay.Trim(), occurredAt);
        var opened = new ExaminationEngagementOpened(_tenantId, engagement, occurredAt);
        if (!Fits(opened))
            return CommandFailure.InvalidContent(
                "The examination engagement exceeds the bounded event payload.");
        RaiseEvent(opened);
        return null;
    }

    public CommandFailure? Revise(long expectedRevision, ExaminationEngagementContent content,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset occurredAt)
    {
        if (_current is null)
            return CommandFailure.MissingRecord("The examination engagement was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("examination engagement", Revision));
        if (_baseline is not null)
            return CommandFailure.StateConflict(
                "A frozen Type I engagement can only change through a baseline amendment.");
        if (ValidateActor(actorMemberId, actorDisplay, occurredAt) is { } actorFailure)
            return actorFailure;
        if (NormalizeContent(content) is not { } normalized)
            return InvalidEngagement();
        if (SameContent(_current, normalized))
            return null;

        var engagement = View(normalized, "draft", Revision + 1, actorMemberId,
            actorDisplay.Trim(), occurredAt);
        var revised = new ExaminationEngagementRevised(_tenantId, engagement, occurredAt);
        if (!Fits(revised))
            return CommandFailure.InvalidContent(
                "The revised examination engagement exceeds the bounded event payload.");
        RaiseEvent(revised);
        return null;
    }

    public CommandFailure? FreezeBaseline(long expectedRevision, Uuid baselineId,
        TypeIBaselinePreview preview, string confirmationRationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset frozenAt)
    {
        confirmationRationale = confirmationRationale?.Trim() ?? string.Empty;
        if (preview is null)
            return CommandFailure.InvalidContent("A complete baseline preview is required.");
        if (_baseline is { } existing)
            return SameFreeze(existing, baselineId, preview, confirmationRationale,
                    actorMemberId, actorDisplay)
                ? null
                : CommandFailure.StateConflict(
                    "The Type I baseline is already frozen; changes require an attributable amendment.");
        if (_current is null)
            return CommandFailure.MissingRecord("The examination engagement was not found.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("examination engagement", Revision));
        if (ValidateActor(actorMemberId, actorDisplay, frozenAt) is { } actorFailure)
            return actorFailure;
        if (baselineId == Uuid.Empty || !BoundedText(confirmationRationale, MaximumRationaleLength))
            return CommandFailure.InvalidContent(
                "Baseline confirmation requires a stable identity and a bounded rationale.");
        if (preview.Blockers is null || preview.Blockers.Count > 1000)
            return InvalidPreview("The baseline preview is invalid.");
        if (preview.Blockers.Count != 0)
            return CommandFailure.StateConflict(
                "The baseline cannot be frozen while source blockers remain.");
        if (!PreviewMatchesEngagement(preview))
            return InvalidPreview("The baseline preview does not match this engagement.");
        if (ValidatePreview(preview, _current.BoundaryId) is { } previewFailure)
            return previewFailure;

        var sources = Freeze(preview.Sources);
        var differences = FreezeText(preview.ScopeDifferences);
        var frozen = new TypeIBaselineView(_tenantId, _programId, Id, baselineId, "frozen", 1,
            sources, differences, preview.CanonicalManifest, preview.ContentSha256, frozenAt,
            actorMemberId, actorDisplay.Trim(), confirmationRationale, null, null, [], [], 0)
        {
            SourceCoverage = FreezeCoverage(preview.SourceCoverage),
        };
        var engagement = _current with
        {
            Status = "baseline_frozen",
            Revision = Revision + 1,
            UpdatedAt = frozenAt,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
        };
        var ev = new TypeIBaselineFrozen(_tenantId, engagement, frozen, frozenAt);
        if (!Fits(ev))
            return CommandFailure.InvalidContent(
                "The complete Type I baseline exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    public CommandFailure? AmendBaseline(long expectedRevision, Uuid baselineId,
        TypeIBaselinePreview preview, IReadOnlyList<Uuid> changedRecordIds,
        IReadOnlyList<Uuid> affectedPackageMaterialIds, string reason, Uuid approverMemberId,
        string approverDisplay, DateTimeOffset amendedAt)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (_current is null || _baseline is null)
            return CommandFailure.MissingRecord("A frozen Type I baseline was not found.");
        if (preview is null || changedRecordIds is null || affectedPackageMaterialIds is null)
            return CommandFailure.InvalidContent(
                "A baseline amendment requires a complete preview and impact list.");
        if (baselineId != Uuid.Empty && _baselineHistory.FirstOrDefault(item =>
                item.TypeIBaselineId == baselineId) is { } prior)
            return SameAmendment(prior, preview, changedRecordIds, affectedPackageMaterialIds,
                    reason, approverMemberId, approverDisplay)
                ? null
                : CommandFailure.StateConflict(
                    "A Type I baseline amendment identity cannot be reused with different content.");
        if (expectedRevision != Revision)
            return CommandFailure.ForVersion(
                VersionedRecordRules.StaleRevision("examination engagement", Revision));
        if (ValidateActor(approverMemberId, approverDisplay, amendedAt) is { } actorFailure)
            return actorFailure;
        if (baselineId == Uuid.Empty || !BoundedText(reason, MaximumRationaleLength))
            return CommandFailure.InvalidContent(
                "A baseline amendment requires a new identity and a bounded reason.");
        if (_baseline.AmendmentDepth >= MaximumAmendmentDepth)
            return CommandFailure.StateConflict(
                "The Type I baseline amendment lineage reached its supported limit.");
        if (preview.Blockers is null || preview.Blockers.Count != 0)
            return CommandFailure.StateConflict(
                "The baseline amendment cannot include unresolved source blockers.");
        if (!PreviewMatchesEngagement(preview))
            return InvalidPreview("The baseline amendment preview does not match this engagement.");
        if (ValidatePreview(preview, _current.BoundaryId) is { } previewFailure)
            return previewFailure;
        if (baselineId == _baseline.TypeIBaselineId ||
            _baselineHistory.Any(item => item.TypeIBaselineId == baselineId))
            return CommandFailure.StateConflict(
                "A baseline amendment must use a new immutable identity.");

        var expectedChanges = ChangedRecords(_baseline.Sources, preview.Sources);
        var suppliedChanges = changedRecordIds.Distinct().OrderBy(static id => id.ToString(),
            StringComparer.Ordinal).ToArray();
        if (expectedChanges.Length == 0 || !expectedChanges.SequenceEqual(suppliedChanges))
            return CommandFailure.InvalidContent(
                "A baseline amendment must identify every changed source record exactly once.");
        if (affectedPackageMaterialIds.Any(static id => id == Uuid.Empty) ||
            affectedPackageMaterialIds.Distinct().Count() != affectedPackageMaterialIds.Count)
            return CommandFailure.InvalidContent(
                "Affected package material identities must be unique and nonempty.");

        var sources = Freeze(preview.Sources);
        var differences = FreezeText(preview.ScopeDifferences);
        var amended = new TypeIBaselineView(_tenantId, _programId, Id, baselineId, "amended",
            _baseline.Revision + 1, sources, differences, preview.CanonicalManifest,
            preview.ContentSha256, amendedAt, approverMemberId, approverDisplay.Trim(),
            _baseline.ConfirmationRationale, _baseline.TypeIBaselineId, reason,
            Array.AsReadOnly(suppliedChanges), FreezeIds(affectedPackageMaterialIds),
            _baseline.AmendmentDepth + 1)
        {
            SourceCoverage = FreezeCoverage(preview.SourceCoverage),
        };
        var engagement = _current with
        {
            Revision = Revision + 1,
            UpdatedAt = amendedAt,
            ActorMemberId = approverMemberId,
            ActorDisplay = approverDisplay.Trim(),
        };
        var ev = new TypeIBaselineAmended(_tenantId, engagement, amended, amendedAt);
        if (!Fits(ev))
            return CommandFailure.InvalidContent(
                "The complete Type I baseline amendment exceeds the bounded event payload.");
        RaiseEvent(ev);
        return null;
    }

    void Apply(ExaminationEngagementOpened ev)
    {
        if (ev.TenantId != _tenantId || ev.Engagement.TenantId != _tenantId ||
            ev.Engagement.ProgramId != _programId || ev.Engagement.ExaminationEngagementId != Id ||
            ev.Engagement.Status != "draft" || ev.Engagement.Revision != 1 ||
            ev.Engagement.UpdatedAt != ev.OccurredAt || _current is not null ||
            ValidateActor(ev.Engagement.ActorMemberId, ev.Engagement.ActorDisplay,
                ev.OccurredAt) is not null ||
            NormalizeContent(Content(ev.Engagement)) is not { } content ||
            !SameContent(ev.Engagement, content))
            throw new InvalidOperationException("The examination engagement opening event is invalid.");
        _current = FreezeView(ev.Engagement);
    }

    void Apply(ExaminationEngagementRevised ev)
    {
        if (_current is null || _baseline is not null || ev.TenantId != _tenantId ||
            ev.Engagement.TenantId != _tenantId || ev.Engagement.ProgramId != _programId ||
            ev.Engagement.ExaminationEngagementId != Id || ev.Engagement.Status != "draft" ||
            ev.Engagement.Revision != Revision + 1 || ev.Engagement.UpdatedAt != ev.OccurredAt ||
            ValidateActor(ev.Engagement.ActorMemberId, ev.Engagement.ActorDisplay,
                ev.OccurredAt) is not null ||
            NormalizeContent(Content(ev.Engagement)) is not { } content ||
            !SameContent(ev.Engagement, content))
            throw new InvalidOperationException("The examination engagement revision is invalid.");
        _current = FreezeView(ev.Engagement);
    }

    void Apply(TypeIBaselineFrozen ev)
    {
        if (_current is null || _baseline is not null || ev.TenantId != _tenantId ||
            ev.Engagement.TenantId != _tenantId || ev.Engagement.ProgramId != _programId ||
            ev.Engagement.ExaminationEngagementId != Id || ev.Engagement.Status != "baseline_frozen" ||
            ev.Engagement.Revision != Revision + 1 || ev.Engagement.UpdatedAt != ev.OccurredAt ||
            ev.Baseline.TenantId != _tenantId || ev.Baseline.ProgramId != _programId ||
            ev.Baseline.ExaminationEngagementId != Id || ev.Baseline.Status != "frozen" ||
            ev.Baseline.Revision != 1 || ev.Baseline.AmendsBaselineId is not null ||
            ev.Baseline.UpdatedAt != ev.OccurredAt || ev.Baseline.ActorMemberId != ev.Engagement.ActorMemberId ||
            !BaselineMatchesManifest(ev.Baseline, ev.Engagement.BoundaryId) ||
            ValidateActor(ev.Baseline.ActorMemberId, ev.Baseline.ActorDisplay,
                ev.OccurredAt) is not null)
            throw new InvalidOperationException("The Type I baseline freeze event is invalid.");
        _current = FreezeView(ev.Engagement);
        _baseline = FreezeBaselineView(ev.Baseline);
        _baselineHistory.Add(_baseline);
    }

    void Apply(TypeIBaselineAmended ev)
    {
        if (_current is null || _baseline is null || ev.TenantId != _tenantId ||
            ev.Engagement.TenantId != _tenantId || ev.Engagement.ProgramId != _programId ||
            ev.Engagement.ExaminationEngagementId != Id || ev.Engagement.Status != "baseline_frozen" ||
            ev.Engagement.Revision != Revision + 1 || ev.Engagement.UpdatedAt != ev.OccurredAt ||
            ev.Baseline.TenantId != _tenantId || ev.Baseline.ProgramId != _programId ||
            ev.Baseline.ExaminationEngagementId != Id || ev.Baseline.Status != "amended" ||
            ev.Baseline.Revision != _baseline.Revision + 1 ||
            ev.Baseline.AmendsBaselineId != _baseline.TypeIBaselineId ||
            ev.Baseline.AmendmentDepth != _baseline.AmendmentDepth + 1 ||
            ev.Baseline.UpdatedAt != ev.OccurredAt ||
            !BaselineMatchesManifest(ev.Baseline, ev.Engagement.BoundaryId) ||
            ValidateActor(ev.Baseline.ActorMemberId, ev.Baseline.ActorDisplay,
                ev.OccurredAt) is not null ||
            !ChangedRecords(_baseline.Sources, ev.Baseline.Sources)
                .SequenceEqual(ev.Baseline.ChangedRecordIds.OrderBy(static id => id.ToString(),
                    StringComparer.Ordinal)))
            throw new InvalidOperationException("The Type I baseline amendment event is invalid.");
        _current = FreezeView(ev.Engagement);
        _baseline = FreezeBaselineView(ev.Baseline);
        _baselineHistory.Add(_baseline);
    }

    bool PreviewMatchesEngagement(TypeIBaselinePreview preview) =>
        preview.TenantId == _tenantId && preview.ProgramId == _programId &&
        preview.ExaminationEngagementId == Id;

    static CommandFailure? ValidatePreview(TypeIBaselinePreview preview, Uuid expectedBoundaryId)
    {
        if (preview.Sources is null || preview.Sources.Count is 0 or > MaximumSources ||
            preview.ScopeDifferences is null || preview.ScopeDifferences.Count > 1000 ||
            !ValidSourceCoverage(preview.SourceCoverage))
            return InvalidPreview("A Type I baseline requires a bounded, complete source manifest.");
        var identities = new HashSet<(string Kind, Uuid RecordId)>();
        foreach (var source in preview.Sources)
        {
            if (source is null || !ValidKind(source.Kind) || source.RecordId == Uuid.Empty ||
                source.VersionId == Uuid.Empty || source.Revision <= 0 ||
                !ValidDigest(source.ContentSha256) ||
                !identities.Add((source.Kind, source.RecordId)))
                return InvalidPreview(
                    "Every baseline source must have one exact, unique version and content digest.");
        }
        if (!HasExactApprovedBoundary(preview.Sources, expectedBoundaryId))
            return InvalidPreview(
                "The Type I baseline must bind the engagement's exact approved boundary.");
        if (preview.ScopeDifferences.Any(difference =>
                !BoundedText(difference?.Trim() ?? string.Empty, MaximumScopeDifferenceLength)))
            return InvalidPreview("Scope differences must be described with bounded text.");
        return null;
    }

    static bool BaselineMatchesManifest(TypeIBaselineView baseline, Uuid expectedBoundaryId)
    {
        try
        {
            if (!HasExactApprovedBoundary(baseline.Sources, expectedBoundaryId))
                return false;
            if (!ValidSourceCoverage(baseline.SourceCoverage))
                return false;
            var preview = TypeIBaselinePreview.Create(baseline.TenantId, baseline.ProgramId,
                baseline.ExaminationEngagementId, baseline.Sources, [], baseline.ScopeDifferences,
                baseline.SourceCoverage);
            return preview.ContentSha256 == baseline.ContentSha256 &&
                   preview.CanonicalManifest == baseline.CanonicalManifest &&
                   ValidDigest(baseline.ContentSha256) && baseline.Sources.Count is > 0 and <= MaximumSources &&
                   baseline.ScopeDifferences.All(difference =>
                       BoundedText(difference, MaximumScopeDifferenceLength));
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            return false;
        }
    }

    static bool HasExactApprovedBoundary(
        IReadOnlyList<TypeIBaselineSourceReference>? sources, Uuid expectedBoundaryId)
    {
        if (sources is null || expectedBoundaryId == Uuid.Empty)
            return false;
        TypeIBaselineSourceReference? boundary = null;
        foreach (var source in sources)
        {
            if (source is null || source.Kind != "approved_boundary")
                continue;
            if (boundary is not null)
                return false;
            boundary = source;
        }
        return boundary?.RecordId == expectedBoundaryId;
    }

    bool SameFreeze(TypeIBaselineView existing, Uuid baselineId, TypeIBaselinePreview preview,
        string rationale, Uuid actorMemberId, string actorDisplay) =>
        existing.TypeIBaselineId == baselineId && PreviewMatchesEngagement(preview) &&
        existing.ContentSha256 == SafeDigest(preview) &&
        existing.ConfirmationRationale == rationale && existing.ActorMemberId == actorMemberId &&
        existing.ActorDisplay == actorDisplay?.Trim();

    static bool SameAmendment(TypeIBaselineView existing, TypeIBaselinePreview preview,
        IReadOnlyList<Uuid> changedRecordIds, IReadOnlyList<Uuid> affectedPackageMaterialIds,
        string reason, Uuid actorMemberId, string actorDisplay) =>
        existing.ContentSha256 == SafeDigest(preview) && existing.AmendmentReason == reason &&
        existing.ActorMemberId == actorMemberId && existing.ActorDisplay == actorDisplay?.Trim() &&
        existing.ChangedRecordIds.SequenceEqual(changedRecordIds.OrderBy(static id => id.ToString(),
            StringComparer.Ordinal)) &&
        existing.AffectedPackageMaterialIds.SequenceEqual(affectedPackageMaterialIds.OrderBy(
            static id => id.ToString(), StringComparer.Ordinal));

    static Uuid[] ChangedRecords(
        IReadOnlyList<TypeIBaselineSourceReference> before,
        IReadOnlyList<TypeIBaselineSourceReference> after)
    {
        var prior = before.ToDictionary(static source => (source.Kind, source.RecordId));
        var next = after.ToDictionary(static source => (source.Kind, source.RecordId));
        return prior.Keys.Union(next.Keys)
            .Where(key => !prior.TryGetValue(key, out var oldSource) ||
                          !next.TryGetValue(key, out var newSource) || oldSource != newSource)
            .Select(static key => key.RecordId).Distinct()
            .OrderBy(static id => id.ToString(), StringComparer.Ordinal).ToArray();
    }

    static CommandFailure? ValidateActor(Uuid memberId, string display, DateTimeOffset at) =>
        memberId == Uuid.Empty || !BoundedText(display?.Trim() ?? string.Empty,
            MaximumActorDisplayLength) || at == default
            ? CommandFailure.InvalidContent(
                "An attributed engagement decision requires a member, display name, and time.")
            : null;

    static ExaminationEngagementContent? NormalizeContent(ExaminationEngagementContent? content)
    {
        var auditFirm = content?.AuditFirm?.Trim() ?? string.Empty;
        if (content is null || content.EngagementType != "type_i" ||
            !BoundedText(auditFirm, MaximumAuditFirmLength) ||
            content.PeriodStart == default || content.PeriodEnd is { } end && end != content.PeriodStart ||
            content.BoundaryId == Uuid.Empty || content.TrustServicesCategories is null ||
            content.TrustServicesCategories.Count is 0 or > 5 ||
            content.ManagementContactMemberIds is null || content.ManagementContactMemberIds.Count > 20)
            return null;
        var categories = content.TrustServicesCategories.Select(static value => value?.Trim() ?? string.Empty)
            .OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        if (categories.Any(category => !Categories.Contains(category)) ||
            categories.Distinct(StringComparer.Ordinal).Count() != categories.Length)
            return null;
        var contacts = content.ManagementContactMemberIds.OrderBy(static id => id.ToString(),
            StringComparer.Ordinal).ToArray();
        if (contacts.Any(static id => id == Uuid.Empty) || contacts.Distinct().Count() != contacts.Length)
            return null;
        return new ExaminationEngagementContent("type_i", auditFirm,
            content.PeriodStart, content.PeriodEnd, Array.AsReadOnly(categories), content.BoundaryId,
            Array.AsReadOnly(contacts));
    }

    static bool SameContent(ExaminationEngagementView current,
        ExaminationEngagementContent? content) => NormalizeContent(content) is { } normalized &&
        current.EngagementType == normalized.EngagementType && current.AuditFirm == normalized.AuditFirm &&
        current.PeriodStart == normalized.PeriodStart && current.PeriodEnd == normalized.PeriodEnd &&
        current.TrustServicesCategories.SequenceEqual(normalized.TrustServicesCategories) &&
        current.BoundaryId == normalized.BoundaryId &&
        current.ManagementContactMemberIds.SequenceEqual(normalized.ManagementContactMemberIds);

    ExaminationEngagementView View(ExaminationEngagementContent content, string status,
        long revision, Uuid actorMemberId, string actorDisplay, DateTimeOffset updatedAt) =>
        new(_tenantId, _programId, Id, content.EngagementType, content.AuditFirm,
            content.PeriodStart, content.PeriodEnd, content.TrustServicesCategories,
            content.BoundaryId, content.ManagementContactMemberIds, status, revision,
            updatedAt, actorMemberId, actorDisplay);

    static ExaminationEngagementContent Content(ExaminationEngagementView view) => new(
        view.EngagementType, view.AuditFirm, view.PeriodStart, view.PeriodEnd,
        view.TrustServicesCategories, view.BoundaryId, view.ManagementContactMemberIds);

    static ExaminationEngagementView FreezeView(ExaminationEngagementView view) => view with
    {
        TrustServicesCategories = Array.AsReadOnly(view.TrustServicesCategories.ToArray()),
        ManagementContactMemberIds = Array.AsReadOnly(view.ManagementContactMemberIds.ToArray()),
    };

    static TypeIBaselineView FreezeBaselineView(TypeIBaselineView baseline) => baseline with
    {
        Sources = Freeze(baseline.Sources),
        SourceCoverage = FreezeCoverage(baseline.SourceCoverage),
        ScopeDifferences = FreezeText(baseline.ScopeDifferences),
        ChangedRecordIds = FreezeIds(baseline.ChangedRecordIds),
        AffectedPackageMaterialIds = FreezeIds(baseline.AffectedPackageMaterialIds),
    };

    static ReadOnlyCollection<TypeIBaselineSourceReference> Freeze(
        IReadOnlyList<TypeIBaselineSourceReference> sources) => Array.AsReadOnly(sources
        .OrderBy(static source => source.Kind, StringComparer.Ordinal)
        .ThenBy(static source => source.RecordId.ToString(), StringComparer.Ordinal).ToArray());

    static ReadOnlyCollection<TypeIBaselineSourceCoverage> FreezeCoverage(
        IReadOnlyList<TypeIBaselineSourceCoverage> coverage) => Array.AsReadOnly(coverage
        .OrderBy(static family => family.Family, StringComparer.Ordinal).ToArray());

    static ReadOnlyCollection<string> FreezeText(IReadOnlyList<string> values) => Array.AsReadOnly(values
        .Select(static value => value.Trim().Normalize(NormalizationForm.FormC))
        .OrderBy(static value => value, StringComparer.Ordinal).ToArray());

    static ReadOnlyCollection<Uuid> FreezeIds(IReadOnlyList<Uuid> values) => Array.AsReadOnly(values
        .OrderBy(static value => value.ToString(), StringComparer.Ordinal).ToArray());

    static bool ValidKind(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
        value[0] is >= 'a' and <= 'z' && value.All(static character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static bool ValidSourceCoverage(IReadOnlyList<TypeIBaselineSourceCoverage>? coverage)
    {
        if (coverage is null || coverage.Count != TypeIBaselineSourceFamilies.Required.Count)
            return false;
        var families = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in coverage)
        {
            if (item is null || item.RecordCount is < 0 or > MaximumSources ||
                !ValidDigest(item.ContentSha256) ||
                !families.Add(item.Family) ||
                !TypeIBaselineSourceFamilies.Required.Contains(item.Family,
                    StringComparer.Ordinal))
                return false;
        }
        return families.SetEquals(TypeIBaselineSourceFamilies.Required);
    }

    static bool BoundedText(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    static string? SafeDigest(TypeIBaselinePreview preview)
    {
        try
        {
            return preview.ContentSha256;
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            return null;
        }
    }

    static bool Fits<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value,
        value!.GetType(), ComplianceCoreJsonContext.Default).Length <= MaximumEventBytes;

    static CommandFailure InvalidEngagement() => CommandFailure.InvalidContent(
        "A Type I engagement requires an audit firm, one point-in-time date, a boundary, " +
        "supported Trust Services categories, and valid management contacts.");

    static CommandFailure InvalidPreview(string message) => CommandFailure.InvalidContent(message);
}
