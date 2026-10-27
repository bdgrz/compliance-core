using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Retention;

[Discriminator("bdgrz.artifact_retention.bound", 1)]
public sealed record ArtifactRetentionBound(ArtifactRetentionSource Source, long Revision, ulong SourcePosition,
    Bdgrz.Compliance.Features.AccessControl.ActorReference RecordedBy, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse = null,
    ImmutableContentRef? OperationResult = null) : DomainEvent, INativeOperationFact;
