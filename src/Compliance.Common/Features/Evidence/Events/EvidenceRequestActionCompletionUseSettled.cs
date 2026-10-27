using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>
///     A risk action completion review use of an evidence request ended. The settlement intent records whether the
///     review reached its irreversible decision or was canceled; a settlement before any grant fences the key.
/// </summary>
[Discriminator("bdgrz.evidence-request.evidence-request-action-completion-use-settled", 1)]
public sealed record EvidenceRequestActionCompletionUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
