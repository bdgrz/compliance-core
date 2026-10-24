using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     The complete terms of a request after one accepted recording, revision or assignment, with the reason and the
///     attributable decision that produced them. It is verified immutable content: the request owner references it and
///     never copies it into an event.
/// </summary>
public sealed record AuditorRequestTermsContent(Uuid ProgramId, Uuid ExaminationId, string Source, string ExternalReference,
    string Wording, string Scope, string Priority, Uuid OwnerMemberId, DateOnly DueOn, bool RequiresInternalReview,
    string Reason, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset At);
