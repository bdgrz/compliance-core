using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     The proof a closure holds for one participant: the stream, the number of results applied to it and the exact
///     closure use the participant granted.
/// </summary>
public sealed record CampaignParticipantProof(Uuid PersonId, EventStreamAddress Participant, long Revision,
    ReservationGrant Grant);
