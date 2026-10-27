using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>A compartment-labeled request discussion entry.</summary>
public sealed record AuditorRequestCommentView(
    Uuid CommentId,
    string Body,
    string Compartment,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset RecordedAt);
