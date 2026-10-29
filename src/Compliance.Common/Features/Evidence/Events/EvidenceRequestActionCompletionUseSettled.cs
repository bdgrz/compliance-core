using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>
///     A risk action completion review use of an evidence request ended. The settlement intent records whether the
///     review reached its irreversible decision or was canceled; a settlement before any grant fences the key.
/// </summary>
[Discriminator("bdgrz.evidence-request.evidence-request-action-completion-use-settled", 1)]
public sealed record EvidenceRequestActionCompletionUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
