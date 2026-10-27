using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.legal_hold_placed", 1)]
public sealed record ArtifactLegalHoldPlaced(ArtifactRetentionSource Source, long Revision, ulong SourcePosition,
    Uuid HoldId, string Reason, Bdgrz.Compliance.Features.AccessControl.ActorReference RecordedBy, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse = null,
    ImmutableContentRef? OperationResult = null) : DomainEvent, INativeOperationFact;
