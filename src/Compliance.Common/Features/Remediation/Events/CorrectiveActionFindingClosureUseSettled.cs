using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     A finding-closure use of a corrective action ended. <paramref name="Disposition" /> repeats the disposition
///     of the settlement intent so the owner can install the closed-parent fence from its own ordered stream: a
///     <see cref="SourceUseSettlementDisposition.ParentClosed" /> settlement keeps the action under the closed finding
///     generation, and releasing the temporary use never restores an unrestricted writable action.
/// </summary>
[Discriminator("bdgrz.corrective-action.corrective-action-finding-closure-use-settled", 1)]
public sealed record CorrectiveActionFindingClosureUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult,
    SourceUseSettlementDisposition Disposition) : DomainEvent, ISourceUseFact;
