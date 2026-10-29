using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The open request accepted one response draft for its current generation. <paramref name="Intent" /> is the frozen
///     <c>ChildAdmissionIntent</c> that carries the complete draft; the response owner is materialized independently,
///     and a later resolution drains it instead of discarding it. The logical id of <paramref name="Operation" /> is the
///     response id.
/// </summary>
[Discriminator("bdgrz.auditor-request.auditor-response-admitted", 1)]
public sealed record AuditorResponseAdmitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
