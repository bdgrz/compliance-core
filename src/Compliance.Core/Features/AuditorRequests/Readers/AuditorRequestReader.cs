using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Readers;

/// <summary>Composes full request, independent response and frozen selection views from verified native histories.</summary>
public sealed class AuditorRequestReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content,
    TimeProvider? clock = null)
{
    public async ValueTask<AuditorRequestReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid request,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new AuditorRequest(tenant, program, request), ct).ConfigureAwait(false);
        var history = await AuditorRequestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (!owner.IsRecorded)
            return new(owner, history, null);
        var terms = new List<AuditorRequestTermsVersion>();
        var comments = new List<AuditorRequestCommentView>();
        var selections = new List<AuditorSampleSelectionView>();
        var transitions = new List<AuditorRequestHistoryEntry>();
        AuditorRequestTermsContent? current = null;
        Uuid? examination = null;
        foreach (var fact in history.Facts)
        {
            var native = (INativeOperationFact)fact;
            var operation = native.Operation;
            var actorId = Uuid.Parse(operation.Actor.Id, CultureInfo.InvariantCulture);
            var display = operation.Actor.Display ?? string.Empty;
            var detail = string.Empty;
            var reference = fact switch
            {
                AuditorRequestRecorded recorded => recorded.Change,
                AuditorRequestRevised revised => revised.Change,
                AuditorRequestAssigned assigned => assigned.Change,
                _ => null,
            };
            if (reference is not null)
            {
                current = await AuditorContent.ReadAsync<AuditorRequestTermsContent>(content, tenant, reference, ct)
                    .ConfigureAwait(false);
                if (current.ProgramId != program)
                {
                    if (terms.Count == 0)
                        return new(owner, history, null);
                    throw Invalid("The request terms changed their immutable program binding.");
                }
                if (current.ExaminationId == Uuid.Empty || examination is { } originalExamination && current.ExaminationId != originalExamination || current.ActorMemberId != actorId ||
                    current.At != operation.AcceptedAt)
                    throw Invalid("The request terms differ from native attribution or examination.");
                examination ??= current.ExaminationId;
                terms.Add(new(terms.Count + 1, current.Wording, current.Scope, current.Priority, current.OwnerMemberId,
                    current.DueOn, current.Reason, current.ActorMemberId, current.ActorDisplay, current.At));
                detail = current.Reason;
            }
            if (fact is AuditorCommentAdmitted comment)
            {
                var admission = await AuditorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, comment.Intent, ct)
                    .ConfigureAwait(false);
                VerifyAdmission(owner, comment, admission, AdmittedChildKind.AuditorComment, comment.Generation);
                var authored = await AuditorContent.ReadAsync<AuditorCommentContent>(content, tenant,
                    admission.CanonicalAuthoredContent, ct).ConfigureAwait(false);
                if (authored.ProgramId != program || authored.RequestId != request ||
                    authored.CommentId != operation.LogicalId || authored.ActorMemberId != actorId ||
                    authored.At != operation.AcceptedAt)
                    throw Invalid("The comment body differs from its exact admitted scope or attribution.");
                comments.Add(new(authored.CommentId, authored.Body, authored.Compartment, authored.ActorMemberId,
                    authored.ActorDisplay, authored.At));
            }
            if (fact is AuditorSampleSelectionAdmitted selection)
            {
                var selectionView = await SelectionAsync(owner, selection, program, ct).ConfigureAwait(false);
                selections.Add(new(selectionView.SampleSelectionId, selectionView.PopulationSnapshotId, selectionView.PopulationContentSha256,
                    selectionView.SelectionMethod, selectionView.SelectedRowIds, selectionView.SelectionContentSha256, actorId, display,
                    selectionView.UpdatedAt, selectionView.PopulationVersion, selectionView.Source, selectionView.ExternalReference,
                    selectionView.SupersedesSelectionId, selectionView.AmendmentReason));
            }
            if (fact is AuditorRequestReopened reopened)
            {
                var reason = await AuditorContent.ReadAsync<AuditorRequestReopenContent>(content, tenant, reopened.Change, ct)
                    .ConfigureAwait(false);
                if (reason.ActorMemberId != actorId || reason.At != operation.AcceptedAt)
                    throw Invalid("The reopen reason differs from its native attribution.");
                detail = reason.Reason;
            }
            if (fact is AuditorRequestResolutionAccepted accepted)
            {
                var intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, accepted.Intent, ct)
                    .ConfigureAwait(false);
                var decision = await AuditorContent.ReadAsync<AuditorResolutionDecision>(content, tenant,
                    intent.AuthoredDecision, ct).ConfigureAwait(false);
                if (decision.ProgramId != program || intent.ParentId != request ||
                    intent.Header.FirstOwner != owner.Stream || intent.Header.RootOperationId != operation.RootOperationId ||
                    decision.DecisionId != operation.RootOperationId)
                    throw Invalid("The resolution history differs from its frozen first owner or program.");
                detail = decision.Resolution;
            }
            transitions.Add(new((long)fact.Metadata.AggregateVersion, fact.GetType().Name, detail, actorId, display, operation.AcceptedAt));
        }
        if (current is null)
            throw Invalid("A recorded request has no verified terms.");
        var responses = await ResponseHistoriesAsync(tenant, program, request, history, ct).ConfigureAwait(false);
        var status = WorkStatus(owner, responses, current.DueOn, DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime));
        var latest = (INativeOperationFact)owner.Latest!;
        var view = new AuditorRequestView(tenant, program, request, current.ExaminationId, current.Source,
            current.ExternalReference, current.Wording, current.Scope, current.Priority, current.OwnerMemberId,
            current.DueOn, current.RequiresInternalReview, status, owner.Revision, latest.Operation.AcceptedAt,
            Uuid.Parse(latest.Operation.Actor.Id, CultureInfo.InvariantCulture), latest.Operation.Actor.Display ?? string.Empty,
            terms.AsReadOnly(), comments.AsReadOnly(), responses, selections.AsReadOnly(), transitions.AsReadOnly());
        return new(owner, history, view);
    }

    public async ValueTask<IReadOnlyList<AuditorRequestResponseView>> ReadResponseViewsAsync(Uuid tenant, Uuid program,
        Uuid request, CancellationToken ct = default)
    {
        var snapshot = await ReadAsync(tenant, program, request, ct).ConfigureAwait(false);
        if (snapshot.View is null)
            return [];
        return snapshot.View.Responses.Select(response => (Response: response,
                Version: response.Versions.SingleOrDefault(version => version.Version == response.CurrentVersion)))
            .Where(static item => item.Version?.SubmittedAt is not null)
            .Select(item => new AuditorRequestResponseView(tenant, program, item.Response.ResponseId, request,
                item.Response.Status, item.Version!.SubmittedAt!.Value,
                item.Version.EvidenceArtifactIds.Count == 1 ? item.Version.EvidenceArtifactIds[0] : null)).ToArray();
    }

    public async ValueTask<SampleSelectionView?> ReadSelectionAsync(Uuid tenant, Uuid program, Uuid request,
        Uuid selection, CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new AuditorRequest(tenant, program, request), ct).ConfigureAwait(false);
        var terms = await AuditorCommands.TermsAsync(content, tenant, owner, ct).ConfigureAwait(false);
        if (!terms.IsSuccess)
        {
            if (terms.Error!.Kind == RequestErrorKind.NotFound)
                return null;
            throw terms.Error.IsTransient ? new ImmutableContentUnavailableException(terms.Error.Message)
                : Invalid(terms.Error.Message);
        }
        var history = await AuditorRequestHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
        if (history.Selection(selection) is not { } admitted)
            return null;
        var child = await reader.HydrateAsync(new AuditorSampleSelection(tenant, program, request, selection), ct)
            .ConfigureAwait(false);
        if (child.Recorded is not { } recorded)
            return null;
        if (recorded.Change != admitted.Intent || recorded.Operation.RootOperationId != admitted.Operation.RootOperationId ||
            recorded.Operation.FirstOwner != owner.Stream || recorded.Operation.CausationEventId != admitted.Metadata.EventId)
            throw Invalid("The selection materialization differs from its admitted native predecessor.");
        return await SelectionAsync(owner, admitted, program, ct).ConfigureAwait(false);
    }

    async Task<SampleSelectionView> SelectionAsync(AuditorRequest owner, AuditorSampleSelectionAdmitted admitted,
        Uuid program, CancellationToken ct)
    {
        var intent = await AuditorContent.ReadAsync<AuditorSampleSelectionIntent>(content, owner.TenantId,
            admitted.Intent, ct).ConfigureAwait(false);
        var rows = await ImmutableSelectionManifests.ReadAsync<Uuid>(content, owner.Stream.Realm,
            intent.PopulationRowIds, ct).ConfigureAwait(false);
        var population = await AuditorContent.ReadAsync<AuditPopulations.Models.AuditPopulationVersionView>(content, owner.TenantId,
            intent.PopulationContent, ct).ConfigureAwait(false);
        var included = population.Rows.Where(static row => row.Classification == "included")
            .Select(static row => row.RowId).ToHashSet();
        if (intent.ProgramId != program || intent.RequestId != owner.Id || intent.SelectionId != admitted.Operation.LogicalId ||
            intent.Header.FirstOwner != owner.Stream || intent.Header.RootOperationId != admitted.Operation.RootOperationId ||
            intent.ParentAdmissionRevision != (ulong)admitted.Revision || intent.RequestGeneration != (ulong)admitted.Generation ||
            intent.PopulationId != admitted.PopulationId || intent.PopulationVersion != admitted.PopulationVersion ||
            intent.SupersedesSelectionId != admitted.SupersedesSelectionId || population.Version != intent.PopulationVersion ||
            !population.SampleEligible || population.ContentSha256.Length != 64 || rows.Count == 0 ||
            rows.Distinct().Count() != rows.Count || rows.Any(row => row == Uuid.Empty || !included.Contains(row)))
            throw Invalid("The sample selection differs from its admitted scope, immutable population, rows or amendment lineage.");
        return new(owner.TenantId, program, intent.SelectionId, owner.Id, intent.PopulationId, intent.PopulationVersion,
            population.ContentSha256, intent.SelectionMethod, rows, admitted.Intent.Sha256, "recorded", intent.RecordedAt,
            intent.Source, intent.ExternalReference, intent.SupersedesSelectionId, intent.AmendmentReason);
    }

    async Task<IReadOnlyList<AuditorRequestResponseHistoryView>> ResponseHistoriesAsync(Uuid tenant, Uuid program,
        Uuid request, AuditorRequestHistorySnapshot history, CancellationToken ct)
    {
        var result = new List<AuditorRequestResponseHistoryView>();
        foreach (var admitted in history.Responses)
        {
            var admission = await AuditorContent.ReadAsync<ChildAdmissionIntent>(content, tenant, admitted.Intent, ct)
                .ConfigureAwait(false);
            VerifyAdmission(new AuditorRequest(tenant, program, request), admitted, admission, AdmittedChildKind.AuditorResponse,
                admitted.Generation);
            var draft = await AuditorContent.ReadAsync<AuditorResponseDraftContent>(content, tenant,
                admission.CanonicalAuthoredContent, ct).ConfigureAwait(false);
            if (draft.ProgramId != program || draft.RequestId != request || draft.ResponseId != admitted.Operation.LogicalId || draft.AuthorMemberId.ToString() != admitted.Operation.Actor.Id ||
                draft.At != admitted.Operation.AcceptedAt)
                throw Invalid("The response draft differs from its admitted program or child.");
            var owner = await reader.HydrateAsync(new AuditorResponse(tenant, program, request, draft.ResponseId), ct)
                .ConfigureAwait(false);
            var facts = await AuditorResponseHistory.ReadAsync(events, owner, ct).ConfigureAwait(false);
            if (owner.Created is { } created && (created.Change != admitted.Intent ||
                created.Operation.FirstOwner != admitted.Owner || created.Operation.CausationEventId != admitted.Metadata.EventId))
                throw Invalid("The response materialization differs from its exact admitted predecessor.");
            var versions = new List<AuditorResponseVersionView>();
            if (owner.IsMaterialized)
                versions.Add(new(1, draft.Summary, draft.EvidenceArtifactIds, "draft", draft.AuthorMemberId,
                    draft.AuthorDisplay, draft.At, null, null, null));
            AuditorResponseDeliveryView? delivery = null;
            AuditorResponseDispositionView? disposition = null;
            foreach (var fact in facts)
            {
                switch (fact)
                {
                    case AuditResponseDraftRevised revised:
                        var version = await AuditorContent.ReadAsync<AuditorResponseVersionView>(content, tenant,
                            revised.Change, ct).ConfigureAwait(false);
                        if (version.Version != versions.Count + 1 || version.AuthorMemberId != revised.ActorMemberId)
                            throw Invalid("The response version differs from its native sequence or author.");
                        versions.Add(version);
                        break;
                    case AuditorResponseSubmitted submitted:
                        var submission = await AuditorContent.ReadAsync<AuditorResponseSubmissionView>(content, tenant,
                            submitted.Change, ct).ConfigureAwait(false);
                        if (submission.Version != submitted.Version)
                            throw Invalid("The response submission names another business version.");
                        versions[^1] = versions[^1] with
                        {
                            Status = "submitted",
                            SubmittedAt = submission.SubmittedAt,
                            SubmissionNote = submission.SubmissionNote
                        };
                        break;
                    case AuditorResponseDisclosureReviewed reviewed:
                        var review = await AuditorContent.ReadAsync<AuditorResponseReviewView>(content, tenant,
                            reviewed.Change, ct).ConfigureAwait(false);
                        if (review.Outcome != reviewed.Outcome || review.ReviewerMemberId != reviewed.ActorMemberId)
                            throw Invalid("The response review differs from its native decision or reviewer.");
                        versions[^1] = versions[^1] with { Status = review.Outcome, Review = review };
                        break;
                    case AuditorResponseDelivered delivered:
                        delivery = await AuditorContent.ReadAsync<AuditorResponseDeliveryView>(content, tenant,
                            delivered.Change, ct).ConfigureAwait(false);
                        if (delivery.Version != delivered.Version)
                            throw Invalid("The response delivery names another frozen business version.");
                        versions[^1] = versions[^1] with { Status = "delivered" };
                        break;
                    case AuditorResponseDispositionRecorded recorded:
                        disposition = await AuditorContent.ReadAsync<AuditorResponseDispositionView>(content, tenant,
                            recorded.Change, ct).ConfigureAwait(false);
                        if (disposition.Version != recorded.Version || disposition.Outcome != recorded.Outcome)
                            throw Invalid("The response disposition names another delivered version or decision.");
                        break;
                }
            }
            if (!AuditorResponseVersionNumber.TryGet(owner.BusinessVersion, out var currentVersion) && owner.IsMaterialized)
                throw Invalid("The response business version exceeds the public view range.");
            result.Add(new(draft.ResponseId, currentVersion, delivery?.Version,
                owner.IsMaterialized ? owner.Status : "pending", versions.AsReadOnly(), delivery, disposition));
        }
        return result.AsReadOnly();
    }

    static void VerifyAdmission(AuditorRequest owner, DomainEvent fact, ChildAdmissionIntent intent,
        AdmittedChildKind kind, long generation)
    {
        var operation = ((INativeOperationFact)fact).Operation;
        if (intent.ParentId != owner.Id || intent.ParentGeneration != (ulong)generation || intent.ChildKind != kind ||
            intent.ChildId != operation.LogicalId || intent.Header.FirstOwner != owner.Stream ||
            intent.Header.RootOperationId != operation.RootOperationId || intent.Header.OriginalActor != operation.Actor ||
            intent.Header.AuthoredIntentSha256 != operation.AuthoredIntentSha256 ||
            intent.ParentAdmissionRevision != fact.Metadata.AggregateVersion)
            throw Invalid("The child intent differs from its exact native admission.");
    }

    internal static string WorkStatus(AuditorRequest owner, IReadOnlyList<AuditorRequestResponseHistoryView> responses,
        DateOnly due, DateOnly asOf) => owner.Resolved is not null ? "resolved" :
        responses.Any(static response => response.Disposition?.Outcome == "accepted") ? "accepted" :
        responses.Any(static response => response.Status == "rejected") ? "blocked" :
        responses.Any(static response => response.Status is "submitted" or "approved" or "delivered") ? "submitted" :
        asOf > due ? "overdue" : "open";
    static ImmutableContentIntegrityException Invalid(string message) => new(message);
}
