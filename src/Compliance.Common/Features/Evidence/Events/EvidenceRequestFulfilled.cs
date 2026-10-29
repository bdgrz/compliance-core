using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>Fulfils a request by linking the evidence artifact that answers it; fulfilment is final.</summary>
[Discriminator("bdgrz.evidence.request.fulfilled", 2)]
public sealed record EvidenceRequestFulfilled(Uuid TenantId, Uuid ProgramId, Uuid EvidenceRequestId,
    long Revision, Uuid ArtifactId, ActorReference FulfilledBy, DateTimeOffset FulfilledAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
