using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The open request accepted one comment. <paramref name="Intent" /> is the frozen <c>ChildAdmissionIntent</c> with
///     the complete body, compartment and original actor. The logical id of <paramref name="Operation" /> is the comment
///     id, which is also the identity of the thread that materializes it.
/// </summary>
[Discriminator("bdgrz.auditor-request.auditor-comment-admitted", 1)]
public sealed record AuditorCommentAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
