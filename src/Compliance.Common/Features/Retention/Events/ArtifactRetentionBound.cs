using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Retention.Events;

[Discriminator("bdgrz.artifact_retention.bound", 1)]
public sealed record ArtifactRetentionBound(ArtifactRetentionSource Source, long Revision, ulong SourcePosition,
    Bdgrz.Compliance.Features.AccessControl.Models.ActorReference RecordedBy, DateTimeOffset RecordedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse = null,
    ImmutableContentRef? OperationResult = null) : DomainEvent, INativeOperationFact;
