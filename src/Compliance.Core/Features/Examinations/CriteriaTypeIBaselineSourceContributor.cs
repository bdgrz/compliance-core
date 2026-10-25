using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Features.SourceFacts;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Resolves the criteria edition selected for the program at the Type I date.</summary>
public sealed class CriteriaTypeIBaselineSourceContributor(IAggregateReader reader,
    ICriteriaCatalog catalog, ICriteriaTextOverlayReader overlays, IImmutableContentStore? content = null)
    : ITypeIBaselineSourceContributor
{
    const string SelectionDomain = "bdgrz.snapshot.source.criteria_selection.v1\0";
    const string EditionDomain = "bdgrz.snapshot.source.criteria_edition.v1\0";

    public string Family => "criteria";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        try
        {
            return await ReadCoreAsync(engagement, context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact criteria source cannot be verified: " + exception.Message,
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<TypeIBaselineSourceContribution>> ReadCoreAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        var through = new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var receiptCut = context.RecordedThrough is null ? null : context.RequireReceipts(engagement.TenantId);
        var program = await reader.HydrateAsync(new ComplianceProgram(engagement.TenantId,
            engagement.ProgramId), ct).ConfigureAwait(false);
        var programReceipts = receiptCut is null ? null : await receiptCut.ReadAsync(program.Stream, ct).ConfigureAwait(false);
        var selectedFact = programReceipts?.Included.Select(record => record.Event).OfType<ProgramCriteriaEditionSelected>()
            .LastOrDefault();
        if (selectedFact is not null && (selectedFact.TenantId != engagement.TenantId || selectedFact.ProgramId != engagement.ProgramId))
            throw new ImmutableContentIntegrityException("The criteria selection belongs to another exact program.");
        var selection = receiptCut is null ? program.CriteriaSelectionAt(through) : selectedFact is null ? null
            : new ProgramCriteriaEditionSelection(selectedFact.Revision, selectedFact.EditionId, selectedFact.ChangedAt);
        if (!program.IsCreated || selection is null)
            return Result<TypeIBaselineSourceContribution>.Success(Contribution([],
                ["No criteria edition was selected for the program by the Type I date."], []));

        var edition = catalog.GetEdition(selection.EditionId);
        if (edition is null)
            return Result<TypeIBaselineSourceContribution>.Success(Contribution([],
                ["The criteria edition selected for the program is unavailable."], []));

        var originalEntries = catalog.ListEntries(selection.EditionId, null, null, null);
        if (originalEntries.Count == 0)
            return Result<TypeIBaselineSourceContribution>.Success(Contribution([],
                ["The selected criteria edition contains no criteria entries."], []));

        var applied = await overlays.ApplyPageAsync(engagement.TenantId, originalEntries, false, ct)
            .ConfigureAwait(false);
        if (!applied.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(applied.Error);
        if (applied.Value.Count != originalEntries.Count ||
            applied.Value.Where((entry, index) => entry.EditionId != selection.EditionId ||
                entry.Identifier != originalEntries[index].Identifier).Any())
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The criteria overlay reader returned an invalid edition page."));

        var blockers = new List<string>();
        var scopeDifferences = edition.SupportGaps.Select(gap =>
            $"Criteria edition {edition.EditionLabel} declares {gap.Category} support gap {gap.Code}: {gap.Note}")
            .ToList();
        if (!edition.IsComplete)
            blockers.Add($"Criteria edition {edition.EditionLabel} is marked incomplete.");
        var effectiveEntries = new Criterion[originalEntries.Count];
        for (var index = 0; index < applied.Value.Count; index++)
        {
            var entry = applied.Value[index];
            if (receiptCut is not null)
            {
                effectiveEntries[index] = await HistoricalOverlayAsync(engagement.TenantId, originalEntries[index],
                    entry, receiptCut, ct).ConfigureAwait(false);
                continue;
            }
            if (entry.Overlay is { } current && (current.Revision < 1 ||
                string.IsNullOrWhiteSpace(current.Supplier) ||
                string.IsNullOrWhiteSpace(current.LicenseReference) ||
                current.Actor.Kind != "member" || current.RecordedAt == default ||
                current.AllowedUses is null))
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The criteria overlay reader returned an invalid source version."));
            if (entry.Overlay is { } overlay && overlay.RecordedAt > through)
            {
                if (overlay.Revision > 1)
                {
                    var difference = $"Criteria overlay {entry.Identifier} has later revisions, and its historical version is unavailable.";
                    blockers.Add(difference);
                    scopeDifferences.Add(difference);
                }
                effectiveEntries[index] = originalEntries[index];
                continue;
            }
            effectiveEntries[index] = entry;
        }

        var confirmed = await reader.HydrateAsync(new ComplianceProgram(engagement.TenantId,
            engagement.ProgramId), ct).ConfigureAwait(false);
        if (receiptCut is null && !Equals(selection, confirmed.CriteriaSelectionAt(through)))
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The program criteria selection changed while the baseline preview was read. Retry the preview.",
                isTransient: true));

        var sources = new TypeIBaselineSourceReference[]
        {
            new("criteria_selection", engagement.ProgramId, selection.EditionId,
                selection.Revision, SelectionDigest(engagement.TenantId, engagement.ProgramId,
                    selection)),
            new("criteria_edition", edition.EditionId, edition.EditionId, 1,
                EditionDigest(edition, effectiveEntries)),
        };
        return Result<TypeIBaselineSourceContribution>.Success(Contribution(sources, blockers,
            scopeDifferences));
    }

    async Task<Criterion> HistoricalOverlayAsync(Uuid tenant, Criterion original, Criterion projected,
        RecordedSourceReceiptCut cut, CancellationToken ct)
    {
        var stream = new CriteriaTextOverlay(tenant, original.EditionId, original.Identifier).Stream;
        var captured = await cut.ReadAsync(stream, ct).ConfigureAwait(false);
        var all = captured.Records.Select(record => record.Event).OfType<CriteriaTextOverlayEntryRevised>().ToArray();
        if (all.Length != captured.Records.Count)
            throw new ImmutableContentIntegrityException("The overlay source contains another native fact kind.");
        CriteriaTextOverlayRevision? retained = null;
        CriteriaTextOverlayRevision? current = null;
        long revision = 0;
        foreach (var fact in all)
        {
            if (fact.TenantId != tenant || fact.EditionId != original.EditionId || fact.Identifier != original.Identifier ||
                fact.Operation.Owner != stream || fact.Revision != ++revision)
                throw new ImmutableContentIntegrityException("The licensed overlay source has another exact scope or revision.");
            if (content is null)
                throw new ImmutableContentUnavailableException("Historical licensed overlay content is not configured.");
            current = await CriteriaTextOverlayHistory.ReadRevisionAsync(content, fact, ct).ConfigureAwait(false);
            if (captured.Includes(fact))
                retained = current;
        }
        if (current is null)
        {
            if (projected != original)
                throw new ImmutableContentUnavailableException("The projected overlay has no exact native source.");
            return original;
        }
        if (CriteriaTextOverlayPolicy.Apply(original, current, current, false) != projected)
            throw new ImmutableContentUnavailableException("The projected licensed overlay differs from its current native source.");
        return retained is null ? original : CriteriaTextOverlayPolicy.Apply(original, retained, current, false);
    }

    static TypeIBaselineSourceContribution Contribution(
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        new("criteria", sources, blockers, scopeDifferences);

    static string SelectionDigest(Uuid tenantId, Uuid programId,
        ProgramCriteriaEditionSelection selection)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteString("tenant_id", tenantId.ToString());
            writer.WriteString("program_id", programId.ToString());
            writer.WriteNumber("selection_revision", selection.Revision);
            writer.WriteString("edition_id", selection.EditionId.ToString());
            writer.WriteString("selected_at", selection.SelectedAt.ToUniversalTime());
            writer.WriteEndObject();
        }
        return Digest(SelectionDomain, stream.ToArray());
    }

    static string EditionDigest(CriteriaCatalogEdition edition,
        IReadOnlyList<Criterion> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartObject();
            writer.WriteString("edition_id", edition.EditionId.ToString());
            Write(writer, "framework", edition.Framework);
            Write(writer, "edition_label", edition.EditionLabel);
            writer.WriteString("published_at", edition.PublishedAt.ToUniversalTime());
            writer.WriteBoolean("is_complete", edition.IsComplete);
            Write(writer, "coverage_note", edition.CoverageNote);
            Write(writer, "source_url", edition.SourceUrl);
            Write(writer, "content_rights", edition.ContentRights);
            writer.WritePropertyName("support_gaps");
            writer.WriteStartArray();
            foreach (var gap in edition.SupportGaps.OrderBy(static gap => gap.Category,
                         StringComparer.Ordinal).ThenBy(static gap => gap.Code,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                Write(writer, "category", gap.Category);
                Write(writer, "code", gap.Code);
                Write(writer, "note", gap.Note);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            foreach (var entry in entries.OrderBy(static entry => entry.Identifier,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                Write(writer, "identifier", entry.Identifier);
                Write(writer, "source_identifier", entry.SourceIdentifier);
                Write(writer, "category", entry.Category);
                Write(writer, "kind", entry.Kind);
                Write(writer, "parent_identifier", entry.ParentIdentifier);
                Write(writer, "summary", entry.Summary);
                Write(writer, "licensed_text", entry.LicensedText);
                if (entry.Overlay is { } overlay)
                {
                    writer.WritePropertyName("overlay");
                    writer.WriteStartObject();
                    writer.WriteNumber("revision", overlay.Revision);
                    Write(writer, "supplier", overlay.Supplier);
                    Write(writer, "license_reference", overlay.LicenseReference);
                    writer.WriteBoolean("display", overlay.AllowedUses.Display);
                    writer.WriteBoolean("export", overlay.AllowedUses.Export);
                    writer.WriteString("actor_id", overlay.Actor.Id.ToString());
                    Write(writer, "actor_kind", overlay.Actor.Kind);
                    Write(writer, "actor_display", overlay.Actor.Display);
                    writer.WriteString("recorded_at", overlay.RecordedAt.ToUniversalTime());
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("overlay");
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Digest(EditionDomain, stream.ToArray());
    }

    static void Write(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteString(name, value.Normalize(NormalizationForm.FormC));
    }

    static string Digest(string domain, byte[] content)
    {
        var prefix = Encoding.UTF8.GetBytes(domain);
        var material = new byte[prefix.Length + content.Length];
        prefix.CopyTo(material, 0);
        content.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }
}
