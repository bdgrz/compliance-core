using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The canonical authored content of one admitted comment: normalized body and compartment with the original actor.</summary>
public sealed record AuditorCommentContent(Uuid ProgramId, Uuid RequestId, Uuid CommentId, string Body,
    string Compartment, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset At);
