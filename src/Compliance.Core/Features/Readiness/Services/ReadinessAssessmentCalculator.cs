using System.Globalization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Projections;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Services;

/// <summary>Reads source owners once, freezes the selected inputs, and evaluates the released product rules.</summary>
public sealed class ReadinessAssessmentCalculator(ProgramReader programs, ControlReader controls,
    ControlMappingReader mappings, ApplicabilityReader applicability, ICriteriaCatalog catalog,
    IReadinessSourceReader sources, IImmutableContentStore content, IEnumerable<IReadinessComputedGapSource> computedSources)
{
    public async ValueTask<Result<ReadinessAssessmentResult>> CalculateAsync(RecordReadinessAssessment request,
        ActorReference actor, DateTimeOffset now, CancellationToken ct = default)
    {
        var asOf = request.AsOf ?? now;
        if (asOf == default || asOf > now)
            return Result<ReadinessAssessmentResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "A readiness assessment cannot use an unknown or future as-of time."));
        var program = await programs.HistoryAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        if (!program.Owner.IsCreated)
            return Result<ReadinessAssessmentResult>.Failure(new RequestError(RequestErrorKind.NotFound, "The program was not found."));
        var sourceResult = await sources.ReadAsync(request.TenantId, request.ProgramId, asOf, ct).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
            return Result<ReadinessAssessmentResult>.Failure(sourceResult.Error);
        var sourceSet = sourceResult.Value;
        var selectedEdition = program.CriteriaSelectionAt(asOf)?.EditionId;
        var edition = selectedEdition is { } editionId ? catalog.GetEdition(editionId) : null;
        var applicabilitySnapshot = await applicability.ReadProgramAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        var categories = sourceSet.Boundaries.Select(boundary => ReadinessRules.BoundaryVersionAt(boundary,
                asOf, DateOnly.FromDateTime(asOf.UtcDateTime)))
            .Where(static version => version is not null)
            .SelectMany(static version => version!.Content.TrustServicesCategories).ToHashSet(StringComparer.Ordinal);
        var excluded = applicabilitySnapshot.Decisions.Where(decision => decision.EditionId == selectedEdition &&
                decision.Versions.Any(version => version.ReviewedAt is { } reviewed && reviewed <= asOf &&
                    version.Status is "accepted" or "superseded" or "withdrawn" &&
                    (version.WithdrawnAt is null || version.WithdrawnAt > asOf)))
            .Select(static decision => decision.CriterionIdentifier).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<Criterion> criteria = edition is null ? [] : catalog.ListEntries(edition.EditionId, null, "criterion", null)
            .Where(criterion => categories.Contains(criterion.Category) && !excluded.Contains(criterion.Identifier)).ToArray();
        var mappingSnapshot = await mappings.ReadProgramAsync(request.TenantId, request.ProgramId, ct).ConfigureAwait(false);
        var selectedMappings = mappingSnapshot.Mappings.Where(mapping => mapping.EditionId == selectedEdition &&
            criteria.Any(criterion => criterion.Identifier == mapping.CriterionIdentifier)).ToArray();
        var effective = new List<ReadinessEffectiveControl>();
        foreach (var controlId in selectedMappings.Where(mapping => ReadinessRules.ActiveAt(mapping, asOf) is not null)
            .Select(static mapping => mapping.ControlId).Distinct())
        {
            var source = await controls.ReadAsync(request.TenantId, request.ProgramId, controlId, ct).ConfigureAwait(false);
            effective.Add(new ReadinessEffectiveControl(controlId, source.IsVisible && source.ProgramId == request.ProgramId
                ? ReadinessControlSelection.EffectiveVersionAt(source, asOf) : null));
        }
        var evaluation = ReadinessRules.Evaluate(request.ProgramId, asOf, edition?.EditionId, criteria,
            selectedMappings, effective.ToDictionary(static item => item.ControlId, static item => item.Version), sourceSet, edition);
        var contributed = new List<ReadinessComputedGapSet>();
        foreach (var source in computedSources.OrderBy(static source => source.Family, StringComparer.Ordinal))
        {
            var contribution = await source.ReadAsync(request.TenantId, request.ProgramId, asOf, ct).ConfigureAwait(false);
            if (!contribution.IsSuccess)
                return Result<ReadinessAssessmentResult>.Failure(contribution.Error);
            if (contribution.Value.Family != source.Family)
                return Result<ReadinessAssessmentResult>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A computed-gap source answered for another family."));
            contributed.Add(contribution.Value);
        }
        // Every specified family needs a real answer; a missing adapter is explicit unknown input.
        string[] requiredFamilies = ["platform_access", "application_inventory", "technology_and_information_inventory",
            "control_environment", "policies", "system_description", "assurance_work", "external_access_governance",
            "change_and_incident_oversight", "risk_and_provider_oversight"];
        var absent = requiredFamilies.Where(family => contributed.All(source => source.Family != family)).ToArray();
        evaluation = evaluation with
        {
            Inputs = evaluation.Inputs.Concat(absent.Select(static family => new ReadinessInputView(family, "unknown", 0,
                "The owning context has not supplied its complete computed-gap source."))).ToArray(),
        };
        evaluation = ReadinessComputedGapComposition.Compose(request.ProgramId, evaluation, contributed);
        var realm = request.TenantId;
        var snapshot = await ReadinessContent.WriteAsync(content, realm, ReadinessInputSnapshot.Schema,
            new ReadinessInputSnapshot(request.ProgramId, asOf, edition?.EditionId,
                edition is null ? null : await ReadinessContent.WriteAsync(content, realm, "readiness-input-snapshot.catalog-edition.v1", edition, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync<IReadOnlyList<Criterion>>(content, realm, "readiness-input-snapshot.criteria.v1", criteria, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync<IReadOnlyList<ControlCriterionMappingView>>(content, realm, "readiness-input-snapshot.mappings.v1", selectedMappings, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync<IReadOnlyList<ReadinessEffectiveControl>>(content, realm, "readiness-input-snapshot.effective-controls.v1", effective, ct).ConfigureAwait(false),
                await ReadinessContent.WriteAsync(content, realm, "readiness-input-snapshot.sources.v1", sourceSet, ct).ConfigureAwait(false),
                await ImmutableSelectionManifests.WriteAsync<AuthoritativeReadCut>(content, realm.ToString(), Uuid.CreateVersion4(),
                    "authoritative-read-cut.v1", [], SelectionOrder.Authored, ct).ConfigureAwait(false)), ct).ConfigureAwait(false);
        // The complete source snapshots preserve their exact version histories; references index the selected rule dependencies.
        var references = evaluation.Findings.SelectMany(static finding => finding.Sources)
            .Concat(evaluation.Gaps.SelectMany(static gap => gap.Sources))
            .Concat(contributed.SelectMany(static source => source.SourceRecordVersions))
            .Append(new ReadinessSourceReference("program", request.ProgramId, program.Owner.Revision.ToString(CultureInfo.InvariantCulture)))
            .Append(new ReadinessSourceReference("frozen_input_snapshot", snapshot.ContentId, snapshot.Sha256))
            .Distinct().OrderBy(static reference => reference.Kind, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Id.ToString(), StringComparer.Ordinal).ThenBy(static reference => reference.Version, StringComparer.Ordinal).ToArray();
        return Result<ReadinessAssessmentResult>.Success(new ReadinessAssessmentResult(request.TenantId, request.ProgramId,
            request.AssessmentId, ReadinessRules.Version, asOf, evaluation.InputFingerprint, snapshot, references,
            evaluation.Completeness, evaluation.Inputs, evaluation.Findings, evaluation.Gaps, actor, now, request.ScheduledDay)
        { ComputedInputs = contributed.ToArray() });
    }
}
