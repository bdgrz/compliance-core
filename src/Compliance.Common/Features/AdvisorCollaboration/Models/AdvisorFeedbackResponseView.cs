using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>An attributed internal response to advisor feedback.</summary>
public sealed record AdvisorFeedbackResponseView(string Response, ActorReference RespondedBy,
    DateTimeOffset RespondedAt);
