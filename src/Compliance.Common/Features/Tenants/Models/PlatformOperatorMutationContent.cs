using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>Exact immutable authored intent and original attribution of one roster operation.</summary>
public sealed record PlatformOperatorMutationContent(string CommandKind, Uuid CommandId, ulong? ExpectedRevision,
    Uuid ActorUserId, Uuid SubjectUserId, Uuid AssignmentId, Uuid[] BootstrapUserIds, string Reason,
    ActorAttribution OriginalActor, ActorReference Actor, DateTimeOffset AcceptedAt);
