using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     A person's acknowledgement of the exact policy version, hash, and text shown, applied to the participant
///     owner of <c>(CampaignId, PersonId)</c>. <c>Performer</c> is the workforce person; <c>Recorder</c> is the
///     member who recorded it, which differs only when a member records it for a non-member.
/// </summary>
[Discriminator("bdgrz.policy.acknowledgement.recorded", 2)]
public sealed record PolicyAcknowledgementRecorded(Uuid TenantId, Uuid CampaignId,
    Uuid AcknowledgementId, Uuid PersonId, Uuid PolicyId, long PolicyVersion,
    string ContentSha256, string AcknowledgementText, ActorReference Performer,
    ActorReference Recorder, bool RecordedOnBehalf, DateTimeOffset AcknowledgedAt, OperationProvenance Operation,
    ImmutableContentRef Intent, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
