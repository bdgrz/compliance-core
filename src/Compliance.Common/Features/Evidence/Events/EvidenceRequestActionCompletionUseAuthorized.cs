using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence.Events;

/// <summary>
///     A fulfilled evidence request is held for a risk action completion review. The use does not advance the public
///     request revision: <paramref name="Revision" /> is the held revision, and <paramref name="Generation" /> is the
///     action's submission generation the review seals.
/// </summary>
[Discriminator("bdgrz.evidence-request.evidence-request-action-completion-use-authorized", 1)]
public sealed record EvidenceRequestActionCompletionUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
