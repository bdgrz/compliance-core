using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.SystemDescription;
using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
/// Discovers attributable existing examination material from complete authoritative source cuts. Native package versions
/// are read from verified content; legacy State facts are inspected only at their explicit scoped reference fields.
/// No directory absence, global identity lookup or invented material identity establishes completeness.
/// </summary>
public sealed class TypeIIPeriodCloseMaterialReader(IDomainEventReader events, IImmutableContentStore content)
{
    const int MaximumFacts = 25000;
    static readonly string[] Areas = ["audit-packages", "audit-populations", "management-assertions", "audit-outcomes",
        "examination-signoffs", "management-representation-letters", "program-rollovers", "system-descriptions", "auditor-requests"];

    public async ValueTask<ImmutableContentRef> FreezeAsync(TypeIIPeriodView period,
        TypeIIPeriodCloseSnapshotView prior, TypeIIPeriodCloseValidationContent reconciliation,
        OperationProvenance operation, CancellationToken ct)
    {
        if (reconciliation.SourceContentManifest is not { } publication || reconciliation.RecordedThrough != operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("A close amendment requires its complete exact source publication.");
        var records = await ScanAsync(period, operation.AcceptedAt, ct).ConfigureAwait(false);
        var affected = new HashSet<Uuid> { prior.CloseSnapshotId };
        var affectedPopulations = new HashSet<Uuid>();
        var affectedRequests = new HashSet<Uuid>();
        foreach (var record in records)
        {
            if (record.Fact.Stream.Area == AuditPackageManifest.Area)
            {
                if (record.Payload.TryGetProperty("change", out var change) &&
                    (Is<AuditPackageCreated>(record) || Is<AuditPackageAmended>(record)))
                {
                    var reference = JsonSerializer.Deserialize(change, ComplianceCoreJsonContext.Default.ImmutableContentRef)
                        ?? throw new ImmutableContentIntegrityException("The package native source has no change reference.");
                    var value = await content.ReadJsonAsync<AuditPackageManifestVersionContent>(period.TenantId.ToString(), reference, ct).ConfigureAwait(false);
                    if (value.ProgramId != period.ProgramId || value.Manifest.TenantId != period.TenantId ||
                        value.Manifest.ProgramId != period.ProgramId || value.Manifest.AuditPackageId.ToString() != record.Fact.Stream.Resource)
                        throw new ImmutableContentIntegrityException("The package impact source differs from its native owner scope.");
                    if (value.Content.SourcePeriodId == period.PeriodId && value.Content.ExaminationId == period.ExaminationId &&
                        value.Content.SourceSnapshotKind == "type_ii_close_snapshot" && value.Content.SourceSnapshotId == prior.CloseSnapshotId &&
                        StringComparer.OrdinalIgnoreCase.Equals(value.Content.SourceSnapshotSha256, prior.ContentSha256))
                    {
                        affected.Add(value.Manifest.AuditPackageId);
                        affected.Add(value.Manifest.AuditPackageManifestId);
                    }
                }
                continue;
            }
            if (record.Fact.Stream.Area == "auditor-requests")
                continue;
            if (record.Fact.Stream.Area == "system-descriptions")
            {
                if (record.Payload.TryGetProperty("version", out var versionJson))
                {
                    var version = JsonSerializer.Deserialize(versionJson, ComplianceCoreJsonContext.Default.SystemDescriptionVersionView)
                        ?? throw new ImmutableContentIntegrityException("A system description version has no exact frozen scope.");
                    if (version.TenantId != period.TenantId || version.ProgramId != period.ProgramId ||
                        version.SystemDescriptionId.ToString() != record.Fact.Stream.Resource)
                        throw new ImmutableContentIntegrityException("The system description differs from its native material owner scope.");
                    if (version.Scope.TypeIIPeriodId == period.PeriodId && version.Scope.ExaminationId == period.ExaminationId &&
                        version.Scope.CloseSnapshotId == prior.CloseSnapshotId)
                    {
                        affected.Add(version.SystemDescriptionId);
                        affected.Add(version.SystemDescriptionVersionId);
                    }
                }
                continue;
            }
            var subject = Uuid.Parse(record.Fact.Stream.Resource, CultureInfo.InvariantCulture);
            var body = record.Payload.TryGetProperty("state", out var state) ? state : record.Payload;
            if (ReferencesClose(body, prior))
            {
                affected.Add(subject);
                if (record.Fact.Stream.Area == "audit-populations")
                {
                    affectedPopulations.Add(subject);
                    CollectIds(body, "export_id", affected);
                }
            }
        }
        // A letter or management review can reference the exact affected package/assertion rather than repeat the close.
        bool changed;
        do
        {
            changed = false;
            foreach (var record in records.Where(row => row.Fact.Stream.Area is "management-representation-letters" or
                         "management-assertions" or "examination-signoffs" or "audit-outcomes" or "program-rollovers"))
            {
                var body = record.Payload.TryGetProperty("state", out var state) ? state : record.Payload;
                if (ReferencesMaterial(body, affected))
                    changed |= affected.Add(Uuid.Parse(record.Fact.Stream.Resource, CultureInfo.InvariantCulture));
            }
        } while (changed);
        foreach (var record in records.Where(row => row.Fact.Stream.Area == "auditor-requests" &&
                     Is<AuditorSampleSelectionAdmitted>(row)))
        {
            var reference = JsonSerializer.Deserialize(record.Payload.GetProperty("intent"), ComplianceCoreJsonContext.Default.ImmutableContentRef)
                ?? throw new ImmutableContentIntegrityException("A sample admission has no verified intent reference.");
            var intent = await content.ReadJsonAsync<AuditorSampleSelectionIntent>(period.TenantId.ToString(), reference, ct).ConfigureAwait(false);
            if (intent.ProgramId != period.ProgramId || intent.RequestId.ToString() != record.Fact.Stream.Resource ||
                intent.Header.FirstOwner != record.Fact.Stream || intent.Header.RootOperationId != intent.SelectionId)
                throw new ImmutableContentIntegrityException("An admitted sample differs from its exact native request owner.");
            if (!affectedPopulations.Contains(intent.PopulationId))
                continue;
            var selected = await content.ReadJsonAsync<AuditPopulationVersionView>(period.TenantId.ToString(), intent.PopulationContent, ct).ConfigureAwait(false);
            if (selected.Version != intent.PopulationVersion)
                throw new ImmutableContentIntegrityException("The selected population version differs from its exact admitted source.");
            if (selected.CloseSnapshotId != prior.CloseSnapshotId || selected.CloseSnapshotSha256 != prior.ContentSha256)
                continue;
            affected.Add(intent.SelectionId);
            affected.Add(intent.RequestId);
            affectedRequests.Add(intent.RequestId);
        }
        foreach (var record in records.Where(row => row.Fact.Stream.Area == "auditor-requests" &&
                     (Is<AuditorResponseAdmitted>(row) || Is<AuditorCommentAdmitted>(row))))
        {
            if (!affectedRequests.Contains(Uuid.Parse(record.Fact.Stream.Resource, CultureInfo.InvariantCulture)))
                continue;
            var reference = JsonSerializer.Deserialize(record.Payload.GetProperty("intent"), ComplianceCoreJsonContext.Default.ImmutableContentRef)
                ?? throw new ImmutableContentIntegrityException("An admitted request child has no frozen intent.");
            var intent = await content.ReadJsonAsync<ChildAdmissionIntent>(period.TenantId.ToString(), reference, ct).ConfigureAwait(false);
            if (intent.ParentId.ToString() != record.Fact.Stream.Resource || intent.Header.FirstOwner != record.Fact.Stream ||
                intent.ChildId != intent.Header.RootOperationId)
                throw new ImmutableContentIntegrityException("An affected child differs from its exact admitted request and operation.");
            affected.Add(intent.ChildId);
        }
        var confirmed = await ScanAsync(period, operation.AcceptedAt, ct).ConfigureAwait(false);
        if (records.Count != confirmed.Count || records.Zip(confirmed).Any(pair => pair.First.Fact != pair.Second.Fact ||
                pair.First.PayloadSha256 != pair.Second.PayloadSha256))
            throw new ImmutableContentUnavailableException("Examination material changed during exact close amendment capture.");
        var proof = new TypeIIPeriodCloseMaterialProof(new DurableIntentHeader(operation.RootOperationId, operation.Owner,
            operation.AffectedOwner, operation.ExpectedRevisionOwner, operation.Actor, operation.Episode,
            operation.OriginalExpectedRevision, operation.AuthoredIntentSha256, operation.AcceptedAt), period.PeriodId,
            prior.CloseSnapshotId, prior.ContentSha256, publication,
            TypeIIPeriodRules.ChangedRecordIds(prior.SourceReferences, reconciliation.SourceReferences),
            affected.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), records);
        return await content.WriteJsonAsync(period.TenantId.ToString(), Uuid.CreateVersion4(), TypeIIPeriodCloseMaterialProof.Schema, proof, ct).ConfigureAwait(false);
    }

