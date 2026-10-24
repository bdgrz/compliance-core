using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     The feedback thread was materialized from an admitted contribution, at thread revision one.
///     <paramref name="Intent" /> repeats the admission intent the handoff accepted; the author and instant are the
///     original actor and the admission instant.
/// </summary>
[Discriminator("bdgrz.advisor-feedback.recorded", 1)]
public sealed record AdvisorFeedbackRecorded(Uuid TenantId, Uuid ProgramId, Uuid HandoffId,
    Uuid FeedbackId, Uuid RecordId, Uuid RecordVersionId, string Kind, string Body,
    ActorReference Author, DateTimeOffset CreatedAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact
{
    /// <summary>The native selected identity for sources whose version is not a UUID pair.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AdvisorRecordVersion? SelectedSource { get; init; }
}
