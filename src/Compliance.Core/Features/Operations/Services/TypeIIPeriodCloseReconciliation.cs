using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>
/// Derives expected/actual/missing work from native generations and exact materialized occurrence terms. Publishes
/// all required effective source families plus complete operating/change/incident histories before one native close
/// proposal. Receipt time and effective interval remain distinct; missing or corrupt source bytes never become ready.
/// </summary>
public sealed class TypeIIPeriodCloseReconciliation(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content,
    ControlOperationsReader operating, FindingReader findings, PublishedTypeIBaselinePreviewReader sourceFamilies,
    TypeIIPeriodCloseHistory history, TypeIIPeriodCloseSourcePublication publication, RecordedSourceReceiptCutFactory receiptCuts)
    : ITypeIIPeriodCloseReconciliation
{
    public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriodReadSnapshot period,
        ClaimsPrincipal actor, Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
    {
        if (period.View is not { } current || operation.Owner != period.Owner.Stream || operation.AcceptedAt == default ||
            operation.RootOperationId == Uuid.Empty || requesterUserId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "Close capture requires its exact opened period and server decision.");
        try
        {
            var operatingReceipts = receiptCuts.Create(current.TenantId, operation.AcceptedAt);
            var engagement = await new ExaminationEngagementReader(reader, events, content).ReadAsync(current.TenantId,
                current.ProgramId, current.ExaminationId, ct).ConfigureAwait(false);
            if (engagement.View is not { } terms || terms.TenantId != current.TenantId || terms.ProgramId != current.ProgramId)
                return Failure(RequestErrorKind.NotFound, "The period examination engagement was not found.");
            // Family readers still select versions effective on this date. History readers independently honor the receipt cutoff.
            var read = await sourceFamilies.ReadClosingAsync(terms with { PeriodStart = current.PeriodEnd, PeriodEnd = null },
                requesterUserId, operation.AcceptedAt, ct).ConfigureAwait(false);
            if (!read.IsSuccess)
                return Result<TypeIIPeriodCloseValidationContent>.Failure(read.Error!);
            if (read.Value.SourceContentManifest is not { } sourceManifest)
                throw new ImmutableContentIntegrityException("The close source families have no complete physical publication proof.");
            var closingSources = await publication.ReadAsync(current, sourceManifest, ct).ConfigureAwait(false);
            if (closingSources.RecordedThrough != operation.AcceptedAt)
                throw new ImmutableContentIntegrityException("The source publication differs from the frozen close receipt cutoff.");
            var published = closingSources.Sources.ToList();
            var references = read.Value.Sources.Select(source => new ExaminationContentReference(source.Kind, source.RecordId,
                source.VersionId, source.Revision, source.ContentSha256)).ToList();
            var blockers = read.Value.Blockers.ToList();
            if (!TypeIBaselineSourceFamilies.Required.All(family => read.Value.SourceCoverage.Count(row => row.Family == family) == 1))
                blockers.Add("The close source proof does not cover every required family exactly once.");
            // Opening facts remain part of the examination lineage, including its exact approved system-description chain.
            if (current.OpeningSourceContentManifest is { } opening)
            {
                var openingSources = await publication.ReadAsync(current, opening, ct).ConfigureAwait(false);
                published.AddRange(openingSources.Sources);
                references.AddRange(current.OpeningSourceReferences);
            }
            else if (current.OpeningSourceReferences.Count != 0)
                blockers.Add("The opening's complete physical source publication proof is unavailable.");

            var completeOperating = await operating.ReadAsync(current.TenantId, current.ProgramId, ct).ConfigureAwait(false);
            var expected = current.ExpectedOccurrences.ToDictionary(item => item.OccurrenceId);
            var matches = new Dictionary<Uuid, Dictionary<Uuid, ControlOccurrenceCreationIntent>>();
            var native = new List<TypeIIPeriodCloseNativeSource>();
            foreach (var fact in period.OccurrenceGenerations)
            {
                native.Add(await history.CaptureOperatingAsync(period.Owner.Stream, fact, ct).ConfigureAwait(false));
                var manifest = await ControlOperatingContent.ReadAsync<ControlOccurrenceSelectionManifest>(content,
                    current.TenantId, fact.CreationIntentManifest, ct).ConfigureAwait(false);
                foreach (var selected in manifest.Selections)
                {
                    if (!expected.TryGetValue(selected.ExpectedOccurrenceId, out var requirement))
                        throw new ImmutableContentIntegrityException("A close generation selects an occurrence outside the frozen expected population.");
                    var creation = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content,
                        current.TenantId, selected.Intent, ct).ConfigureAwait(false);
                    if (creation.TenantId != current.TenantId || creation.ProgramId != current.ProgramId ||
                        creation.ControlId != requirement.ControlId || creation.OccurrenceId != selected.OccurrenceId ||
                        creation.SelectedAt > operation.AcceptedAt)
                        throw new ImmutableContentIntegrityException("A selected occurrence differs from its exact period population or decision cutoff.");
                    if (!matches.TryGetValue(selected.ExpectedOccurrenceId, out var actual))
                        matches.Add(selected.ExpectedOccurrenceId, actual = []);
                    if (actual.TryGetValue(selected.OccurrenceId, out var earlier) && !earlier.SameTermsAs(creation))
                        throw new ImmutableContentIntegrityException("Two native selections disagree about one occurrence's frozen terms.");
                    actual[selected.OccurrenceId] = creation;
                }
            }
            // The approved plan fixes one expected event/ad-hoc slot. Its exact native business windows are the population;
            // no matching native window is missing, and multiple distinct windows are a duplicate of that declared slot.
            foreach (var requirement in current.ExpectedOccurrences.Where(item => item.DueOn is null))
            {
                foreach (var view in completeOperating.ReadRecordedOccurrences(requirement.ControlId).Where(item =>
                             item.ControlVersionId == requirement.ControlVersionId && item.Kind == requirement.Cadence &&
                             item.PeriodStart is { } from && from >= current.PeriodStart && from <= current.PeriodEnd &&
                             (item.PeriodEnd is null || item.PeriodEnd <= current.PeriodEnd)))
                {
                    var (owner, _) = await operating.ReadStateAsync(current.TenantId, current.ProgramId, requirement.ControlId,
                        view.OccurrenceId, Uuid.Empty, ct).ConfigureAwait(false);
                    if (owner.Opened is not { } opened)
                        continue;
                    var creation = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content,
                        current.TenantId, opened.CreationIntent, ct).ConfigureAwait(false);
                    if (creation.TenantId != current.TenantId || creation.ProgramId != current.ProgramId ||
                        creation.ControlId != requirement.ControlId || creation.OccurrenceId != view.OccurrenceId ||
                        creation.SelectedAt > operation.AcceptedAt)
                        throw new ImmutableContentIntegrityException("The event-window occurrence differs from its exact period source scope.");
                    if (!matches.TryGetValue(requirement.OccurrenceId, out var actual))
                        matches.Add(requirement.OccurrenceId, actual = []);
                    actual[view.OccurrenceId] = creation;
                }
            }
            var sourceMatches = new List<TypeIIPeriodCloseOccurrenceMatch>();
            var actualExpected = new List<Uuid>();
            var duplicates = new List<Uuid>();
            var missing = new HashSet<Uuid>();
            var unresolved = new HashSet<Uuid>();
            var capturedOccurrences = new HashSet<Uuid>();
            foreach (var requirement in current.ExpectedOccurrences)
            {
                if (!matches.TryGetValue(requirement.OccurrenceId, out var actual))
                {
                    missing.Add(requirement.OccurrenceId);
                    continue;
                }
                if (actual.Count > 1)
                    duplicates.Add(requirement.OccurrenceId);
                var materialized = false;
                foreach (var (id, creation) in actual)
                {
                    var (owner, view) = await operating.ReadStateAsync(current.TenantId, current.ProgramId, requirement.ControlId,
                        id, Uuid.Empty, ct).ConfigureAwait(false);
                    if (owner.Opened is not { } opened || view is null)
                    {
                        missing.Add(requirement.OccurrenceId);
                        continue;
                    }
                    var original = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content,
                        current.TenantId, opened.CreationIntent, ct).ConfigureAwait(false);
                    if (!original.SameTermsAs(creation) || opened.ControlVersionId != requirement.ControlVersionId ||
                        requirement.DueOn is { } expectedDue && opened.DueOn != expectedDue)
                        throw new ImmutableContentIntegrityException("The materialized occurrence does not match the selected exact content and approved control version.");
                    var ownerFacts = await ControlOccurrenceHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
                    await RequireOperatingReceiptCutAsync(operatingReceipts, owner.Stream, ownerFacts, ct).ConfigureAwait(false);
                    materialized = true;
                    sourceMatches.Add(new TypeIIPeriodCloseOccurrenceMatch(requirement.OccurrenceId,
                        NativeFactReference.Of(owner.Stream, opened), opened.CreationIntent));
                    if (view.State is not (ControlOperationStates.Approved or ControlOperationStates.ActionRequested or ControlOperationStates.Deferred))
                        missing.Add(requirement.OccurrenceId);
                    if (view.State is ControlOperationStates.ActionRequested or ControlOperationStates.Deferred)
                        unresolved.Add(id);
                    if (capturedOccurrences.Add(id))
                        foreach (var fact in ownerFacts)
                            native.Add(await history.CaptureOperatingAsync(owner.Stream, fact, ct).ConfigureAwait(false));
                }
                if (materialized)
                    actualExpected.Add(requirement.OccurrenceId);
            }
            // Capture every recorded applicable operating window, including non-recurring work; only exact mapped slots satisfy expected work.
            foreach (var controlId in completeOperating.PlannedControlIds.OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                var line = await reader.HydrateAsync(new ControlOperatingPlan(current.TenantId, current.ProgramId, controlId), ct).ConfigureAwait(false);
                var lineFacts = await ControlOperatingPlanHistory.ReadAsync(events, line, ct).ConfigureAwait(false);
                await RequireOperatingReceiptCutAsync(operatingReceipts, line.Stream, lineFacts, ct).ConfigureAwait(false);
                foreach (var fact in lineFacts)
                    native.Add(await history.CaptureOperatingAsync(line.Stream, fact, ct).ConfigureAwait(false));
                foreach (var view in completeOperating.ReadRecordedOccurrences(controlId).Where(item =>
                             item.PeriodStart is { } from && from <= current.PeriodEnd && (item.PeriodEnd is null || item.PeriodEnd >= current.PeriodStart)))
                {
                    if (!capturedOccurrences.Add(view.OccurrenceId))
                        continue;
                    var (owner, _) = await operating.ReadStateAsync(current.TenantId, current.ProgramId, controlId, view.OccurrenceId,
                        Uuid.Empty, ct).ConfigureAwait(false);
                    var ownerFacts = await ControlOccurrenceHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
                    await RequireOperatingReceiptCutAsync(operatingReceipts, owner.Stream, ownerFacts, ct).ConfigureAwait(false);
                    foreach (var fact in ownerFacts)
                        native.Add(await history.CaptureOperatingAsync(owner.Stream, fact, ct).ConfigureAwait(false));
                    if (view.State is ControlOperationStates.ActionRequested or ControlOperationStates.Deferred)
                        unresolved.Add(view.OccurrenceId);
                }
            }
            var findingRead = await findings.ReadAsync(current.TenantId, current.ProgramId, operation.AcceptedAt, ct).ConfigureAwait(false);
            if (!findingRead.IsSuccess)
                return Result<TypeIIPeriodCloseValidationContent>.Failure(findingRead.Error!);
            foreach (var finding in findingRead.Value.Findings.Where(item => item.Status != FindingStates.Closed))
                unresolved.Add(finding.FindingId);
            var other = await history.ReadAsync(current, actor, operation.AcceptedAt, ct).ConfigureAwait(false);
            unresolved.UnionWith(other.Unresolved);
            blockers.AddRange(other.Blockers);
            native.AddRange(other.Records);
            var source = await publication.PublishAsync(current, operation, "type_ii_native_history",
                native.DistinctBy(item => item.Fact).OrderBy(item => item.Fact.Stream.ToString(), StreamKeyOrder.Instance)
                    .ThenBy(item => item.Fact.AggregateVersion).ToArray(), sourceMatches.Distinct().OrderBy(item => item.ExpectedOccurrenceId.ToString(),
                    StringComparer.Ordinal).ThenBy(item => item.Opened.Stream.ToString(), StreamKeyOrder.Instance).ToArray(), ct).ConfigureAwait(false);
            references.Add(source.Reference);
            published.Add(source.Published);
            var canonicalReferences = references.Distinct().OrderBy(item => item.SourceType, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ThenBy(item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Version).ThenBy(item => item.ContentSha256, StringComparer.Ordinal).ToArray();
            var publicationEntries = published.Distinct().OrderBy(item => item.SourceType, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId.ToString(), StringComparer.Ordinal).ThenBy(item => item.VersionId.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.Version).ThenBy(item => item.LogicalSha256, StringComparer.Ordinal).ToArray();
            foreach (var reference in canonicalReferences)
                if (!publicationEntries.Any(entry => entry.SourceType == reference.SourceType && entry.RecordId == reference.RecordId &&
                        entry.VersionId == reference.VersionId && entry.Version == reference.Version && entry.LogicalSha256 == reference.ContentSha256))
                    blockers.Add("A selected close source has no exact complete byte publication proof.");
            var sealedPublication = await publication.SealAsync(current, operation, publicationEntries, ct, read.Value.SourceCoverage).ConfigureAwait(false);
            return Result<TypeIIPeriodCloseValidationContent>.Success(new TypeIIPeriodCloseValidationContent(canonicalReferences,
                current.ExpectedOccurrences.Select(item => item.OccurrenceId).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(),
                actualExpected.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), duplicates.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(),
                missing.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray(), unresolved.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray())
            {
                SourceContentManifest = sealedPublication,
                RecordedThrough = operation.AcceptedAt,
                SourceBlockers = blockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                SourceDifferences = read.Value.ScopeDifferences.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            });
        }
        catch (CloseSourceAccessException)
        {
            return Failure(RequestErrorKind.Forbidden, "The actor cannot capture every required restricted close source.");
        }
        catch (ImmutableContentUnavailableException)
        {
            return Failure(RequestErrorKind.Conflict, "Exact close source bytes are not available yet.", true);
        }
        catch (ImmutableContentIntegrityException)
        {
            return Failure(RequestErrorKind.Conflict, "Exact close source content cannot be verified.");
        }
    }

    static async Task RequireOperatingReceiptCutAsync(RecordedSourceReceiptCut cut, EventStreamAddress stream,
        IReadOnlyList<DomainEvent> facts, CancellationToken ct)
    {
        var prefix = await cut.ReadAsync(stream, ct).ConfigureAwait(false);
        if (facts.Any(fact => !prefix.Includes(fact)) || prefix.Included.Count() != facts.Count)
            throw new ImmutableContentUnavailableException("The exact operating native source is not a complete committed prefix at the frozen close receipt cutoff.");
    }

    static Result<TypeIIPeriodCloseValidationContent> Failure(RequestErrorKind kind, string message, bool transient = false) =>
        Result<TypeIIPeriodCloseValidationContent>.Failure(new RequestError(kind, message, transient));
}
