using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Fulfils a request by linking the evidence artifact that answers it; fulfilment is final.</summary>
[Discriminator("bdgrz.evidence.request.fulfilled", 2)]
public sealed record EvidenceRequestFulfilled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, Uuid ArtifactId, ActorReference FulfilledBy, DateTimeOffset FulfilledAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
