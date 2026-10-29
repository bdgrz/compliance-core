using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Events;

/// <summary>
///     A reviewed completion observation from <c>manual</c> evidence or an <c>lms_export</c> row, applied to the
///     participant owner. The evidence reference names the source artifact.
/// </summary>
[Discriminator("bdgrz.training_completion.recorded", 2)]
public sealed record TrainingCompletionRecorded(Uuid TenantId, Uuid CampaignId,
    Uuid CompletionId, Uuid PersonId, Uuid RequirementId, long RequirementVersion,
    DateOnly CompletedOn, string Source, string EvidenceReference, ActorReference Recorder,
    DateTimeOffset RecordedAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
