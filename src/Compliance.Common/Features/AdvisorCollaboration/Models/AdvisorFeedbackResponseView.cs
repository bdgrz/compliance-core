using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Models;

/// <summary>An attributed internal response to advisor feedback.</summary>
public sealed record AdvisorFeedbackResponseView(string Response, ActorReference RespondedBy,
    DateTimeOffset RespondedAt);
