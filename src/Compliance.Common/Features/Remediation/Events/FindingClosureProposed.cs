using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>One closure proposal revision was recorded, retaining exact evidence and admitted-action selections.</summary>
[Discriminator("bdgrz.finding.closure.proposed", 1)]
public sealed record FindingClosureProposed(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, Uuid ClosureId, long ProposalRevision, FindingClosureTerms Terms,
    ImmutableSelectionManifest ActionsReliedOn, ActorReference ProposedBy, DateTimeOffset ProposedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
