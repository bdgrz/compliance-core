using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>
///     Selects the due recurring occurrences of one Type II period for one generation key. Selection is a complete
///     immutable manifest, never a count cap: <paramref name="OccurrenceIdsManifest" /> lists every selected canonical
///     occurrence id in canonical order and <paramref name="CreationIntentManifest" /> holds the full creation intent
///     of each. A selected occurrence is not yet materialized; the occurrence fan-out materializes each one.
/// </summary>
[Discriminator("bdgrz.operations.due-control-occurrences-generated", 1)]
public sealed record DueControlOccurrencesGenerated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    string GenerationKey,
    DateOnly ThroughDate,
    ImmutableContentRef OccurrenceIdsManifest,
    Uuid? ActorMemberId,
    string ActorDisplay,
    DateTimeOffset OccurredAt,
    OperationProvenance Operation,
    ImmutableContentRef CreationIntentManifest,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, ITypeIIPeriodStateFact
{
    public long NativeSequence { get; init; }
    public TypeIIPeriodAuthorityHeader Header { get; init; } = null!;
    public ImmutableContentRef CurrentContent { get; init; } = null!;
}
