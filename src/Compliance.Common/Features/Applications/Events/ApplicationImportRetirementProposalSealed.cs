using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>
///     The preparer's omission proposal, sealed with its complete immutable content. It is never authority to retire:
///     acceptance still requires current impact evidence.
/// </summary>
[Discriminator("bdgrz.application_import.retirement_proposal_sealed", 2)]
public sealed record ApplicationImportRetirementProposalSealed(Uuid TenantId, Uuid BatchId,
    long Revision, string ProposalSha256, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult, ImmutableContentRef Proposal) : DomainEvent, INativeOperationFact;
