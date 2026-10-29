using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Risks.Events;

/// <summary>One immutable method publication, with its original operation and result.</summary>
[Discriminator("bdgrz.risk.method.version_published", 2)]
public sealed record RiskMethodVersionPublished(Uuid TenantId, Uuid ProgramId, long PublishedVersion,
    ImmutableContentRef Content, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
