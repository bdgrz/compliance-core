using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Events;

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
