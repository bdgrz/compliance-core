using Cntryl.Portia;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.AuditOutcome;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Readers;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Rules;

/// <summary>Validates a proposed plan against the current frozen Type I source chain.</summary>
static class TypeIIPlanSourceValidator
{
    public static async ValueTask<Result<TypeIIPlanSourceValidation>> ValidateAsync(
        IAggregateReader reader, ExaminationEngagementReader examinations, ControlReader controlSources, FindingReader findings, ControlOperationsReader operating, SystemDescriptionReader descriptions, Uuid tenantId,
        Uuid programId, Uuid examinationId, TypeIIPlanContent content, DateTimeOffset proposedAt, CancellationToken ct)
    {
        if (content is null || content.ExaminationId != examinationId ||
            content.CarryForwardInputs is null || content.Controls is null ||
            content.CarriedForwardGaps is null || content.AcknowledgedGaps is null ||
            content.RequiredChanges is null || content.TrustServiceCategories is null ||
            content.CarryForwardInputs.Any(item => item is null) ||
            content.Controls.Any(item => item is null) ||
            content.CarriedForwardGaps.Any(item => item is null) ||
            content.AcknowledgedGaps.Any(item => item is null) ||
            content.RequiredChanges.Any(item => item is null) ||
            content.Controls.Select(item => (item.ControlId, item.ControlVersionId)).Distinct().Count() !=
                content.Controls.Count ||
            content.CarryForwardInputs.Select(item => (item.SourceType, item.RecordId, item.VersionId))
                .Distinct().Count() != content.CarryForwardInputs.Count)
            return Failure(RequestErrorKind.Validation,
                "The Type II plan content must identify the examination and include complete source and planning collections.");

        var engagement = await examinations.ReadAsync(tenantId, programId, examinationId, ct).ConfigureAwait(false);
        if (engagement.View is not { } engagementView || engagement.Baseline is not { } baseline)
            return Failure(RequestErrorKind.NotFound,
                "The Type I examination and its frozen baseline were not found.");
        if (engagementView.TenantId != tenantId || engagementView.ProgramId != programId ||
            engagementView.ExaminationEngagementId != examinationId ||
            baseline.TenantId != tenantId || baseline.ProgramId != programId ||
            baseline.ExaminationEngagementId != examinationId ||
            baseline.Status is not ("frozen" or "amended") || !ValidBaseline(baseline) ||
            content.TypeIBaselineId != baseline.TypeIBaselineId ||
            content.SourceSnapshotId != baseline.TypeIBaselineId ||
            !HashEquals(content.SourceSnapshotSha256, baseline.ContentSha256))
            return Failure(RequestErrorKind.Conflict,
                "The Type II plan must bind the current exact Type I baseline manifest.");
        if (!StringComparer.Ordinal.Equals(content.AuditFirm, engagementView.AuditFirm) ||
            !content.TrustServiceCategories.Order(StringComparer.Ordinal)
                .SequenceEqual(engagementView.TrustServicesCategories.Order(StringComparer.Ordinal)))
            return Failure(RequestErrorKind.Conflict,
                "The Type II plan must retain the audit firm and trust-service categories from the Type I engagement.");

        if (!ExactBaselineSources(content.CarryForwardInputs, baseline.Sources) ||
            content.CarriedForwardGaps.Any(gap => gap.SourceKind != "finding" &&
                !baseline.Sources.Any(source =>
                source.Kind == gap.SourceKind && source.RecordId == gap.SourceRecordId &&
                source.VersionId == gap.SourceVersionId && source.Revision == gap.SourceVersion &&
                HashEquals(source.ContentSha256, gap.SourceSha256))) ||
            content.RequiredChanges.Any(change => !baseline.Sources.Any(source =>
                source.Kind == "change" && source.RecordId == change.ChangeId)))
            return Failure(RequestErrorKind.Conflict,
                "Every carried-forward source, gap, and required change must belong to the exact frozen baseline.");
        var carried = await ValidateCarriedForwardFindingsAsync(reader, findings, tenantId, programId,
            content, proposedAt, ct).ConfigureAwait(false);
        if (!carried.IsSuccess)
            return Result<TypeIIPlanSourceValidation>.Failure(carried.Error!);
        if (!await ControlPlansMatchAsync(reader, controlSources, operating, tenantId, programId, content, baseline,
                proposedAt, ct).ConfigureAwait(false))
            return Failure(RequestErrorKind.Conflict,
                "Every planned control must match its exact approved control version, operating owner, cadence, and effective period.");

        var outcome = await reader.HydrateAsync(new AuditOutcomeAggregate(tenantId, programId,
            content.TypeIOutcomeId), ct).ConfigureAwait(false);
        if (outcome.Current is not { Status: "recorded" } outcomeView ||
            outcomeView.TenantId != tenantId || outcomeView.ProgramId != programId ||
            outcomeView.AuditOutcomeId != content.TypeIOutcomeId ||
            outcomeView.Content.ExaminationId != examinationId ||
            outcomeView.Content.ScopeKind != "type_i" ||
            outcomeView.Content.SourceKind != "auditor" ||
            outcomeView.Content.SourceSnapshotId != baseline.TypeIBaselineId ||
            !HashEquals(outcomeView.Content.SourceSnapshotSha256, baseline.ContentSha256))
            return Failure(RequestErrorKind.Conflict,
                "The Type II plan must reference an auditor outcome for this exact Type I baseline.");

        var descriptionSources = baseline.Sources.Where(source =>
            source.Kind == "system_description" &&
            source.VersionId == content.SystemDescriptionVersionId).Take(2).ToArray();
        if (descriptionSources.Length != 1)
            return Failure(RequestErrorKind.Conflict,
                "The approved system-description version must be included in the Type I baseline.");
        var descriptionSource = descriptionSources[0];
        SystemDescriptionReadSnapshot description;
        SystemDescriptionVersionSource? selectedDescription;
        try
        {
            description = await descriptions.ReadAsync(tenantId, programId, descriptionSource.RecordId, ct).ConfigureAwait(false);
            selectedDescription = await descriptions.VersionSourceAsync(tenantId, programId, descriptionSource.RecordId, content.SystemDescriptionVersionId, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException failure)
        {
            return Result<TypeIIPlanSourceValidation>.Failure(SystemDescriptionReadFailure.Of(failure));
        }
        var descriptionView = description.Read();
        var descriptionVersion = selectedDescription?.Version;
        var approval = selectedDescription?.Approval;
        var binding = descriptionView.Bindings.SingleOrDefault(item =>
            item.VersionId == content.SystemDescriptionVersionId && item.Purpose == "type_i_baseline");
        if (selectedDescription?.ApprovalSource is null || descriptionVersion is not { Status: "approved" } ||
            descriptionVersion.TenantId != tenantId || descriptionVersion.ProgramId != programId ||
            descriptionVersion.SystemDescriptionId != descriptionSource.RecordId ||
            descriptionVersion.Scope.Stage != "type_i" ||
            descriptionVersion.Scope.ExaminationId != examinationId ||
            descriptionVersion.Scope.TypeIBaselineId != baseline.TypeIBaselineId ||
            descriptionVersion.Version != descriptionSource.Revision ||
            !HashEquals(descriptionVersion.ContentSha256, descriptionSource.ContentSha256) ||
            approval is null || approval.Version != descriptionVersion.Version || binding is null ||
            binding.Version != descriptionVersion.Version || binding.ExaminationId != examinationId ||
            binding.SnapshotId != baseline.TypeIBaselineId)
            return Failure(RequestErrorKind.Conflict,
                "The Type II plan must use the approved system description bound to the exact Type I baseline.");

        if (outcomeView.RecordedAt < baseline.UpdatedAt ||
            proposedAt == default || proposedAt < baseline.UpdatedAt ||
            proposedAt < outcomeView.RecordedAt || proposedAt < descriptionVersion.UpdatedAt ||
            proposedAt < approval.ApprovedAt || proposedAt < binding.BoundAt)
            return Failure(RequestErrorKind.Conflict,
                "The Type II plan cannot be proposed before its frozen source records exist.");

        return Result<TypeIIPlanSourceValidation>.Success(new TypeIIPlanSourceValidation(
            baseline, outcomeView, descriptionVersion, approval, binding));
    }

    static bool ExactBaselineSources(IReadOnlyList<ExaminationContentReference> references,
        IReadOnlyList<TypeIBaselineSourceReference> sources) => references.Count == sources.Count &&
        references.All(reference => sources.Any(source => source.Kind == reference.SourceType &&
            source.RecordId == reference.RecordId && source.VersionId == reference.VersionId &&
            source.Revision == reference.Version &&
            HashEquals(source.ContentSha256, reference.ContentSha256)));

    static async ValueTask<Result> ValidateCarriedForwardFindingsAsync(IAggregateReader reader, FindingReader findings,
        Uuid tenantId, Uuid programId, TypeIIPlanContent content, DateTimeOffset asOf,
        CancellationToken ct)
    {
        var source = await findings.ReadAsync(tenantId, programId, asOf, ct)
            .ConfigureAwait(false);
        if (!source.IsSuccess)
            return Result.Failure(source.Error);
        var current = source.Value.Findings;
        var required = current.Where(finding => finding.Status is FindingStates.Open or
            FindingStates.Remediated).ToArray();
        var planned = content.CarriedForwardGaps.Where(gap => gap.SourceKind == "finding")
            .ToArray();
        if (required.Length != planned.Length ||
            planned.Select(gap => gap.SourceRecordId).Distinct().Count() != planned.Length)
            return Conflict("Every current open or remediated finding must be explicitly carried into the Type II plan.");

        foreach (var finding in required)
        {
            var gap = planned.SingleOrDefault(item => item.SourceRecordId == finding.FindingId);
            if (gap is null)
                return Conflict("A current finding is missing from the Type II opening plan.");
            var identity = FindingContentIdentityFactory.Create(finding,
                source.Value.ReadClosures(finding.FindingId));
            var accepted = finding.ReadinessStatus == FindingStates.Accepted;
            if (gap.SourceVersionId != identity.VersionId || gap.SourceVersion != identity.Revision ||
                !HashEquals(gap.SourceSha256, identity.ContentSha256) ||
                gap.OwnerMemberId != finding.OwnerMemberId ||
                finding.Status == FindingStates.Open && gap.Disposition == "remediated" ||
                finding.Status == FindingStates.Remediated && gap.Disposition != "remediated" ||
                accepted && gap.Disposition != "accepted" || !accepted && gap.Disposition == "accepted")
                return Conflict("A carried-forward finding must retain its exact current revision, owner, and disposition.");

            var owner = await reader.HydrateAsync(Member.ForVerification(tenantId,
                finding.OwnerMemberId), ct).ConfigureAwait(false);
            if (!owner.IsRegistered || owner.IsSuspended || owner.IsDeprovisioned ||
                owner.Affiliation != "client_personnel" || owner.UserId == Uuid.Empty)
                return Conflict("Every carried-forward finding must retain a current client personnel owner.");
        }

        return Result.Success;
    }

    static async ValueTask<bool> ControlPlansMatchAsync(IAggregateReader reader, ControlReader controlSources, ControlOperationsReader operating,
        Uuid tenantId,
        Uuid programId, TypeIIPlanContent content, TypeIBaselineView baseline,
        DateTimeOffset proposedAt, CancellationToken ct)
    {
        var baselineControls = baseline.Sources.Where(source => source.Kind == "approved_control")
            .ToArray();
        if (content.Controls.Count != baselineControls.Length)
            return false;

        foreach (var source in baselineControls)
        {
            var control = await controlSources.ReadAsync(tenantId, programId, source.RecordId, ct)
                .ConfigureAwait(false);
            if (!control.IsVisible || control.TenantId != tenantId || control.ProgramId != programId)
                return false;
            var version = control.ReadVersions().SingleOrDefault(item =>
                item.VersionId == source.VersionId);
            if (version is not { Status: "approved" or "superseded" } ||
                version.TenantId != tenantId || version.ProgramId != programId ||
                version.ControlId != source.RecordId || version.Revision != source.Revision ||
                version.ApprovedAt > baseline.UpdatedAt ||
                version.EffectiveFrom > content.PeriodStart ||
                version.EffectiveUntil is { } versionUntil && versionUntil <= content.PeriodStart)
                return false;

            var plan = (await operating.ReadLineAsync(tenantId, programId, source.RecordId, Uuid.Empty, ct)
                .ConfigureAwait(false)).Line.PlanOn(content.PeriodStart);
            if (plan is not { Status: "approved" or "superseded", Owner: not null } ||
                plan.Cadence is null || ControlCadenceSchedule.Validate(plan.Cadence) is not null ||
                plan.ControlVersionId != source.VersionId || plan.ApprovedAt is not { } approvedAt ||
                approvedAt > proposedAt || plan.Owner.Kind != "member" ||
                plan.Owner.Id == Uuid.Empty)
                return false;
            var owner = await reader.HydrateAsync(Member.ForVerification(tenantId, plan.Owner.Id), ct)
                .ConfigureAwait(false);
            if (!owner.IsRegistered || owner.IsSuspended || owner.IsDeprovisioned ||
                owner.Affiliation != "client_personnel" || owner.UserId == Uuid.Empty)
                return false;

            var entry = content.Controls.SingleOrDefault(item =>
                item.ControlId == source.RecordId && item.ControlVersionId == source.VersionId);
            if (entry is null || string.IsNullOrWhiteSpace(entry.ControlTitle) ||
                string.IsNullOrWhiteSpace(entry.Cadence) ||
                entry.ControlTitle.Trim() != version.Content.Title ||
                entry.ExpectedEvidenceDescriptions is null ||
                !entry.ExpectedEvidenceDescriptions.SequenceEqual(
                    version.Content.ExpectedEvidenceDescriptions, StringComparer.Ordinal) ||
                entry.OwnerMemberId != plan.Owner.Id ||
                !StringComparer.OrdinalIgnoreCase.Equals(entry.Cadence.Trim(),
                    CadenceName(plan.Cadence)) ||
                entry.DueWithinDays != plan.Cadence.DueWithinDays ||
                entry.ExpectedOccurrences != ExpectedOccurrences(plan.Cadence, content.PeriodStart,
                    content.PeriodEnd, plan.EffectiveUntil, version.EffectiveUntil) ||
                entry.EffectiveFrom != content.PeriodStart ||
                entry.EffectiveThrough != EffectiveThrough(content.PeriodEnd,
                    plan.EffectiveUntil, version.EffectiveUntil))
                return false;
        }
        return true;
    }

    static string CadenceName(ControlCadence cadence) => cadence.Kind == ControlCadenceSchedule.Recurring
        ? cadence.Frequency ?? string.Empty
        : cadence.Kind;

    static int ExpectedOccurrences(ControlCadence cadence, DateOnly periodStart, DateOnly periodEnd,
        DateOnly? planEffectiveUntil, DateOnly? controlEffectiveUntil)
    {
        if (cadence.Kind != ControlCadenceSchedule.Recurring)
            return 1;
        if (cadence.FirstPeriodStart != periodStart)
            return -1;
        var expirations = new[] { planEffectiveUntil, controlEffectiveUntil }
            .Where(value => value is not null).Select(value => value!.Value).ToArray();
        DateOnly? until = expirations.Length == 0 ? null : expirations.Min();
        return ControlCadenceSchedule.Periods(cadence, periodStart, until, periodEnd).Count();
    }

    static DateOnly? EffectiveThrough(DateOnly periodEnd, DateOnly? planEffectiveUntil,
        DateOnly? controlEffectiveUntil)
    {
        var expirations = new[] { planEffectiveUntil, controlEffectiveUntil }
            .Where(value => value is not null).Select(value => value!.Value).ToArray();
        if (expirations.Length == 0)
            return null;
        var until = expirations.Min();
        if (until > periodEnd)
            return null;
        return until.AddDays(-1);
    }

    static bool ValidBaseline(TypeIBaselineView baseline)
    {
        var preview = TypeIBaselinePreview.Create(baseline.TenantId, baseline.ProgramId,
            baseline.ExaminationEngagementId, baseline.Sources, [], baseline.ScopeDifferences,
            baseline.SourceCoverage);
        return HashEquals(preview.ContentSha256, baseline.ContentSha256) &&
            preview.CanonicalManifest == baseline.CanonicalManifest;
    }

    static bool HashEquals(string? left, string? right) => left is not null && right is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(left, right);

    static Result<TypeIIPlanSourceValidation> Failure(RequestErrorKind kind, string message) =>
        Result<TypeIIPlanSourceValidation>.Failure(new RequestError(kind, message));

    static Result Conflict(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message));
}

sealed record TypeIIPlanSourceValidation(TypeIBaselineView Baseline,
    AuditOutcomeView Outcome, SystemDescriptionVersionView SystemDescription,
    SystemDescriptionApprovalView Approval, SystemDescriptionBindingView Binding);
