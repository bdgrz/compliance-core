using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     A pending resolution ended before it resolved the request. The fact is the local receiver fence: a delayed
///     <c>CompleteAuditorRequestResolution</c> sees it and does nothing, and only then are the response uses settled.
///     <paramref name="SourceProofs" /> is the failure proof. Admission reopens for the generation.
/// </summary>
[Discriminator("bdgrz.auditor-request.auditor-request-resolution-canceled", 1)]
public sealed record AuditorRequestResolutionCanceled(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
