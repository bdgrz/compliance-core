using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>The attributable submission of one exact draft version of a response.</summary>
public sealed record AuditorResponseSubmissionView(int Version, string SubmissionNote, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset SubmittedAt);
