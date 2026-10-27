using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>Exact supplied receiver intent and server-prepared bounded authority/source at admission.</summary>
public sealed record WeeklyDigestMutationContent(string CommandKind, DeliverWeeklyWorkDigest? Delivery,
    RecordWorkDigestDelivery? Recording, ActorReference Actor, ActorAttribution OriginalActor,
    DateTimeOffset AcceptedAt, WeeklyWorkDigestHeader Before, WorkDigestDeliveryAttemptView? SelectedBefore,
    WorkDigestDeliveryAttemptView? LatestBefore, string? SuppressionReason,
    SourceFactProof<WeeklyWorkDigestScheduled>? ScheduleSource,
    SourceFactProof<WeeklyWorkDigestDeliveryRequested>? RequestSource, bool NoChange);
