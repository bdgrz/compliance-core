using Bdgrz.Compliance.Hosting.Persistence;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Events;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPopulations.Rules;

/// <summary>
/// Reconstructs complete transient audit-population reconciliation and history derived from
/// an approved Type II close snapshot. Operational source records remain in their own streams.
/// </summary>
public sealed class AuditPopulationRules
{
    const int MaximumTextLength = 4000;
    const int MaximumFields = 128;
    const int MaximumFieldValueLength = 2000;
    const int MaximumReferences = 256;
    static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "included", "excluded", "duplicate", "missing", "rejected", "unresolved",
        "canceled", "not_applicable", "exception_backed"
    };
    static readonly HashSet<string> ExportFormats = new(StringComparer.Ordinal)
    {
        "csv", "json"
    };

    readonly Dictionary<Type, Action<DomainEvent>> _reducers = [];
    readonly List<DomainEvent> _pendingChanges = [];
    public Uuid Id { get; }
    public EventStreamAddress Stream { get; }
    public Uuid TenantId => _tenantId;
    public Uuid ProgramId => _programId;
    public IReadOnlyList<DomainEvent> PendingChanges => _pendingChanges.AsReadOnly();
    public void ClearPendingChanges() => _pendingChanges.Clear();
    void On<T>(Action<T> reducer) where T : DomainEvent =>
        _reducers.Add(typeof(T), fact => reducer((T)fact));
    public void Apply(DomainEvent fact)
    {
        if (!_reducers.TryGetValue(fact.GetType(), out var reducer))
            throw new InvalidOperationException("The population read mutation has another exact kind.");
        reducer(fact);
    }
    void RaiseEvent(DomainEvent fact)
    {
        Apply(fact);
        _pendingChanges.Add(fact);
    }

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    AuditPopulationView? _current;

    public long Revision => _current?.Revision ?? 0;
    public AuditPopulationView? Current => _current;

    public AuditPopulationRules(Uuid tenantId, Uuid programId, Uuid populationId)
    {
        Id = populationId;
        Stream = new EventStreamAddress(tenantId.ToString(), "audit-populations", populationId.ToString());
        _tenantId = tenantId;
        _programId = programId;
        On<AuditPopulationDefined>(Apply);
        On<AuditPopulationDefinitionRevised>(Apply);
        On<AuditPopulationGenerated>(Apply);
        On<AuditPopulationFactRecorded>(Apply);
        On<AuditPopulationRowClassified>(Apply);
        On<AuditPopulationReconciled>(Apply);
        On<AuditPopulationAmended>(Apply);
        On<AuditPopulationFrozen>(Apply);
        On<AuditPopulationExportGenerated>(Apply);
    }

    public CommandFailure? Define(Uuid definitionId, AuditPopulationDefinitionContent content,
        TypeIIPeriodView closedPeriod, Uuid actorMemberId, string actorDisplay, DateTimeOffset definedAt)
    {
        if (_current is { } existing)
            return SameDefinition(existing, definitionId, content, closedPeriod, actorMemberId,
                actorDisplay, definedAt) ? null : CommandFailure.StateConflict(
                "The audit population already exists with a different definition or close snapshot.");
        if (!ValidIdentity() || definitionId == Uuid.Empty ||
            !ValidActor(actorMemberId, actorDisplay, definedAt) ||
            NormalizeDefinition(content) is not { } normalized ||
            !ValidClosedPeriod(closedPeriod) ||
            closedPeriod.TenantId != _tenantId || closedPeriod.ProgramId != _programId ||
            closedPeriod.PeriodId == Uuid.Empty || closedPeriod.ExaminationId == Uuid.Empty ||
            normalized.PeriodStart < closedPeriod.PeriodStart || normalized.PeriodEnd > closedPeriod.PeriodEnd)
            return Invalid("A population definition requires valid scope, attribution, and the exact frozen Type II close snapshot.");

        var closeSnapshot = closedPeriod.CloseSnapshots[^1];
        var definition = NewDefinition(normalized, 1, actorMemberId, actorDisplay.Trim(), definedAt, "");
        var state = new AuditPopulationView(_tenantId, _programId, Id, definitionId,
            closedPeriod.PeriodId, closedPeriod.ExaminationId, "defined", 1,
            normalized.PeriodStart, normalized.PeriodEnd, 0, null, new[] { definition }, closeSnapshot,
            Array.Empty<AuditPopulationSourceSnapshotView>(), Array.Empty<AuditPopulationRowView>(), null,
            Array.Empty<AuditPopulationVersionView>(), Array.Empty<AuditPopulationAmendmentView>(),
            Array.Empty<AuditPopulationExportView>(), false,
            actorMemberId, actorDisplay.Trim(), definedAt);
        return Emit(state, actorMemberId, definedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationDefined(tenant, subject, revision, actor, snapshot, at),
            "define", creating: true);
    }

    public CommandFailure? ReviseDefinition(long expectedRevision,
        AuditPopulationDefinitionContent content, string changeReason,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset revisedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        changeReason = Clean(changeReason);
        if (current.DefinitionVersions.Count > 0 && current.DefinitionVersions[^1] is { } prior &&
            NormalizeDefinition(content) is { } retry && Digest(retry) == prior.ContentSha256 &&
            prior.ChangeReason == changeReason && prior.RecordedByMemberId == actorMemberId &&
            prior.RecordedByDisplay == Clean(actorDisplay) && prior.RecordedAt == revisedAt)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "defined" || current.Rows.Count > 0 ||
            !ValidActor(actorMemberId, actorDisplay, revisedAt) || revisedAt < current.UpdatedAt ||
            !Bounded(changeReason, 1, MaximumTextLength) ||
            NormalizeDefinition(content) is not { } normalized ||
            normalized.PeriodStart < current.PeriodStart || normalized.PeriodEnd > current.PeriodEnd)
            return Invalid("A population definition can only be revised before source facts are recorded.");
        var definition = NewDefinition(normalized, current.DefinitionVersions.Count + 1,
            actorMemberId, actorDisplay.Trim(), revisedAt, changeReason);
        var state = current with
        {
            Status = "defined",
            Revision = Revision + 1,
            DefinitionVersions = Append(current.DefinitionVersions, definition),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = revisedAt
        };
        return Emit(state, actorMemberId, revisedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationDefinitionRevised(tenant, subject, revision, actor, snapshot, at),
            "definition_revision");
    }

    /// <summary>Marks collection intent without inventing facts or requiring a connector.</summary>
    public CommandFailure? Generate(long expectedRevision, string generationKey,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset generatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        generationKey = Clean(generationKey);
        if (current.GenerationKey == generationKey && Bounded(generationKey, 1, 200) &&
            current.ActorMemberId == actorMemberId && current.ActorDisplay == Clean(actorDisplay) &&
            current.UpdatedAt == generatedAt)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "defined" || !Bounded(generationKey, 1, 200) ||
            !ValidActor(actorMemberId, actorDisplay, generatedAt) || generatedAt < current.UpdatedAt)
            return Invalid("Population generation requires a defined population and attributed generation key.");
        var state = current with
        {
            Status = "collecting",
            GenerationKey = generationKey,
            Revision = Revision + 1,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = generatedAt
        };
        return Emit(state, actorMemberId, generatedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationGenerated(tenant, subject, revision, actor, snapshot, at),
            "generate");
    }

    public CommandFailure? RecordFacts(long expectedRevision,
        AuditPopulationSourceSnapshotContent sourceSnapshot,
        IReadOnlyList<AuditPopulationRowContent> rows, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset recordedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is not ("defined" or "collecting" or "amending") ||
            !ValidActor(actorMemberId, actorDisplay, recordedAt) || recordedAt < current.UpdatedAt ||
            NormalizeSource(sourceSnapshot, actorMemberId, actorDisplay, recordedAt) is not { } normalizedSource ||
            rows is null || rows.Count == 0 ||
            rows.Any(row => NormalizeRow(row, normalizedSource, current.CurrentDefinition!.Content,
                _tenantId, _programId, Id,
                actorMemberId, actorDisplay, recordedAt) is null) ||
            rows.Select(row => row.RowId).Distinct().Count() != rows.Count ||
            rows.Any(row => current.Rows.Any(existing => existing.RowId == row.RowId)) ||
            current.SourceSnapshots.Any(existing => existing.SnapshotId == normalizedSource.SnapshotId))
            return Invalid("Population facts require unique stable rows, authorized fields, exact source provenance, and attributed collection.");
        var normalizedRows = rows.Select(row => NormalizeRow(row, normalizedSource,
            current.CurrentDefinition!.Content, _tenantId, _programId, Id,
            actorMemberId, actorDisplay.Trim(), recordedAt)!).ToArray();
        var state = current with
        {
            Status = current.Status == "amending" ? "amending" : "collecting",
            Revision = Revision + 1,
            SourceSnapshots = Append(current.SourceSnapshots, normalizedSource),
            Rows = Freeze(current.Rows.Concat(normalizedRows).ToArray()),
            Reconciliation = null,
            SampleEligible = false,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = recordedAt
        };
        return Emit(state, actorMemberId, recordedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationFactRecorded(tenant, subject, revision, actor, snapshot, at),
            "facts");
    }

    public CommandFailure? ClassifyRow(long expectedRevision, Uuid rowId,
        string classification, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset classifiedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        classification = Clean(classification).ToLowerInvariant();
        rationale = Clean(rationale);
        var index = current.Rows.ToList().FindIndex(row => row.RowId == rowId);
        if (current.Status is not ("defined" or "collecting" or "amending") || index < 0 ||
            !Classifications.Contains(classification) || !Bounded(rationale, 1, MaximumTextLength) ||
            !ValidActor(actorMemberId, actorDisplay, classifiedAt) || classifiedAt < current.UpdatedAt)
            return Invalid("A row classification requires a current row, supported category, rationale, and attribution.");
        var row = current.Rows[index] with
        {
            Classification = classification,
            ClassificationRationale = rationale,
            RecordedByMemberId = actorMemberId,
            RecordedByDisplay = actorDisplay.Trim(),
            RecordedAt = classifiedAt
        };
        var rows = current.Rows.ToArray();
        rows[index] = row;
        var state = current with
        {
            Revision = Revision + 1,
            Rows = Freeze(rows),
            Reconciliation = null,
            SampleEligible = false,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = classifiedAt
        };
        return Emit(state, actorMemberId, classifiedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationRowClassified(tenant, subject, revision, actor, snapshot, at),
            "classification");
    }

    public CommandFailure? Reconcile(long expectedRevision,
        AuditPopulationReconciliationContent content, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset reconciledAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status is not ("defined" or "collecting" or "amending") ||
            !ValidActor(actorMemberId, actorDisplay, reconciledAt) || reconciledAt < current.UpdatedAt ||
            NormalizeReconciliation(content, current, actorMemberId, actorDisplay, reconciledAt) is not { } reconciliation)
            return Invalid("Population reconciliation must exactly match every row classification and source boundary.");
        var state = current with
        {
            Status = "reconciled",
            Revision = Revision + 1,
            Reconciliation = reconciliation,
            SampleEligible = false,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = reconciledAt
        };
        return Emit(state, actorMemberId, reconciledAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationReconciled(tenant, subject, revision, actor, snapshot, at),
            "reconcile");
    }

    /// <summary>Opens an attributable successor draft; prior frozen rows and hashes stay immutable.</summary>
    public CommandFailure? Amend(long expectedRevision, string reason,
        AuditPopulationSourceSnapshotContent sourceSnapshot,
        IReadOnlyList<AuditPopulationRowContent> changedRows, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset amendedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        reason = Clean(reason);
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "frozen" || current.Current is not { } frozen ||
            !Bounded(reason, 1, MaximumTextLength) ||
            !ValidActor(actorMemberId, actorDisplay, amendedAt) || amendedAt < current.UpdatedAt ||
            NormalizeSource(sourceSnapshot, actorMemberId, actorDisplay, amendedAt) is not { } normalizedSource ||
            changedRows is null || changedRows.Count == 0 ||
            NormalizeAmendedRows(changedRows, current, normalizedSource, actorMemberId,
                actorDisplay, amendedAt) is not { } normalizedRows)
            return Invalid("A population amendment requires a frozen predecessor, changed rows, new source snapshot, and attribution.");

        var changedIds = normalizedRows.Select(row => row.RowId).OrderBy(IdKey, StringComparer.Ordinal).ToArray();
        var rows = current.Rows.ToDictionary(row => row.RowId);
        foreach (var row in normalizedRows)
            rows[row.RowId] = row;
        var amendment = new AuditPopulationAmendmentView(frozen.Version, frozen.Version + 1,
            reason, Freeze(changedIds), frozen.ContentSha256, actorMemberId, actorDisplay.Trim(), amendedAt);
        var state = current with
        {
            Status = "amending",
            Revision = Revision + 1,
            SourceSnapshots = Append(current.SourceSnapshots, normalizedSource),
            Rows = Freeze(rows.Values.OrderBy(row => IdKey(row.RowId), StringComparer.Ordinal).ToArray()),
            Reconciliation = null,
            Amendments = Append(current.Amendments, amendment),
            SampleEligible = false,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = amendedAt
        };
        return Emit(state, actorMemberId, amendedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationAmended(tenant, subject, revision, actor, snapshot, at),
            "amend");
    }

    public CommandFailure? Freeze(long expectedRevision, string rationale,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset frozenAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        rationale = Clean(rationale);
        if (current.Status == "frozen" && current.Current is { } alreadyFrozen &&
            current.ActorMemberId == actorMemberId && current.ActorDisplay == Clean(actorDisplay) &&
            current.UpdatedAt == frozenAt && alreadyFrozen.FreezeRationale == rationale)
            return null;
        if (expectedRevision != Revision)
            return StaleRevision();
        if (current.Status != "reconciled" || current.Reconciliation is not { } reconciliation ||
            !Bounded(rationale, 1, MaximumTextLength) ||
            !ValidActor(actorMemberId, actorDisplay, frozenAt) || frozenAt < current.UpdatedAt)
            return CommandFailure.StateConflict("A current exact-row reconciliation is required before freezing.");
        var eligible = reconciliation.Completeness == "complete";
        var contentHash = PopulationDigest(current, reconciliation);
        var version = new AuditPopulationVersionView(current.CurrentVersionNumber + 1,
            current.DefinitionVersions[^1].Version, current.SourceSnapshots, current.Rows,
            reconciliation with { ContentSha256 = reconciliation.ContentSha256 },
            current.CloseSnapshot.CloseSnapshotId, current.CloseSnapshot.ContentSha256,
            contentHash, eligible, actorMemberId, actorDisplay.Trim(), frozenAt, rationale);
        var state = current with
        {
            Status = "frozen",
            Revision = Revision + 1,
            CurrentVersionNumber = version.Version,
            Versions = Append(current.Versions, version),
            SampleEligible = eligible,
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = frozenAt
        };
        return Emit(state, actorMemberId, frozenAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationFrozen(tenant, subject, revision, actor, snapshot, at),
            "freeze");
    }

    public CommandFailure? RecordExport(long expectedRevision, Uuid exportId,
        int populationVersion, string format, string fileName, Uuid artifactId,
        string outputContentSha256, long outputContentLength,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset generatedAt)
    {
        if (_current is not { } current)
            return CommandFailure.MissingRecord("The audit population was not found.");
        format = Clean(format).ToLowerInvariant();
        fileName = Clean(fileName);
        if (current.Exports.FirstOrDefault(item => item.ExportId == exportId) is { } existing)
            return existing.PopulationVersion == populationVersion && existing.Format == format &&
                existing.FileName == fileName && existing.ArtifactId == artifactId &&
                existing.OutputContentSha256 == outputContentSha256 &&
                existing.OutputContentLength == outputContentLength ? null :
                CommandFailure.StateConflict("An export identity cannot be reused for different population content.");
        if (expectedRevision != Revision)
            return StaleRevision();
        var version = current.Versions.FirstOrDefault(item => item.Version == populationVersion);
        if (current.Status is not ("frozen" or "amending" or "reconciled") || version is null ||
            !ExportFormats.Contains(format) ||
            exportId == Uuid.Empty || artifactId == Uuid.Empty || !IsHash(outputContentSha256) ||
            outputContentLength <= 0 || !Bounded(fileName, 1, 255) ||
            fileName.Contains('/') || fileName.Contains('\\') ||
            !ValidActor(actorMemberId, actorDisplay, generatedAt) || generatedAt < current.UpdatedAt)
            return Invalid("Population export requires an exact frozen version, supported format, stable identity, and attribution.");
        var export = new AuditPopulationExportView(exportId, populationVersion, format, fileName,
            version.ContentSha256, version.CloseSnapshotId, generatedAt, actorMemberId, actorDisplay.Trim(),
            artifactId, outputContentSha256, outputContentLength);
        var state = current with
        {
            Revision = Revision + 1,
            Exports = Append(current.Exports, export),
            ActorMemberId = actorMemberId,
            ActorDisplay = actorDisplay.Trim(),
            UpdatedAt = generatedAt
        };
        return Emit(state, actorMemberId, generatedAt,
            static (tenant, subject, revision, actor, snapshot, at) =>
                new AuditPopulationExportGenerated(tenant, subject, revision, actor, snapshot, at),
            "export");
    }

    void Apply(AuditPopulationDefined ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, true, "define");
    void Apply(AuditPopulationDefinitionRevised ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "definition_revision");
    void Apply(AuditPopulationGenerated ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "generate");
    void Apply(AuditPopulationFactRecorded ev) => ApplyState(ev.TenantId, ev.SubjectId, ev.Revision,
        ev.ActorMemberId, ev.State, ev.OccurredAt, false, "facts");
    void Apply(AuditPopulationRowClassified ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "classification");
    void Apply(AuditPopulationReconciled ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "reconcile");
    void Apply(AuditPopulationAmended ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "amend");
    void Apply(AuditPopulationFrozen ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "freeze");
    void Apply(AuditPopulationExportGenerated ev) => ApplyState(ev.TenantId, ev.SubjectId,
        ev.Revision, ev.ActorMemberId, ev.State, ev.OccurredAt, false, "export");

    void ApplyState(Uuid tenantId, Uuid subjectId, long revision, Uuid? actorMemberId,
        AuditPopulationView state, DateTimeOffset occurredAt, bool creating, string transition)
    {
        if (tenantId != _tenantId || subjectId != Id || state is null ||
            state.TenantId != _tenantId || state.ProgramId != _programId || state.AuditPopulationId != Id ||
            state.Revision != revision || state.UpdatedAt != occurredAt || occurredAt == default ||
            revision != Revision + 1 || creating != (_current is null) || actorMemberId == Uuid.Empty ||
            state.ActorMemberId != actorMemberId || ValidateState(state) is not null ||
            _current is null && (transition != "define" || !ValidInitialState(state)) ||
            _current is { } prior && !ValidTransition(prior, state, actorMemberId, transition))
            throw new InvalidOperationException("The audit-population event is invalid.");
        _current = Freeze(state);
    }

    CommandFailure? Emit(AuditPopulationView state, Uuid actorMemberId, DateTimeOffset occurredAt,
        Func<Uuid, Uuid, long, Uuid?, AuditPopulationView, DateTimeOffset, DomainEvent> createEvent,
        string transition, bool creating = false)
    {
        if (ValidateState(state) is not null ||
            (creating ? _current is not null : _current is null ||
                !ValidTransition(_current, state, actorMemberId, transition)))
            return Invalid("The audit-population transition is invalid.");
        var ev = createEvent(_tenantId, Id, state.Revision, actorMemberId, Freeze(state), occurredAt);
        RaiseEvent(ev);
        return null;
    }

    CommandFailure? ValidateState(AuditPopulationView state)
    {
        if (state.TenantId != _tenantId || state.ProgramId != _programId ||
            state.AuditPopulationId != Id || state.AuditPopulationDefinitionId == Uuid.Empty ||
            state.Revision <= 0 || state.PeriodStart == default || state.PeriodEnd < state.PeriodStart ||
            state.DefinitionVersions is null || state.DefinitionVersions.Count < 1 ||
            state.CloseSnapshot is null || !ValidCloseSnapshot(state.CloseSnapshot) ||
            state.SourceSnapshots is null ||
            state.Rows is null || state.Reconciliation is { } rec && !ValidReconciliation(rec) ||
            state.Versions is null ||
            state.Amendments is null ||
            state.Exports is null ||
            state.CurrentVersionNumber != state.Versions.Count ||
            state.Versions.Where((version, index) => version.Version != index + 1 ||
                !ValidPopulationVersion(version, state) || index > 0 &&
                (version.CloseSnapshotId != state.Versions[0].CloseSnapshotId ||
                 version.CloseSnapshotSha256 != state.Versions[0].CloseSnapshotSha256)).Any() ||
            state.DefinitionVersions.Where((version, index) => version.Version != index + 1 ||
                NormalizeDefinition(version.Content) is not { } definition ||
                Digest(definition) != version.ContentSha256 || version.RecordedByMemberId == Uuid.Empty ||
                !Bounded(version.RecordedByDisplay, 1, 200) || version.RecordedAt == default ||
                !Bounded(version.ChangeReason, 0, MaximumTextLength)).Any() ||
            !SameDefinitionVersion(state.CurrentDefinition!, state.DefinitionVersions[^1]) ||
            state.SourceSnapshots.Any(item => !ValidSourceSnapshot(item)) ||
            state.SourceSnapshots.Select(item => item.SnapshotId).Distinct().Count() != state.SourceSnapshots.Count ||
            state.Rows.Any(item => !ValidRow(item, state)) ||
            state.Rows.Select(item => item.RowId).Distinct().Count() != state.Rows.Count ||
            state.Reconciliation is { } reconciliation &&
                !reconciliation.SourceSnapshotIds.SequenceEqual(state.SourceSnapshots.Select(item => item.SnapshotId)) ||
            state.Versions.Any(version => version.SourceSnapshots.Any(source =>
                !state.SourceSnapshots.Any(current => SameSourceSnapshot(source, current))) ||
                version.Rows.Any(row => !ValidRow(row, state)) ||
                !version.Reconciliation.SourceSnapshotIds.SequenceEqual(version.SourceSnapshots.Select(item => item.SnapshotId))) ||
            state.Amendments.Where((item, index) => item.SupersededVersion != index + 1 ||
                item.SuccessorVersion != item.SupersededVersion + 1 ||
                !Bounded(item.Reason, 1, MaximumTextLength) || item.ChangedRowIds.Count == 0 ||
                !ValidIds(item.ChangedRowIds) || !IsHash(item.SupersededContentSha256) ||
                item.AmendedByMemberId == Uuid.Empty || !Bounded(item.AmendedByDisplay, 1, 200) ||
                item.AmendedAt == default).Any() ||
            state.Exports.Any(item => !ValidExport(item, state)) ||
            state.Exports.Select(item => item.ExportId).Distinct().Count() != state.Exports.Count ||
            state.CurrentVersionNumber != state.Versions.Count ||
            state.SampleEligible != (state.Status == "frozen" && (state.Current?.SampleEligible ?? false)) ||
            !ValidStatus(state) || !ValidActor(state.ActorMemberId, state.ActorDisplay, state.UpdatedAt))
            return Invalid("The audit-population event contains an invalid state snapshot.");
        return null;
    }

    static bool ValidTransition(AuditPopulationView prior, AuditPopulationView next,
        Uuid? actor, string transition)
    {
        if (next.Revision != prior.Revision + 1 || next.TenantId != prior.TenantId ||
            next.ProgramId != prior.ProgramId || next.AuditPopulationId != prior.AuditPopulationId ||
            next.AuditPopulationDefinitionId != prior.AuditPopulationDefinitionId ||
            next.TypeIIPeriodId != prior.TypeIIPeriodId || next.ExaminationId != prior.ExaminationId ||
            !Prefix(prior.DefinitionVersions, next.DefinitionVersions) ||
            !Prefix(prior.SourceSnapshots, next.SourceSnapshots) ||
            !Prefix(prior.Versions, next.Versions) || !Prefix(prior.Amendments, next.Amendments) ||
            !Prefix(prior.Exports, next.Exports))
            return false;
        return transition switch
        {
            "definition_revision" => (prior.Status is "defined" or "collecting") &&
                next.Status == "defined" && next.DefinitionVersions.Count == prior.DefinitionVersions.Count + 1 &&
                next.Rows.Count == 0 && next.Versions.Count == prior.Versions.Count &&
                next.ActorMemberId == actor && next.UpdatedAt == next.CurrentDefinition!.RecordedAt,
            "generate" => prior.Status == "defined" && next.Status == "collecting" &&
                next.GenerationKey is { Length: > 0 } && next.Rows.Count == 0 &&
                next.Versions.Count == prior.Versions.Count,
            "facts" => (prior.Status is "defined" or "collecting" or "amending") &&
                (next.Status is "collecting" or "amending") && next.Rows.Count > prior.Rows.Count &&
                next.Reconciliation is null && next.Versions.Count == prior.Versions.Count,
            "classification" => (prior.Status is "defined" or "collecting" or "amending") &&
                next.Status == prior.Status && next.Rows.Count == prior.Rows.Count &&
                next.Rows.Zip(prior.Rows).Count(pair => Digest(pair.First) != Digest(pair.Second)) == 1 &&
                next.Reconciliation is null && next.Versions.Count == prior.Versions.Count,
            "reconcile" => (prior.Status is "defined" or "collecting" or "amending") &&
                next.Status == "reconciled" && next.Rows.Count == prior.Rows.Count &&
                next.Reconciliation is not null && next.Versions.Count == prior.Versions.Count,
            "amend" => prior.Status == "frozen" && next.Status == "amending" &&
                next.Amendments.Count == prior.Amendments.Count + 1 &&
                next.Amendments[^1].AmendedByMemberId == actor && next.Reconciliation is null &&
                next.Versions.Count == prior.Versions.Count,
            "freeze" => prior.Status == "reconciled" && next.Status == "frozen" &&
                next.Versions.Count == prior.Versions.Count + 1 &&
                next.Versions[^1].FrozenByMemberId == actor && next.Versions[^1].FrozenAt == next.UpdatedAt,
            "export" => prior.Status is "frozen" or "amending" or "reconciled" &&
                next.Status == prior.Status &&
                next.Exports.Count == prior.Exports.Count + 1 &&
                next.Exports[^1].GeneratedByMemberId == actor && next.Exports[^1].GeneratedAt == next.UpdatedAt,
            _ => false
        };
    }

    static bool ValidInitialState(AuditPopulationView state) => state.Status == "defined" && state.Revision == 1 &&
        state.CurrentVersionNumber == 0 && state.DefinitionVersions.Count == 1 &&
        state.SourceSnapshots.Count == 0 && state.Rows.Count == 0 && state.Reconciliation is null &&
        state.Versions.Count == 0 && state.Amendments.Count == 0 && state.Exports.Count == 0;

    static bool ValidStatus(AuditPopulationView state)
    {
        var valid = state.Status switch
        {
            "defined" => state.Versions.Count == 0 && state.Amendments.Count == 0 &&
                state.GenerationKey is null && state.SourceSnapshots.Count == 0 && state.Reconciliation is null,
            "collecting" => state.Versions.Count == 0 && state.Amendments.Count == 0 &&
                (state.GenerationKey is not null || state.SourceSnapshots.Count > 0) && state.Reconciliation is null,
            "reconciled" => state.Reconciliation is not null &&
                (state.Versions.Count == 0 || state.Amendments.Count == state.Versions.Count),
            "amending" => state.Versions.Count > 0 && state.Amendments.Count == state.Versions.Count &&
                state.Reconciliation is null,
            "frozen" => state.Versions.Count > 0 && state.Amendments.Count == state.Versions.Count - 1 &&
                state.Reconciliation is not null,
            _ => false
        };
        return valid && state.SampleEligible ==
            (state.Status == "frozen" && state.Current?.SampleEligible == true);
    }

    static bool ValidPopulationVersion(AuditPopulationVersionView item, AuditPopulationView state) => item.Version > 0 &&
        item.DefinitionVersion > 0 && item.SourceSnapshots is not null && item.SourceSnapshots.Count > 0 &&
        item.Rows is not null && item.Rows.Count > 0 && item.Reconciliation is not null &&
        ValidReconciliation(item.Reconciliation) && item.CloseSnapshotId != Uuid.Empty &&
        IsHash(item.CloseSnapshotSha256) && IsHash(item.ContentSha256) &&
        item.SampleEligible == (item.Reconciliation.Completeness == "complete") &&
        item.FrozenByMemberId != Uuid.Empty && Bounded(item.FrozenByDisplay, 1, 200) &&
        item.FrozenAt != default && Bounded(item.FreezeRationale, 1, MaximumTextLength) &&
        state.DefinitionVersions.FirstOrDefault(definition => definition.Version == item.DefinitionVersion) is { } definition &&
        item.ContentSha256 == PopulationDigest(state.AuditPopulationDefinitionId, definition,
            item.CloseSnapshotId, item.CloseSnapshotSha256, item.SourceSnapshots, item.Rows, item.Reconciliation);

    static bool ValidReconciliation(AuditPopulationReconciliationView item) =>
        item.Completeness is "complete" or "incomplete" && item.SourceTotal >= 0 &&
        item.IncludedCount >= 0 && item.ExcludedCount >= 0 && item.DuplicateCount >= 0 &&
        item.MissingCount >= 0 && item.RejectedCount >= 0 && item.UnresolvedCount >= 0 &&
        item.CanceledCount >= 0 && item.NotApplicableCount >= 0 && item.ExceptionBackedCount >= 0 &&
        item.SourceSnapshotIds is not null && item.SourceSnapshotIds.Count > 0 && ValidDistinctIds(item.SourceSnapshotIds) &&
        Bounded(item.ReconciliationRationale, 1, MaximumTextLength) && IsHash(item.ContentSha256) &&
        item.ContentSha256 == Digest(item with { ContentSha256 = new string('0', 64) }) &&
        ValidActor(item.ReconciledByMemberId, item.ReconciledByDisplay, item.ReconciledAt) &&
        item.SourceTotal == item.IncludedCount + item.ExcludedCount + item.DuplicateCount +
            item.MissingCount + item.RejectedCount + item.UnresolvedCount + item.CanceledCount +
            item.NotApplicableCount + item.ExceptionBackedCount;

    static bool ValidRow(AuditPopulationRowView item, AuditPopulationView state) =>
        item.TenantId == state.TenantId && item.ProgramId == state.ProgramId &&
        item.AuditPopulationId == state.AuditPopulationId && item.RowId != Uuid.Empty &&
        state.SourceSnapshots.Any(source => source.SnapshotId == item.SourceSnapshotId) &&
        (item.SourceRowIdentity is null || Bounded(item.SourceRowIdentity, 1, 300)) &&
        Bounded(item.SourceReference, 1, 1000) && Classifications.Contains(item.Classification) &&
        Bounded(item.ClassificationRationale, 1, MaximumTextLength) && item.AuthorizedFields is not null &&
        item.AuthorizedFields.Count <= MaximumFields && item.AuthorizedFields.All(pair =>
            state.CurrentDefinition!.Content.AuthorizedFields.Contains(pair.Key, StringComparer.Ordinal) &&
            Bounded(pair.Key, 1, 200) && pair.Value is not null && pair.Value.Length <= MaximumFieldValueLength) &&
        item.SourceRecordReferences is not null && item.SourceRecordReferences.Count <= MaximumReferences &&
        item.SourceRecordReferences.All(ValidRecordReference) && item.EvidenceArtifactIds is not null &&
        ValidIds(item.EvidenceArtifactIds) && ValidActor(item.RecordedByMemberId, item.RecordedByDisplay, item.RecordedAt);

    static bool ValidRecordReference(AuditPopulationRecordReference item) =>
        item is not null && Bounded(item.RecordType, 1, 100) && item.RecordId != Uuid.Empty &&
        item.Version > 0 && IsHash(item.ContentSha256) && Bounded(item.Relationship, 1, 200);

    static bool ValidExport(AuditPopulationExportView item, AuditPopulationView state) =>
        item.ExportId != Uuid.Empty && state.Versions.Any(version => version.Version == item.PopulationVersion &&
            version.ContentSha256 == item.PopulationContentSha256 && version.CloseSnapshotId == item.CloseSnapshotId) &&
        ExportFormats.Contains(item.Format) && Bounded(item.FileName, 1, 255) &&
        !item.FileName.Contains('/') && !item.FileName.Contains('\\') &&
        (item.ArtifactId == Uuid.Empty && item.OutputContentSha256 is null && item.OutputContentLength is null ||
         item.ArtifactId != Uuid.Empty && item.OutputContentSha256 is { } sha && IsHash(sha) &&
         item.OutputContentLength is > 0) &&
        ValidActor(item.GeneratedByMemberId, item.GeneratedByDisplay, item.GeneratedAt);

    static bool ValidCloseSnapshot(TypeIIPeriodCloseSnapshotView item) => item.CloseSnapshotId != Uuid.Empty &&
        item.Version > 0 && IsHash(item.ContentSha256) && item.ActorMemberId != Uuid.Empty &&
        Bounded(item.ActorRole, 1, 100) && Bounded(item.ActorDisplay, 1, 200) && item.RecordedAt != default &&
        item.SourceReferences is not null && item.ExpectedOccurrenceIds is not null &&
        item.AcknowledgedMaterialItemIds is not null && item.ChangedRecordIds is not null &&
        item.AffectedExaminationMaterialIds is not null;

    static bool ValidClosedPeriod(TypeIIPeriodView? period) => period is not null &&
        period.Status == "closed" && period.CloseSnapshots is not null && period.CloseSnapshots.Count > 0 &&
        period.CloseSnapshots.All(ValidCloseSnapshot) && period.CloseSnapshots[^1].Version == period.CloseSnapshots.Count &&
        period.CloseSnapshots.Select(item => item.CloseSnapshotId).Distinct().Count() == period.CloseSnapshots.Count;

    static AuditPopulationDefinitionContent? NormalizeDefinition(AuditPopulationDefinitionContent? content)
    {
        if (content is null || !Bounded(content.Name, 1, 200) || !Bounded(content.Purpose, 1, MaximumTextLength) ||
            !Bounded(content.SourceUniverse, 1, MaximumTextLength) || content.PeriodStart == default ||
            content.PeriodEnd < content.PeriodStart || !Bounded(content.InclusionRules, 1, MaximumTextLength) ||
            !Bounded(content.ExclusionRules, 1, MaximumTextLength) ||
            !Bounded(content.ExpectedCompletenessEvidence, 1, MaximumTextLength) ||
            content.AuthorizedFields is null || content.AuthorizedFields.Count is < 1 or > MaximumFields ||
            content.AuthorizedFields.Any(field => !Bounded(field, 1, 200)) ||
            content.AuthorizedFields.Distinct(StringComparer.Ordinal).Count() != content.AuthorizedFields.Count ||
            content.ControlIds is null || content.ControlIds.Count > MaximumReferences || !ValidIds(content.ControlIds))
            return null;
        return content with
        {
            Name = content.Name.Trim(),
            Purpose = content.Purpose.Trim(),
            SourceUniverse = content.SourceUniverse.Trim(),
            InclusionRules = content.InclusionRules.Trim(),
            ExclusionRules = content.ExclusionRules.Trim(),
            ExpectedCompletenessEvidence = content.ExpectedCompletenessEvidence.Trim(),
            AuthorizedFields = Freeze(content.AuthorizedFields.Select(item => item.Trim())
                .Order(StringComparer.Ordinal).ToArray()),
            ControlIds = Freeze(content.ControlIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray())
        };
    }

    static AuditPopulationSourceSnapshotView? NormalizeSource(
        AuditPopulationSourceSnapshotContent? content, Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (content is null || content.SnapshotId == Uuid.Empty || content.Version <= 0 ||
            !Bounded(content.SourceReference, 1, 1000) || !IsHash(content.ContentSha256) ||
            content.ExpectedTotal < 0 || content.CapturedAt == default ||
            content.CapturedAt > at || content.CompletenessEvidence is { } evidence &&
                !Bounded(evidence, 1, MaximumTextLength) || !ValidActor(actorMemberId, actorDisplay, at))
            return null;
        return new AuditPopulationSourceSnapshotView(content.SnapshotId, content.Version,
            content.SourceReference.Trim(), content.ContentSha256.ToLowerInvariant(), content.ExpectedTotal,
            content.CollectionComplete, Clean(content.CompletenessEvidence) is { Length: > 0 } cleaned ? cleaned : null,
            content.CapturedAt, actorMemberId, actorDisplay.Trim());
    }

    static AuditPopulationRowView? NormalizeRow(AuditPopulationRowContent? content,
        AuditPopulationSourceSnapshotView source, AuditPopulationDefinitionContent definition,
        Uuid tenantId, Uuid programId, Uuid populationId,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (content is null || content.RowId == Uuid.Empty ||
            content.SourceRowIdentity is not null && !Bounded(content.SourceRowIdentity, 1, 300) ||
            !Bounded(content.SourceReference, 1, 1000) ||
            !Classifications.Contains(Clean(content.Classification).ToLowerInvariant()) ||
            !Bounded(content.ClassificationRationale, 1, MaximumTextLength) ||
            content.AuthorizedFields is null || content.AuthorizedFields.Count > MaximumFields ||
            content.AuthorizedFields.Any(pair => !definition.AuthorizedFields.Contains(pair.Key, StringComparer.Ordinal) ||
                !Bounded(pair.Key, 1, 200) || pair.Value is null || pair.Value.Length > MaximumFieldValueLength) ||
            content.SourceRecordReferences is null || content.SourceRecordReferences.Count > MaximumReferences ||
            content.SourceRecordReferences.Any(item => !ValidRecordReference(item)) ||
            content.EvidenceArtifactIds is null || !ValidIds(content.EvidenceArtifactIds) ||
            !ValidActor(actorMemberId, actorDisplay, at))
            return null;
        var fields = new ReadOnlyDictionary<string, string>(content.AuthorizedFields
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.Ordinal));
        return new AuditPopulationRowView(tenantId, programId, populationId, content.RowId,
            source.SnapshotId, Clean(content.SourceRowIdentity) is { Length: > 0 } identity ? identity : null,
            source.SourceReference, content.Classification.Trim().ToLowerInvariant(),
            content.ClassificationRationale.Trim(), fields,
            Freeze(content.SourceRecordReferences.OrderBy(item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(item => IdKey(item.RecordId), StringComparer.Ordinal).ToArray()),
            Freeze(content.EvidenceArtifactIds.OrderBy(IdKey, StringComparer.Ordinal).ToArray()),
            actorMemberId, actorDisplay.Trim(), at);
    }

    static AuditPopulationReconciliationView? NormalizeReconciliation(
        AuditPopulationReconciliationContent? content, AuditPopulationView state,
        Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        if (content is null || !Bounded(content.ReconciliationRationale, 1, MaximumTextLength) ||
            content.SourceTotal < 0 || content.SourceTotal != state.Rows.Count ||
            content.IncludedCount != Count(state, "included") || content.ExcludedCount != Count(state, "excluded") ||
            content.DuplicateCount != Count(state, "duplicate") || content.MissingCount != Count(state, "missing") ||
            content.RejectedCount != Count(state, "rejected") || content.UnresolvedCount != Count(state, "unresolved") ||
            content.CanceledCount != Count(state, "canceled") ||
            content.NotApplicableCount != Count(state, "not_applicable") ||
            content.ExceptionBackedCount != Count(state, "exception_backed") ||
            content.SourceTotal != content.IncludedCount + content.ExcludedCount + content.DuplicateCount +
                content.MissingCount + content.RejectedCount + content.UnresolvedCount + content.CanceledCount +
                content.NotApplicableCount + content.ExceptionBackedCount || state.SourceSnapshots.Count == 0 ||
            !ValidActor(actorMemberId, actorDisplay, at))
            return null;
        var currentSource = state.SourceSnapshots[^1];
        var complete = currentSource.CollectionComplete && currentSource.CompletenessEvidence is not null &&
            currentSource.ExpectedTotal == content.SourceTotal && content.DuplicateCount == 0 &&
            content.MissingCount == 0 && content.RejectedCount == 0 && content.UnresolvedCount == 0;
        var result = new AuditPopulationReconciliationView(complete ? "complete" : "incomplete",
            content.SourceTotal, content.IncludedCount, content.ExcludedCount, content.DuplicateCount,
            content.MissingCount, content.RejectedCount, content.UnresolvedCount, content.CanceledCount,
            content.NotApplicableCount, content.ExceptionBackedCount,
            Freeze(state.SourceSnapshots.Select(item => item.SnapshotId).ToArray()),
            content.ReconciliationRationale.Trim(), new string('0', 64), actorMemberId,
            actorDisplay.Trim(), at);
        return result with { ContentSha256 = Digest(result with { ContentSha256 = new string('0', 64) }) };
    }

    static AuditPopulationDefinitionVersionView NewDefinition(AuditPopulationDefinitionContent content,
        int version, Uuid actor, string display, DateTimeOffset at, string reason) =>
        new(version, content, Digest(content), actor, display, at, reason);

    static bool SameDefinition(AuditPopulationView state, Uuid definitionId,
        AuditPopulationDefinitionContent? content, TypeIIPeriodView? period,
        Uuid actor, string display, DateTimeOffset at) =>
        state.AuditPopulationDefinitionId == definitionId && state.CurrentDefinition is { } existing &&
        NormalizeDefinition(content) is { } normalized && Digest(normalized) == existing.ContentSha256 &&
        ValidClosedPeriod(period) && period!.TenantId == state.TenantId && period.ProgramId == state.ProgramId &&
        period.CloseSnapshots[^1].CloseSnapshotId == state.CloseSnapshot.CloseSnapshotId &&
        period.CloseSnapshots[^1].ContentSha256 == state.CloseSnapshot.ContentSha256 &&
        existing.RecordedByMemberId == actor && existing.RecordedByDisplay == Clean(display) && existing.RecordedAt == at;

    static bool SameDefinitionVersion(AuditPopulationDefinitionVersionView left,
        AuditPopulationDefinitionVersionView right) => left.Version == right.Version &&
        left.ContentSha256 == right.ContentSha256 && left.RecordedByMemberId == right.RecordedByMemberId &&
        left.RecordedByDisplay == right.RecordedByDisplay && left.RecordedAt == right.RecordedAt &&
        left.ChangeReason == right.ChangeReason;

    static bool SameSourceSnapshot(AuditPopulationSourceSnapshotView left,
        AuditPopulationSourceSnapshotView right) => Digest(left) == Digest(right);

    AuditPopulationRowView[]? NormalizeAmendedRows(IReadOnlyList<AuditPopulationRowContent> changedRows,
        AuditPopulationView state, AuditPopulationSourceSnapshotView source, Uuid actor,
        string display, DateTimeOffset at)
    {
        if (changedRows.Select(item => item.RowId).Distinct().Count() != changedRows.Count)
            return null;
        var normalizedRows = changedRows.Select(row => NormalizeRow(row, source,
            state.CurrentDefinition!.Content, _tenantId, _programId, Id, actor, display, at)).ToArray();
        return normalizedRows.Any(row => row is null) ? null : normalizedRows.Select(row => row!).ToArray();
    }

    static AuditPopulationView Freeze(AuditPopulationView state)
    {
        var rows = Freeze(state.Rows.Select(row => row with
        {
            AuthorizedFields = new ReadOnlyDictionary<string, string>(row.AuthorizedFields.ToDictionary(
                pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
            SourceRecordReferences = Freeze(row.SourceRecordReferences.ToArray()),
            EvidenceArtifactIds = Freeze(row.EvidenceArtifactIds.ToArray())
        }).ToArray());
        return state with
        {
            DefinitionVersions = Freeze(state.DefinitionVersions.ToArray()),
            SourceSnapshots = Freeze(state.SourceSnapshots.ToArray()),
            Rows = rows,
            Versions = Freeze(state.Versions.Select(version => version with
            {
                SourceSnapshots = Freeze(version.SourceSnapshots.ToArray()),
                Rows = Freeze(version.Rows.ToArray()),
                Reconciliation = FreezeReconciliation(version.Reconciliation)
            }).ToArray()),
            Reconciliation = state.Reconciliation is null ? null : FreezeReconciliation(state.Reconciliation),
            Amendments = Freeze(state.Amendments.Select(item => item with
            {
                ChangedRowIds = Freeze(item.ChangedRowIds.ToArray())
            }).ToArray()),
            Exports = Freeze(state.Exports.ToArray())
        };
    }

    static AuditPopulationReconciliationView FreezeReconciliation(AuditPopulationReconciliationView item) =>
        item with { SourceSnapshotIds = Freeze(item.SourceSnapshotIds.ToArray()) };

    static int Count(AuditPopulationView state, string classification) =>
        state.Rows.Count(row => row.Classification == classification);

    static string PopulationDigest(AuditPopulationView state, AuditPopulationReconciliationView reconciliation) =>
        PopulationDigest(state.AuditPopulationDefinitionId, state.CurrentDefinition!,
            state.CloseSnapshot.CloseSnapshotId, state.CloseSnapshot.ContentSha256,
            state.SourceSnapshots, state.Rows, reconciliation);

    static string PopulationDigest(Uuid definitionId, AuditPopulationDefinitionVersionView definition,
        Uuid closeSnapshotId, string closeSnapshotSha256,
        IReadOnlyList<AuditPopulationSourceSnapshotView> sourceSnapshots,
        IReadOnlyList<AuditPopulationRowView> rows,
        AuditPopulationReconciliationView reconciliation) => Digest(new
        {
            AuditPopulationDefinitionId = definitionId,
            Definition = definition,
            CloseSnapshotId = closeSnapshotId,
            ContentSha256 = closeSnapshotSha256,
            SourceSnapshots = sourceSnapshots,
            Rows = rows,
            Reconciliation = reconciliation
        });

    static bool ValidSourceSnapshot(AuditPopulationSourceSnapshotView item) =>
        item.SnapshotId != Uuid.Empty && item.Version > 0 && Bounded(item.SourceReference, 1, 1000) &&
        IsHash(item.ContentSha256) && item.ExpectedTotal >= 0 &&
        (item.CompletenessEvidence is null || Bounded(item.CompletenessEvidence, 1, MaximumTextLength)) &&
        item.CapturedAt != default && ValidActor(item.RecordedByMemberId, item.RecordedByDisplay, item.CapturedAt);

    static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static bool ValidIds(IReadOnlyList<Uuid> ids) => ids.Count <= MaximumReferences && ValidDistinctIds(ids);
    static bool ValidDistinctIds(IReadOnlyList<Uuid> ids) =>
        ids.All(static id => id != Uuid.Empty) && ids.Distinct().Count() == ids.Count;
    static bool Prefix<T>(IReadOnlyList<T> prior, IReadOnlyList<T> next) => prior.Count <= next.Count &&
        prior.Zip(next).All(pair => Digest(pair.First) == Digest(pair.Second));

    static string Digest<T>(T value) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    static string IdKey(Uuid id) => id.ToString();
    static string Clean(string? value) => value?.Trim() ?? string.Empty;
    static bool Bounded(string? value, int minimum, int maximum) => value is not null &&
        value.Trim().Length >= minimum && value.Trim().Length <= maximum;
    static bool ValidActor(Uuid id, string? display, DateTimeOffset at) => id != Uuid.Empty &&
        Bounded(display, 1, 200) && at != default;
    bool ValidIdentity() => _tenantId != Uuid.Empty && _programId != Uuid.Empty && Id != Uuid.Empty;
    CommandFailure StaleRevision() => CommandFailure.ForVersion(
        VersionedRecordRules.StaleRevision("audit population", Revision));
    static CommandFailure Invalid(string message) => CommandFailure.InvalidContent(message);
    static ReadOnlyCollection<T> Freeze<T>(T[] values) => Array.AsReadOnly(values);
    static ReadOnlyCollection<T> Append<T>(IReadOnlyList<T> values, T value) => Freeze(values.Append(value).ToArray());
}
