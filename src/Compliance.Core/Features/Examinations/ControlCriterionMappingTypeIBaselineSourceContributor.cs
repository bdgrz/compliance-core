using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Events;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.SourceFacts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.Snapshots;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Captures control-to-criterion mapping history known on the Type I date.</summary>
public sealed class ControlCriterionMappingTypeIBaselineSourceContributor(
    ControlMappingReader mappingReader, CriteriaCoverageReadConsistency consistency, ICriteriaCatalog catalog)
    : ITypeIBaselineSourceContributor
{
    const string DigestDomain = "bdgrz.snapshot.source.control_criterion_mapping_version.v1\0";

    public string Family => "control_criterion_mappings";

    public ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, CancellationToken ct) =>
        ReadAsync(engagement, new TypeIBaselineSourceReadContext(Uuid.Empty, []), ct);

    public async ValueTask<Result<TypeIBaselineSourceContribution>> ReadAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context,
        CancellationToken ct)
    {
        try
        {
            return await ReadCoreAsync(engagement, context, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact mapping broker receipt source cannot be verified: " + exception.Message,
                isTransient: exception is ImmutableContentUnavailableException));
        }
    }

    async ValueTask<Result<TypeIBaselineSourceContribution>> ReadCoreAsync(
        ExaminationEngagementView engagement, TypeIBaselineSourceReadContext context, CancellationToken ct)
    {
        if (engagement is null || context is null || context.PreviouslyCapturedSources is null)
            return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "A control-mapping baseline read requires its engagement and source context."));

        var receiptCut = context.RecordedThrough is null ? null : context.RequireReceipts(engagement.TenantId);
        var editionSources = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "criteria_edition").ToArray();
        if (editionSources.Length != 1 || editionSources[0].RecordId == Uuid.Empty ||
            editionSources[0].RecordId != editionSources[0].VersionId ||
            editionSources[0].Revision < 1 || !IsDigest(editionSources[0].ContentSha256))
            return Success([], ["Control mappings were not read from the exact captured criteria edition."], []);

        var editionId = editionSources[0].VersionId;
        var selectionSources = context.PreviouslyCapturedSources.Where(static source =>
            source.Kind == "criteria_selection").ToArray();
        if (selectionSources.Length != 1 || selectionSources[0].RecordId != engagement.ProgramId ||
            selectionSources[0].VersionId != editionId || selectionSources[0].Revision < 1 ||
            !IsDigest(selectionSources[0].ContentSha256))
            return Success([], ["Control mappings were not read from the program's exact criteria selection."], []);
        if (catalog.GetEdition(editionId) is null)
            return Success([], ["The captured criteria edition is unavailable for control mappings."], []);

        var controlVersions = new Dictionary<Uuid, Uuid>();
        foreach (var source in context.PreviouslyCapturedSources.Where(static source =>
                     source.Kind == "control_version"))
        {
            if (source.RecordId == Uuid.Empty || source.VersionId == Uuid.Empty ||
                source.Revision < 1 || !IsDigest(source.ContentSha256) ||
                !controlVersions.TryAdd(source.RecordId, source.VersionId))
                return SourceConflict();
        }

        var initialSource = await mappingReader.ReadProgramAsync(engagement.TenantId, engagement.ProgramId, ct)
            .ConfigureAwait(false);
        var projected = await consistency.ReadMappingsAsync(engagement.TenantId,
            engagement.ProgramId, ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(projected.Error);
        var projectedConfirmation = await consistency.ReadMappingsAsync(engagement.TenantId,
            engagement.ProgramId, ct).ConfigureAwait(false);
        if (!projectedConfirmation.IsSuccess)
            return Result<TypeIBaselineSourceContribution>.Failure(projectedConfirmation.Error);
        var confirmedSource = await mappingReader.ReadProgramAsync(engagement.TenantId, engagement.ProgramId, ct)
            .ConfigureAwait(false);
        if (initialSource.Fence != confirmedSource.Fence ||
            !MappingsMatch(initialSource.Mappings, projected.Value) ||
            !MappingsMatch(confirmedSource.Mappings, projectedConfirmation.Value) ||
            !MappingsMatch(projected.Value, projectedConfirmation.Value))
            return SourceConflict();

        var criteria = catalog.ListEntries(editionId, null, null, null)
            .ToDictionary(static entry => entry.Identifier, StringComparer.Ordinal);
        var through = context.RecordedThrough ?? new DateTimeOffset(engagement.PeriodStart.ToDateTime(TimeOnly.MaxValue,
            DateTimeKind.Utc));
        var sources = new List<TypeIBaselineSourceReference>();
        var blockers = new List<string>();
        var scopeDifferences = new List<string>();
        foreach (var mapping in confirmedSource.Mappings)
        {
            if (mapping.EditionId != editionId)
                continue;
            if (!IsValid(mapping, engagement))
                return SourceConflict();
            if (!criteria.TryGetValue(mapping.CriterionIdentifier, out var criterion) ||
                criterion.Kind != mapping.CriterionKind)
                return Result<TypeIBaselineSourceContribution>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "A control mapping does not identify an entry in the selected criteria edition."));

            var receiptStream = receiptCut is null ? null : await receiptCut.ReadAsync(new ControlCriterionMapping(
                engagement.TenantId, engagement.ProgramId, mapping.ControlId, mapping.EditionId,
                mapping.CriterionIdentifier).Stream, ct).ConfigureAwait(false);
            var capturedVersions = new List<ControlCriterionMappingVersionView>();
            for (var index = 0; index < mapping.Versions.Count; index++)
            {
                var version = mapping.Versions[index];
                if (receiptStream is null ? version.ProposedAt > through : !ProposalIncluded(receiptStream, version))
                    continue;
                if (!TryAsOfVersion(mapping, index, through, out var asOfVersion, receiptStream))
                    return SourceConflict();
                capturedVersions.Add(asOfVersion);

                if (asOfVersion.Status == "proposed")
                    scopeDifferences.Add($"Control mapping {mapping.MappingId} version {version.VersionNumber} was pending on the Type I date.");
                else if (asOfVersion.Status == "rejected")
                    scopeDifferences.Add($"Control mapping {mapping.MappingId} version {version.VersionNumber} was rejected by the Type I date.");
                else if (asOfVersion.Status == "retired")
                    scopeDifferences.Add($"Control mapping {mapping.MappingId} version {version.VersionNumber} was retired by the Type I date.");

                if (asOfVersion.Status != "accepted")
                    continue;
                if (!controlVersions.TryGetValue(mapping.ControlId, out var controlVersionId))
                {
                    blockers.Add($"Accepted control mapping {mapping.MappingId} has no control source in the Type I baseline.");
                    scopeDifferences.Add($"Accepted control mapping {mapping.MappingId} is not linked to a captured control.");
                }
                else if (controlVersionId != asOfVersion.ControlVersionId)
                    scopeDifferences.Add($"Accepted control mapping {mapping.MappingId} version {version.VersionNumber} requires remapping to the control version effective on the Type I date.");
            }

            if (capturedVersions.Count == 0)
                continue;
            var latestVersion = capturedVersions[^1].VersionNumber;
            try
            {
                sources.Add(new TypeIBaselineSourceReference(
                    "control_criterion_mapping_version", mapping.MappingId,
                    Uuid.CreateVersion5(mapping.MappingId,
                        $"control_criterion_mapping_version_v1:{latestVersion}"),
                    latestVersion, Digest(mapping, capturedVersions)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or
                                               InvalidOperationException or NotSupportedException or
                                               SnapshotContentException)
            {
                var difference = $"Control mapping {mapping.MappingId} has invalid canonical source content.";
                blockers.Add(difference);
                scopeDifferences.Add(difference);
            }
        }

        return Success(Family, sources, blockers, scopeDifferences);
    }

    static bool MappingsMatch(IReadOnlyList<ControlCriterionMappingView> left,
        IReadOnlyList<ControlCriterionMappingView> right)
    {
        if (left is null || right is null || left.Count != right.Count)
            return false;
        var byId = new Dictionary<Uuid, ControlCriterionMappingView>();
        foreach (var mapping in left)
            if (mapping is null || !byId.TryAdd(mapping.MappingId, mapping))
                return false;
        foreach (var mapping in right)
            if (mapping is null || !byId.TryGetValue(mapping.MappingId, out var source) ||
                !MappingMatches(source, mapping))
                return false;
        return true;
    }

    static bool MappingMatches(ControlCriterionMappingView left,
        ControlCriterionMappingView right)
    {
        if (left.TenantId != right.TenantId || left.ProgramId != right.ProgramId ||
            left.MappingId != right.MappingId || left.ControlId != right.ControlId ||
            left.EditionId != right.EditionId ||
            left.CriterionIdentifier != right.CriterionIdentifier ||
            left.CriterionKind != right.CriterionKind || left.Revision != right.Revision ||
            left.Status != right.Status || left.ActiveVersionNumber != right.ActiveVersionNumber ||
            left.ActiveControlVersionId != right.ActiveControlVersionId ||
            left.Versions is null || right.Versions is null ||
            left.Versions.Count != right.Versions.Count)
            return false;
        for (var index = 0; index < left.Versions.Count; index++)
            if (left.Versions[index] != right.Versions[index])
                return false;
        return true;
    }

    static bool IsValid(ControlCriterionMappingView mapping,
        ExaminationEngagementView engagement)
    {
        if (mapping.TenantId != engagement.TenantId || mapping.ProgramId != engagement.ProgramId ||
            mapping.MappingId == Uuid.Empty || mapping.ControlId == Uuid.Empty ||
            mapping.EditionId == Uuid.Empty ||
            string.IsNullOrWhiteSpace(mapping.CriterionIdentifier) ||
            string.IsNullOrWhiteSpace(mapping.CriterionKind) || mapping.Revision < 1 ||
            mapping.Versions is null || mapping.Versions.Count == 0 ||
            mapping.MappingId != mapping.Versions[0].MappingId)
            return false;

        var reviewedCount = 0;
        var retiredCount = 0;
        var proposedCount = 0;
        var activeCount = 0;
        var hasRetiredVersion = false;
        var decisionIds = new HashSet<Uuid>();
        for (var index = 0; index < mapping.Versions.Count; index++)
        {
            var version = mapping.Versions[index];
            if (version is null || version.VersionNumber != index + 1 || version.MappingId == Uuid.Empty ||
                version.ControlVersionId == Uuid.Empty || !ValidText(version.Rationale, 4000) ||
                !ValidText(version.ApplicabilityExplanation, 4000) ||
                !ValidActor(version.ProposedBy) || version.ProposedAt == default)
                return false;

            if (version.ReviewDecisionId is { } decisionId)
            {
                if (decisionId == Uuid.Empty || !decisionIds.Add(decisionId) ||
                    version.ReviewedBy is null ||
                    !ValidActor(version.ReviewedBy) || !ValidText(version.ReviewRationale, 4000) ||
                    version.ReviewedAt is not { } reviewedAt || reviewedAt < version.ProposedAt ||
                    version.SeparationOfDutiesWaiverId == Uuid.Empty ||
                    version.ProposedBy == version.ReviewedBy &&
                    version.SeparationOfDutiesWaiverId is null)
                    return false;
                reviewedCount++;
            }
            else if (version.ReviewedBy is not null || version.ReviewRationale is not null ||
                     version.ReviewedAt is not null || version.SeparationOfDutiesWaiverId is not null)
                return false;

            var retiredAt = version.RetiredAt;
            if (retiredAt is { } at)
            {
                if (version.Status != "retired" || version.ReviewDecisionId is null ||
                    version.RetiredBy is null || !ValidActor(version.RetiredBy) ||
                    !ValidText(version.RetirementRationale, 4000) ||
                    version.ReviewedAt is not { } reviewedAt || at < reviewedAt)
                    return false;
                retiredCount++;
                hasRetiredVersion = true;
            }
            else if (version.RetiredBy is not null || version.RetirementRationale is not null)
                return false;

            switch (version.Status)
            {
                case "proposed" when version.ReviewDecisionId is null:
                    proposedCount++;
                    break;
                case "accepted" when version.ReviewDecisionId is not null && retiredAt is null:
                    activeCount++;
                    break;
                case "superseded" when version.ReviewDecisionId is not null && retiredAt is null:
                    break;
                case "rejected" when version.ReviewDecisionId is not null && retiredAt is null:
                    break;
                case "retired" when version.ReviewDecisionId is not null && retiredAt is not null:
                    break;
                default:
                    return false;
            }
        }

        var expectedStatus = proposedCount > 0 ? "pending"
            : mapping.ActiveVersionNumber is not null ? "active"
            : hasRetiredVersion ? "retired"
            : "rejected";
        if (proposedCount > 1 || activeCount > 1 || mapping.Revision !=
                mapping.Versions.Count + reviewedCount + retiredCount || mapping.Status != expectedStatus)
            return false;

        if (mapping.ActiveVersionNumber is { } activeVersion)
        {
            if (activeVersion < 1 || activeVersion > mapping.Versions.Count ||
                mapping.Versions[activeVersion - 1].Status != "accepted" ||
                mapping.ActiveControlVersionId !=
                mapping.Versions[activeVersion - 1].ControlVersionId)
                return false;
        }
        else if (mapping.ActiveControlVersionId is not null)
            return false;
        return true;
    }

    static bool TryAsOfVersion(ControlCriterionMappingView mapping, int index,
        DateTimeOffset through, out ControlCriterionMappingVersionView asOf, RecordedSourceReceiptPrefix? receipts = null)
    {
        var version = mapping.Versions[index];
        var reviewed = receipts is null ? version.ReviewedAt is { } reviewedAt && reviewedAt <= through
            : ReviewIncluded(receipts, version);
        var accepted = reviewed && version.Status is ("accepted" or "superseded" or "retired");
        var rejected = reviewed && version.Status == "rejected";
        var retired = accepted && (receipts is null ? version.RetiredAt is { } retiredAt && retiredAt <= through
            : version.RetiredAt is not null && receipts.Includes(fact => fact is ControlCriterionMappingRetired retiredFact &&
                retiredFact.MappingId == version.MappingId && retiredFact.VersionNumber == version.VersionNumber));
        if (!reviewed)
            asOf = version with
            {
                Status = "proposed",
                ReviewDecisionId = null,
                ReviewedBy = null,
                ReviewRationale = null,
                ReviewedAt = null,
                SeparationOfDutiesWaiverId = null,
                RetiredBy = null,
                RetirementRationale = null,
                RetiredAt = null,
            };
        else if (rejected)
            asOf = version with
            {
                Status = "rejected",
                RetiredBy = null,
                RetirementRationale = null,
                RetiredAt = null,
            };
        else if (!accepted)
        {
            asOf = version;
            return false;
        }
        else
        {
            var hasAcceptedSuccessor = false;
            for (var candidateIndex = index + 1; candidateIndex < mapping.Versions.Count;
                 candidateIndex++)
            {
                var candidate = mapping.Versions[candidateIndex];
                if ((receipts is null ? candidate.ReviewedAt is { } candidateReviewedAt && candidateReviewedAt <= through
                        : ReviewIncluded(receipts, candidate)) &&
                    candidate.Status is ("accepted" or "superseded" or "retired"))
                {
                    hasAcceptedSuccessor = true;
                    break;
                }
            }

            asOf = version with
            {
                Status = retired ? "retired" : hasAcceptedSuccessor ? "superseded" : "accepted",
                RetiredBy = retired ? version.RetiredBy : null,
                RetirementRationale = retired ? version.RetirementRationale : null,
                RetiredAt = retired ? version.RetiredAt : null,
            };
        }
        return true;
    }

    static bool ProposalIncluded(RecordedSourceReceiptPrefix receipts, ControlCriterionMappingVersionView version) =>
        receipts.Includes(fact => fact is ControlCriterionMappingProposed proposed &&
            proposed.MappingId == version.MappingId && proposed.VersionNumber == version.VersionNumber &&
            proposed.ControlVersionId == version.ControlVersionId);

    static bool ReviewIncluded(RecordedSourceReceiptPrefix receipts, ControlCriterionMappingVersionView version) =>
        version.ReviewDecisionId is { } decision && receipts.Includes(fact => fact is ControlCriterionMappingReviewed reviewed &&
            reviewed.MappingId == version.MappingId && reviewed.VersionNumber == version.VersionNumber && reviewed.DecisionId == decision);

    static string Digest(ControlCriterionMappingView mapping,
        IReadOnlyList<ControlCriterionMappingVersionView> versions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, SnapshotContentIdentity.CanonicalWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("control_id", mapping.ControlId.ToString());
            writer.WriteString("criterion_identifier", mapping.CriterionIdentifier);
            writer.WriteString("criterion_kind", mapping.CriterionKind);
            writer.WriteString("edition_id", mapping.EditionId.ToString());
            writer.WriteString("mapping_id", mapping.MappingId.ToString());
            writer.WriteString("program_id", mapping.ProgramId.ToString());
            writer.WriteString("tenant_id", mapping.TenantId.ToString());
            writer.WritePropertyName("versions");
            writer.WriteStartArray();
            foreach (var version in versions)
            {
                var element = JsonSerializer.SerializeToElement(version,
                    ComplianceCoreJsonContext.Default.ControlCriterionMappingVersionView);
                SnapshotContentIdentity.WriteCanonical(writer, element);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        var prefix = Encoding.UTF8.GetBytes(DigestDomain);
        var bytes = stream.ToArray();
        var material = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(material, 0);
        bytes.CopyTo(material, prefix.Length);
        return Convert.ToHexStringLower(SnapshotSourceCapture.HashData(material));
    }

    static bool ValidActor(ActorReference? actor) => actor is not null &&
        actor.Kind == "member" && Uuid.TryParse(actor.Id, null, out var memberId) &&
        memberId != Uuid.Empty && ValidText(actor.Display, 200);

    static bool ValidText(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value == value.Trim();

    static bool IsDigest(string? value) => value is { Length: 64 } && value.All(static character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

    static Result<TypeIBaselineSourceContribution> Success(
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new(
            "control_criterion_mappings", sources, blockers, scopeDifferences));

    static Result<TypeIBaselineSourceContribution> Success(string family,
        IReadOnlyList<TypeIBaselineSourceReference> sources, IReadOnlyList<string> blockers,
        IReadOnlyList<string> scopeDifferences) =>
        Result<TypeIBaselineSourceContribution>.Success(new(family, sources, blockers,
            scopeDifferences));

    static Result<TypeIBaselineSourceContribution> SourceConflict() =>
        Result<TypeIBaselineSourceContribution>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Control mapping source and projection state changed or are inconsistent. Retry the preview.",
            isTransient: true));
}
