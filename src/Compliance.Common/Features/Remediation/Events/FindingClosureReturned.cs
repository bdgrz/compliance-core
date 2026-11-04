using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>A reviewer returned this exact closure proposal revision with comments.</summary>
[Discriminator("bdgrz.finding.closure.returned", 1)]
public sealed record FindingClosureReturned(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Comments, FindingClosureReviewBinding Review, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
