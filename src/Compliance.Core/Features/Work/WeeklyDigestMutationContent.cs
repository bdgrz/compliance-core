using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Exact supplied receiver intent and server-prepared bounded authority/source at admission.</summary>
public sealed record WeeklyDigestMutationContent(string CommandKind, DeliverWeeklyWorkDigest? Delivery,
    RecordWorkDigestDelivery? Recording, ActorReference Actor, ActorAttribution OriginalActor,
    DateTimeOffset AcceptedAt, WeeklyWorkDigestHeader Before, WorkDigestDeliveryAttemptView? SelectedBefore,
    WorkDigestDeliveryAttemptView? LatestBefore, string? SuppressionReason,
    SourceFactProof<WeeklyWorkDigestScheduled>? ScheduleSource,
    SourceFactProof<WeeklyWorkDigestDeliveryRequested>? RequestSource, bool NoChange);
