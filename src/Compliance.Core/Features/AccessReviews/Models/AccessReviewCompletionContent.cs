using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>The authored closing decision a completion freezes into its closure intent.</summary>
public sealed record AccessReviewCompletionDecision(Uuid CommandId, Uuid CampaignId, long ExpectedRevision,
    string Attestation);

/// <summary>
///     What a completion pinned about one item when it was admitted: the item's own revision and the decision and
///     remediation state that satisfied the completion rules. The use is only granted while the item is exactly there.
/// </summary>
public sealed record AccessReviewItemCompletionFacts(Uuid ItemId, long Revision, Uuid DecisionId, string Decision,
    string RemediationStatus);

/// <summary>The held proof of one item recorded by the closing fact: its pin, its grant and the facts it satisfied.</summary>
public sealed record AccessReviewItemCompletionProof(Uuid ItemId, SourceUseRequirement Requirement,
    ReservationGrant Grant, AccessReviewItemCompletionFacts Facts);

/// <summary>The authoritative permanent proof mismatch that cancels a pending completion.</summary>
public sealed record AccessReviewCompletionFailure(string Reason, Uuid? ItemId, NativeFactReference? Evidence);
