using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>One immutable method publication, with its original operation and result.</summary>
[Discriminator("bdgrz.risk.method.version_published", 2)]
public sealed record RiskMethodVersionPublished(Uuid TenantId, Uuid ProgramId, long PublishedVersion,
    ImmutableContentRef Content, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
