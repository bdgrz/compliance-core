using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>
///     The handoff accepted one feedback contribution while it was ready, unrevoked and unclosed, on an exactly
///     selected version. <paramref name="Intent" /> is the frozen <c>ChildAdmissionIntent</c> that carries the complete
///     contribution; the thread is materialized independently and the admission, not the materialization, is the
///     authority. The logical id of <paramref name="Operation" /> is the feedback id and
///     <paramref name="Generation" /> is the selection generation of the handoff at admission.
/// </summary>
[Discriminator("bdgrz.advisor-handoff.advisor-feedback-admitted", 1)]
public sealed record AdvisorFeedbackAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
