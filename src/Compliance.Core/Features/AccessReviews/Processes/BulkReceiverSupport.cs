using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.AccessReviews.Processes;

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
