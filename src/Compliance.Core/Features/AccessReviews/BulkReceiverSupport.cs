using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>Shared decisions of the bulk decision receivers: the native replay or conflict, and the closed results.</summary>
static class BulkReceiverSupport
{
    /// <summary>The closed result of a receiver whose logical id was already accepted on the owner, or null.</summary>
    public static Result<ReceiverOutcome>? Decide(NativeAcceptance acceptance, EventStreamAddress stream,
        AcceptedOperationBinding binding) => acceptance.Decide(binding) switch
        {
            NativeAcceptanceDecision.Replayed => SourceUseReceiverSupport.Replayed(NativeFactReference.Of(stream,
                acceptance.SelectedFact!)),
            NativeAcceptanceDecision.Conflict => Result<ReceiverOutcome>.Failure(AcceptedAppendResult.ConflictError),
            _ => null,
        };

    public static NativeFactReference Fact(EventStreamAddress stream, DomainEvent fact) =>
        NativeFactReference.Of(stream, fact);

    /// <summary>The reference of the admission fact a bulk decision's receivers and reactors name as their source.</summary>
    public static NativeFactReference Admission(AccessReviewCampaign campaign, AccessReviewDecisionBatchAdmitted admitted) =>
        NativeFactReference.Of(campaign.Stream, admitted);
}
