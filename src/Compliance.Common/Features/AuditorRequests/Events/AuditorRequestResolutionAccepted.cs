using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     The root native acceptance of a resolution. It seals response, comment and sample admission for
///     <c>Generation</c> and references the frozen <c>ClosureIntent</c>; the request only resolves once
///     <see cref="AuditorRequestResolved" /> follows.
/// </summary>
[Discriminator("bdgrz.auditor-request.auditor-request-resolution-accepted", 1)]
public sealed record AuditorRequestResolutionAccepted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