    async Task<IReadOnlyList<TypeIIPeriodCloseNativeSource>> ScanAsync(TypeIIPeriodView period, DateTimeOffset through, CancellationToken ct)
    {
        var result = new List<TypeIIPeriodCloseNativeSource>();
        foreach (var area in Areas)
        {
            var requests = new Dictionary<string, Uuid>(StringComparer.Ordinal);
            await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(period.TenantId.ToString(), area), EventCursor.Start, ct).ConfigureAwait(false))
            {
                if (record.Event.Metadata.IsAudit)
                    continue;
                if (record.Stream.Realm != period.TenantId.ToString() || record.Stream.Area != area ||
                    record.Event.Metadata.EventId == Uuid.Empty || record.Event.Metadata.AggregateVersion == 0)
                    throw new ImmutableContentIntegrityException("Material discovery returned a foreign or unbound native source.");
                var native = TypeIIPeriodCloseHistory.Capture(record.Stream, record.Event);
                var body = native.Payload.TryGetProperty("state", out var state) ? state : native.Payload;
                Uuid program;
                if (record.Event is AuditorRequestRecorded or AuditorRequestRevised or AuditorRequestAssigned)
                {
                    var termsReference = JsonSerializer.Deserialize(native.Payload.GetProperty("change"), ComplianceCoreJsonContext.Default.ImmutableContentRef)
                        ?? throw new ImmutableContentIntegrityException("An auditor request has no verified terms.");
                    var terms = await content.ReadJsonAsync<AuditorRequestTermsContent>(period.TenantId.ToString(), termsReference, ct).ConfigureAwait(false);
                    requests[record.Stream.Resource] = program = terms.ProgramId;
                }
                else if (area == "auditor-requests")
                {
                    if (!requests.TryGetValue(record.Stream.Resource, out program))
                        throw new ImmutableContentUnavailableException("The complete request terms source is not visible yet.");
                }
                else
                    program = Id(body, "program_id");
                if (program == Uuid.Empty)
                    throw new ImmutableContentIntegrityException("A material source has no exact program scope.");
                if (program != period.ProgramId)
                    continue;
                var scopedTenant = Id(body, "tenant_id");
                if (scopedTenant != Uuid.Empty && scopedTenant != period.TenantId)
                    throw new ImmutableContentIntegrityException("The material source differs from its native tenant identity.");
                var scopedSubject = Id(native.Payload, "subject_id");
                if (scopedSubject != Uuid.Empty && scopedSubject.ToString() != record.Stream.Resource)
                    throw new ImmutableContentIntegrityException("The material source differs from its native subject identity.");
                var at = native.Payload.TryGetProperty("occurred_at", out var occurred) ? occurred.GetDateTimeOffset() :
                    record.Event is INativeOperationFact fact ? fact.Operation.AcceptedAt :
                    throw new ImmutableContentIntegrityException("The material source has no exact receipt timestamp.");
                if (at > through)
                    throw new ImmutableContentUnavailableException("Examination material advanced after the close amendment receipt cut.");
                if (result.Count >= MaximumFacts)
                    throw new ImmutableContentIntegrityException("The complete examination impact proof exceeds the supported source bound.");
                result.Add(native);
            }
        }
        return result.OrderBy(row => row.Fact.Stream.ToString(), StringComparer.Ordinal)
            .ThenBy(row => row.Fact.AggregateVersion).ToArray();
    }

    static bool ReferencesClose(JsonElement value, TypeIIPeriodCloseSnapshotView prior)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var pair in new[] { ("source_snapshot_id", "source_snapshot_sha256"),
                         ("source_close_snapshot_id", "source_close_snapshot_sha256"), ("close_snapshot_id", "close_snapshot_sha256"),
                         ("close_snapshot_id", "content_sha256") })
                if (Id(value, pair.Item1) == prior.CloseSnapshotId && value.TryGetProperty(pair.Item2, out var hash) &&
                    hash.ValueKind == JsonValueKind.String && StringComparer.OrdinalIgnoreCase.Equals(hash.GetString(), prior.ContentSha256))
                    return true;
            foreach (var property in value.EnumerateObject())
                if (ReferencesClose(property.Value, prior))
                    return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(item => ReferencesClose(item, prior));
        return false;
    }

    static bool ReferencesMaterial(JsonElement value, HashSet<Uuid> ids)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
                if (property.Name is "package_id" or "assertion_id" or "representation_letter_id" or "audit_outcome_id" &&
                    Uuid.TryParse(property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null, out var id) && ids.Contains(id) ||
                    ReferencesMaterial(property.Value, ids))
                    return true;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Any(item => ReferencesMaterial(item, ids));
        return false;
    }

    static void CollectIds(JsonElement value, string name, HashSet<Uuid> ids)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                if (property.Name == name && Uuid.TryParse(property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null, out var id) && id != Uuid.Empty)
                    ids.Add(id);
                CollectIds(property.Value, name, ids);
            }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                CollectIds(item, name, ids);
    }

    static bool Is<T>(TypeIIPeriodCloseNativeSource source) where T : DomainEvent =>
        source.EventKind == EventKind.Of(typeof(T));

    static Uuid Id(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var id) && id.ValueKind == JsonValueKind.String && Uuid.TryParse(id.GetString(), out var parsed) ? parsed : Uuid.Empty;
}
