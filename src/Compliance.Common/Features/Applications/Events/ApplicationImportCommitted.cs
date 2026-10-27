using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The one publication fact of a batch: every target is published together. <paramref name="EffectsManifest" />
///     lists the native proof of every frozen row; <paramref name="FrozenRows" /> is the complete frozen row list.
///     <paramref name="SourceGeneration" /> is the source revision the plan was frozen against.
/// </summary>
[Discriminator("bdgrz.application_import.committed", 2)]
public sealed record ApplicationImportCommitted(Uuid TenantId, string SourceKey,
    string SourceNamespace, Uuid BatchId, long Revision, string PlanSha256,
    ImmutableContentRef EffectsManifest, DateTimeOffset CommittedAt, OperationProvenance Operation,
    ImmutableContentRef FrozenRows, long SourceGeneration, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
