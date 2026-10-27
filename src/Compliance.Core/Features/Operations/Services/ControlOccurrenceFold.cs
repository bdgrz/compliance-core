using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
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
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Operations.Services;

/// <summary>
///     Folds the facts of a hydrated <see cref="ControlOccurrence" /> into its public view, reading each attestation
///     and review from its verified immutable content. The raw state is never derived overdue; presentation applies
///     the date.
/// </summary>
public static class ControlOccurrenceFold
{
    /// <summary>The raw view of a materialized occurrence, or null when the occurrence was never materialized.</summary>
    public static async Task<ControlOccurrenceView?> ReadAsync(ControlOccurrence occurrence,
        IDomainEventReader events, IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(content);
        if (tenantId != occurrence.TenantId)
            throw new ImmutableContentIntegrityException("The operating composition belongs to another tenant.");
        if (occurrence.Opened is not { } opened)
            return null;
        var facts = await ControlOccurrenceHistory.ReadAsync(events, occurrence, ct).ConfigureAwait(false);
        var intent = await ControlOperatingContent.ReadAsync<ControlOccurrenceCreationIntent>(content, tenantId,
            opened.CreationIntent, ct).ConfigureAwait(false);
        if (opened.CreationIntent.Schema != ControlOccurrenceCreationIntent.Schema || opened.Intent != opened.CreationIntent ||
            intent.TenantId != tenantId || intent.ProgramId != occurrence.ProgramId || intent.ControlId != occurrence.ControlId ||
            intent.OccurrenceId != occurrence.Id || intent.Window != opened.BusinessWindow || intent.PlanVersionId != opened.PlanVersionId ||
            intent.Header is null || intent.Header.RootOperationId != opened.Operation.RootOperationId ||
            intent.Header.FirstOwner != (opened.Operation.FirstOwner ?? opened.Operation.Owner) ||
            intent.Header.OriginalActor != opened.Operation.Actor || intent.Header.Episode != opened.Operation.Episode ||
            intent.Header.OriginalExpectedRevision != opened.Operation.OriginalExpectedRevision ||
            intent.Header.AuthoredIntentSha256 != opened.Operation.AuthoredIntentSha256 ||
            intent.PlanProof is null || intent.PlanProof.Stream != new ControlOperatingPlan(tenantId, occurrence.ProgramId, occurrence.ControlId).Stream ||
            intent.PlanProof.EventId == Uuid.Empty || intent.PlanProof.AggregateVersion == 0 ||
            opened.OpenedBy != opened.Operation.Actor || opened.OpenedAt != opened.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The opening terms differ from their exact frozen creation intent and original source authority.");
        var attestations = new List<ControlAttestationView>();
        var reviews = new List<ControlOccurrenceReviewView>();
        var state = ControlOperationStates.Open;
        foreach (var fact in facts)
            switch (fact)
            {
                case ControlOccurrenceAttested attested:
                    var attestation = await ControlOperatingContent.ReadAsync<ControlAttestationView>(content,
                        tenantId, attested.AttestationContent, ct).ConfigureAwait(false);
                    if (attested.AttestationContent.Schema != ControlOperatingContent.AttestationSchema ||
                        attestation.Version != attestations.Count + 1 || attestation.AttestationId != Uuid.CreateVersion5(occurrence.Id,
                            "attestation-" + attestation.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
                        attestation.PlanVersionId != opened.PlanVersionId || attestation.ControlVersionId != opened.ControlVersionId ||
                        attestation.RecordedBy != attested.Operation.Actor || attestation.RecordedAt != attested.Operation.AcceptedAt ||
                        attestation.RecorderMemberId.ToString() != attested.Operation.Actor.Id || attestation.Evidence is null ||
                        attestation.ExpectedEvidence is null)
                        throw new ImmutableContentIntegrityException("The immutable attestation differs from its exact occurrence version or original recorder.");
                    attestations.Add(attestation);
                    state = ControlOperationStates.Submitted;
                    break;
                case ControlOccurrenceReviewed reviewed:
                    var review = await ControlOperatingContent.ReadAsync<ControlOccurrenceReviewView>(content,
                        tenantId, reviewed.ReviewContent, ct).ConfigureAwait(false);
                    if (reviewed.ReviewContent.Schema != ControlOperatingContent.ReviewSchema || attestations.Count == 0 ||
                        review.DecisionId != reviewed.Operation.LogicalId || review.AttestationId != attestations[^1].AttestationId ||
                        review.AttestationVersion != attestations[^1].Version || review.ReviewedBy != reviewed.Operation.Actor ||
                        review.ReviewedAt != reviewed.Operation.AcceptedAt || review.ReviewerMemberId.ToString() != reviewed.Operation.Actor.Id ||
                        review.RequestedActions is null)
                        throw new ImmutableContentIntegrityException("The immutable review differs from its exact attestation or original reviewer.");
                    reviews.Add(review);
                    state = review.Outcome;
                    break;
            }

        return new ControlOccurrenceView(opened.TenantId, opened.ProgramId, opened.ControlId, opened.OccurrenceId,
            occurrence.Revision, opened.Kind, state, opened.ControlVersionId, opened.PlanVersionId, opened.PeriodStart,
            opened.PeriodEnd, opened.DueOn, opened.Trigger, opened.Assignee, attestations, reviews, []);
    }

    /// <summary>Presents the view as of a date: an open occurrence past its due date is missed.</summary>
    public static ControlOccurrenceView Present(ControlOccurrenceView view, DateOnly? today)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.State == ControlOperationStates.Open && view.DueOn is { } due && today is { } asOf && due < asOf
            ? view with { State = ControlOperationStates.Missed }
            : view;
    }

    /// <summary>The view after one more attestation version.</summary>
    public static ControlOccurrenceView WithAttestation(ControlOccurrenceView view, ControlAttestationView attestation,
        long revision) => view with
        {
            Revision = revision,
            State = ControlOperationStates.Submitted,
            Attestations = [.. view.Attestations, attestation],
        };

    /// <summary>The view after one more review decision.</summary>
    public static ControlOccurrenceView WithReview(ControlOccurrenceView view, ControlOccurrenceReviewView review,
        long revision) => view with
        {
            Revision = revision,
            State = review.Outcome,
            Reviews = [.. view.Reviews, review],
        };
}
