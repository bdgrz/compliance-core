using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     Folds the facts of a hydrated <see cref="ControlOccurrence" /> into its public view, reading each attestation
///     and review from its verified immutable content. The raw state is never derived overdue; presentation applies
///     the date.
/// </summary>
public static class ControlOccurrenceFold
{
    /// <summary>The raw view of a materialized occurrence, or null when the occurrence was never materialized.</summary>
    public static async Task<ControlOccurrenceView?> ReadAsync(ControlOccurrence occurrence,
        IImmutableContentStore content, Uuid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(content);
        if (occurrence.Opened is not { } opened)
            return null;
        var attestations = new List<ControlAttestationView>();
        var reviews = new List<ControlOccurrenceReviewView>();
        var state = ControlOperationStates.Open;
        foreach (var fact in occurrence.Facts)
            switch (fact)
            {
                case ControlOccurrenceAttested attested:
                    attestations.Add(await ControlOperatingContent.ReadAsync<ControlAttestationView>(content,
                        tenantId, attested.AttestationContent, ct).ConfigureAwait(false));
                    state = ControlOperationStates.Submitted;
                    break;
                case ControlOccurrenceReviewed reviewed:
                    var review = await ControlOperatingContent.ReadAsync<ControlOccurrenceReviewView>(content,
                        tenantId, reviewed.ReviewContent, ct).ConfigureAwait(false);
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
