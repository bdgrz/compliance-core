using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Queries;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

/// <summary>
///     Joins the immutable catalog edition with the program's projected accepted mappings and
///     not-applicable decisions. Only accepted versions count, and a control counts once per
///     entry, so drafts and duplicates cannot inflate coverage. A mapping to a control version
///     that is no longer current is flagged for remapping. Coverage never states that a
///     criterion is satisfied.
/// </summary>
public sealed class ListCriteriaCoverageHandler(IAggregateReader reader, ControlReader controlSources, ICriteriaCatalog catalog,
    CriteriaCoverageReadConsistency coverage, ICriteriaTextOverlayReader overlays)
    : IRequestHandler<ListCriteriaCoverage, Page<CriterionCoverageView>>
{
    public ValueTask<Result<Page<CriterionCoverageView>>> HandleAsync(
        IRequestContext<ListCriteriaCoverage> context, CancellationToken ct) =>
        ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct));

    async ValueTask<Result<Page<CriterionCoverageView>>> HandleCoreAsync(
        IRequestContext<ListCriteriaCoverage> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CoverageState is not (null or "mapped" or "unmapped" or "not_applicable"))
            return Failure(RequestErrorKind.Validation,
                "The coverage state filter must be mapped, unmapped, or not_applicable.");
        var program = await reader.HydrateAsync(new ComplianceProgram(request.TenantId,
            request.ProgramId), ct).ConfigureAwait(false);
        if (!program.IsCreated)
            return Failure(RequestErrorKind.NotFound, "The program was not found.");
        if ((request.EditionId ?? program.CriteriaEditionId) is not { } editionId)
            return Failure(RequestErrorKind.Conflict,
                "The program has not selected a criteria edition.");
        if (catalog.GetEdition(editionId) is null)
            return Failure(RequestErrorKind.NotFound, "The criteria edition was not found.");
        var projected = await coverage.ReadMappingsAsync(request.TenantId, request.ProgramId, ct)
            .ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(projected.Error);
        var decisions = await coverage.ReadDecisionsAsync(request.TenantId, request.ProgramId,
            ct).ConfigureAwait(false);
        if (!decisions.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(decisions.Error);
        var editionMappings = projected.Value.Where(mapping => mapping.EditionId == editionId)
            .ToArray();
        var current = new Dictionary<Uuid, Uuid?>();
        foreach (var controlId in editionMappings
                     .Where(static mapping => mapping.ActiveVersionNumber is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await controlSources.ReadAsync(request.TenantId, request.ProgramId, controlId, ct).ConfigureAwait(false);
            current[controlId] = control.ApprovedVersion is { Status: "approved" } version
                ? version.VersionId
                : null;
        }
        var mappings = editionMappings.ToLookup(static mapping => mapping.CriterionIdentifier,
            StringComparer.Ordinal);
        var notApplicable = decisions.Value
            .Where(decision => decision.EditionId == editionId &&
                decision.Status == "not_applicable")
            .ToDictionary(static decision => decision.CriterionIdentifier,
                static decision => decision.Versions[decision.ActiveVersionNumber!.Value - 1].DecisionId,
                StringComparer.Ordinal);
        var entries = catalog.ListEntries(editionId, request.Category, request.Kind, null);
        var projectedEntries = await overlays.ApplyPageAsync(request.TenantId, entries, false, ct)
            .ConfigureAwait(false);
        if (!projectedEntries.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(projectedEntries.Error);
        var items = new List<CriterionCoverageView>();
        foreach (var entry in projectedEntries.Value)
        {
            var item = Coverage(entry, mappings[entry.Identifier].ToArray(), current,
                notApplicable.TryGetValue(entry.Identifier, out var decisionId) ? decisionId : null);
            if (request.CoverageState is null || item.CoverageState == request.CoverageState)
                items.Add(item);
        }
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criteria coverage");
    }

    static CriterionCoverageView Coverage(Criterion entry,
        IReadOnlyList<ControlCriterionMappingView> mappings,
        IReadOnlyDictionary<Uuid, Uuid?> currentVersions, Uuid? notApplicableDecisionId)
    {
        var mapped = mappings.Where(static mapping => mapping.ActiveVersionNumber is not null)
            .OrderBy(static mapping => mapping.MappingId.ToString(), StringComparer.Ordinal)
            .GroupBy(static mapping => mapping.ControlId)
            .Select(static group => group.First())
            .Select(mapping => new MappedControlReference(
                mapping.Versions[mapping.ActiveVersionNumber!.Value - 1].MappingId,
                mapping.ControlId, mapping.ActiveControlVersionId!.Value,
                mapping.ActiveVersionNumber.Value,
                mapping.Versions[mapping.ActiveVersionNumber.Value - 1].ApplicabilityExplanation)
            {
                RemapRequired = currentVersions.GetValueOrDefault(mapping.ControlId) !=
                                mapping.ActiveControlVersionId,
            })
            .ToArray();
        var state = notApplicableDecisionId is not null ? "not_applicable"
            : mapped.Length > 0 ? "mapped"
            : "unmapped";
        return new CriterionCoverageView(entry.EditionId, entry.Identifier, entry.Kind,
            entry.Category, entry.ParentIdentifier, entry.Summary, state, mapped,
            mappings.Count(static mapping => mapping.Status == "pending"))
        {
            NotApplicableDecisionId = notApplicableDecisionId,
            LicensedText = entry.LicensedText,
            Overlay = entry.Overlay,
        };
    }

    static Result<Page<CriterionCoverageView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<CriterionCoverageView>>.Failure(new RequestError(kind, message));
}
