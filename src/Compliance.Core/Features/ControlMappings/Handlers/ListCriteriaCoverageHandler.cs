using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.ControlMappings.Models;
using Bdgrz.Compliance.Features.ControlMappings.Queries;
using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Controls.Services;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Criteria.Readers;
using Bdgrz.Compliance.Features.Criteria.Services;
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
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
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
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Cntryl.Fitz;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.ControlMappings.Handlers;

/// <summary>
///     Joins the immutable catalog edition with the program's projected accepted mappings and
///     not-applicable decisions. Only accepted versions count, and a control counts once per
///     entry, so drafts and duplicates cannot inflate coverage. A mapping to a control version
///     that is no longer current is flagged for remapping. Coverage never states that a
///     criterion is satisfied.
/// </summary>
public sealed class ListCriteriaCoverageHandler(IAggregateReader reader, ProgramReader programs, ControlReader controlSources, ICriteriaCatalog catalog,
    CriteriaCoverageReadConsistency coverage, ICriteriaTextOverlayReader overlays,
    IDomainEventReader? events = null, IRuntimeCriteriaCatalogReader? runtime = null,
    ICriteriaEntryAuthority? entryAuthority = null, ICriteriaPublicationAuthority? publications = null)
    : IRequestHandler<ListCriteriaCoverage, Page<CriterionCoverageView>>
{
    public async ValueTask<Result<Page<CriterionCoverageView>>> HandleAsync(
        IRequestContext<ListCriteriaCoverage> context, CancellationToken ct)
    {
        try
        { return await ControlSourceFailures.ClassifyAsync(() => HandleCoreAsync(context, ct)).ConfigureAwait(false); }
        catch (CriteriaSourceUnavailableException) { return PendingCoverage(); }
        catch (FitzException) { return PendingCoverage(); }
        catch (IOException) { return PendingCoverage(); }
        catch (TimeoutException) { return PendingCoverage(); }
    }

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
        CriteriaPublicationProof? publication;
        var current = program.CriteriaEditionId == editionId;
        if (current)
        {
            publication = program.CurrentSelection is { } selected
                ? selected.CriteriaPublication
                : program.InitialCriteriaPublication;
        }
        else
        {
            var history = await programs.HistoryAsync(request.TenantId, request.ProgramId, ct)
                .ConfigureAwait(false);
            var selections = history.Facts.OfType<ProgramCriteriaEditionSelected>()
                .Where(fact => fact.ChangeCount == 1 && fact.EditionId == editionId).ToArray();
            var created = history.Facts.OfType<ProgramCreated>()
                .FirstOrDefault(fact => fact.CriteriaEditionId == editionId);
            if (selections.Length == 0 && created is null)
                return PendingCoverage();
            publication = selections.LastOrDefault() is { } selection ? selection.CriteriaPublication : created?.CriteriaPublication;
        }
        if (publication is not null)
            return await ReadRuntimeAsync(request, editionId, publication, ct).ConfigureAwait(false);
        if (catalog.GetEdition(editionId) is null)
            return PendingCoverage();
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
        var currentVersions = new Dictionary<Uuid, Uuid?>();
        foreach (var controlId in editionMappings
                     .Where(static mapping => mapping.ActiveVersionNumber is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await controlSources.ReadAsync(request.TenantId, request.ProgramId, controlId, ct).ConfigureAwait(false);
            currentVersions[controlId] = control.ApprovedVersion is { Status: "approved" } version
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
            var item = Coverage(entry, mappings[entry.Identifier].ToArray(), currentVersions,
                notApplicable.TryGetValue(entry.Identifier, out var decisionId) ? decisionId : null);
            if (request.CoverageState is null || item.CoverageState == request.CoverageState)
                items.Add(item);
        }
        return ControlActivationSource.Paginate(items, request.Limit, request.Cursor,
            "criteria coverage");
    }

    async ValueTask<Result<Page<CriterionCoverageView>>> ReadRuntimeAsync(ListCriteriaCoverage request, Uuid editionId,
        CriteriaPublicationProof publication, CancellationToken ct)
    {
        if (request.Limit is < 1 or > 200)
            return Failure(RequestErrorKind.Validation, "The coverage page limit must be between 1 and 200.");
        if (events is null || runtime is null || entryAuthority is null || publications is null ||
            publication.EditionId != editionId || publication.EventId == Uuid.Empty)
            return PendingCoverage();
        var scope = $"{request.TenantId}\n{request.ProgramId}\n{editionId}\n{request.Category ?? ""}\n{request.Kind ?? ""}\n{request.CoverageState ?? ""}";
        var prefix = "coverage-runtime-v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope))) + ":";
        string? sourceCursor = null;
        if (request.Cursor is { } cursor)
        {
            if (cursor.Length > 4096 || !cursor.StartsWith(prefix, StringComparison.Ordinal) || cursor.Length == prefix.Length)
                return Failure(RequestErrorKind.Validation, "The coverage cursor is invalid for this request.");
            sourceCursor = cursor[prefix.Length..];
        }
        var pageResult = await runtime.ListEntriesAsync(request.TenantId, editionId, request.Category, request.Kind,
            null, request.Limit, sourceCursor, ct).ConfigureAwait(false);
        if (!pageResult.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(pageResult.Error!);
        var page = pageResult.Value!;
        if (page.EditionId != editionId || page.PublicationEventId != publication.EventId || page.Coverage != "complete" ||
            page.Items.Count > (request.Limit ?? 50) ||
            !await new RuntimeCriteriaCoverageSource(events, runtime, entryAuthority, publications)
                .CoversAsync(editionId, publication, ct).ConfigureAwait(false))
            return PendingCoverage();

        var entries = new List<Criterion>(page.Items.Count);
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in page.Items)
        {
            if (!CriteriaCatalogEntriesPage.TryConvert(item, editionId, out var criterion) || !identifiers.Add(item.Identifier))
                return PendingCoverage();
            var proof = await CriteriaEntryAuthorityGuard.RequireProofAsync(entryAuthority, editionId, item.Identifier, ct).ConfigureAwait(false);
            if (!proof.IsSuccess || !RuntimeCriteriaCoverageSource.Matches(item, proof.Value!, publication.EventId))
                return PendingCoverage();
            entries.Add(criterion);
        }
        // The source scan and individual proofs can span multiple projection reads.
        // Confirm that the page and its continuation still describe the same snapshot.
        var rechecked = await runtime.ListEntriesAsync(request.TenantId, editionId, request.Category, request.Kind,
            null, request.Limit, sourceCursor, ct).ConfigureAwait(false);
        if (!rechecked.IsSuccess || rechecked.Value is not { } same || same.EditionId != page.EditionId ||
            same.PublicationEventId != page.PublicationEventId || same.Generation != page.Generation ||
            same.Coverage != page.Coverage || same.NextCursor != page.NextCursor || !same.Items.SequenceEqual(page.Items))
            return PendingCoverage();
        var mappingResult = await coverage.ReadMappingsForAsync(request.TenantId, request.ProgramId, editionId, identifiers, ct).ConfigureAwait(false);
        if (!mappingResult.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(mappingResult.Error!);
        var decisionsResult = await coverage.ReadDecisionsForAsync(request.TenantId, request.ProgramId, editionId, identifiers, ct).ConfigureAwait(false);
        if (!decisionsResult.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(decisionsResult.Error!);
        var versions = new Dictionary<Uuid, Uuid?>();
        foreach (var controlId in mappingResult.Value.Where(static mapping => mapping.ActiveVersionNumber is not null)
                     .Select(static mapping => mapping.ControlId).Distinct())
        {
            var control = await controlSources.ReadAsync(request.TenantId, request.ProgramId, controlId, ct).ConfigureAwait(false);
            versions[controlId] = control.ApprovedVersion is { Status: "approved" } version ? version.VersionId : null;
        }
        var mappings = mappingResult.Value.ToLookup(static mapping => mapping.CriterionIdentifier, StringComparer.Ordinal);
        var notApplicable = decisionsResult.Value.Where(static decision => decision.Status == "not_applicable")
            .ToDictionary(static decision => decision.CriterionIdentifier,
                static decision => decision.Versions[decision.ActiveVersionNumber!.Value - 1].DecisionId, StringComparer.Ordinal);
        var projected = await overlays.ApplyPageAsync(request.TenantId, entries, false, ct).ConfigureAwait(false);
        if (!projected.IsSuccess)
            return Result<Page<CriterionCoverageView>>.Failure(projected.Error!);
        var items = new List<CriterionCoverageView>(projected.Value.Count);
        foreach (var entry in projected.Value)
        {
            var item = Coverage(entry, mappings[entry.Identifier].ToArray(), versions,
                notApplicable.TryGetValue(entry.Identifier, out var decision) ? decision : null);
            if (request.CoverageState is null || item.CoverageState == request.CoverageState)
                items.Add(item);
        }
        return Result<Page<CriterionCoverageView>>.Success(new Page<CriterionCoverageView>(items,
            page.NextCursor is null ? null : prefix + page.NextCursor));
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

    static Result<Page<CriterionCoverageView>> PendingCoverage() =>
        Result<Page<CriterionCoverageView>>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Coverage for this criteria edition is not available yet.", isTransient: true));
}
