using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     The finding accepted one corrective action for the current finding generation. <paramref name="Intent" />
///     is the frozen <c>ChildAdmissionIntent</c> that carries the complete action; the action owner is materialized
///     independently and the admission, not the materialization, is the authority. The logical id of
///     <paramref name="Operation" /> is the action id.
/// </summary>
[Discriminator("bdgrz.finding-aggregate.corrective-action-admitted", 1)]
public sealed record CorrectiveActionAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
