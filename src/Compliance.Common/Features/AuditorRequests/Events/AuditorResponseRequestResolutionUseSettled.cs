using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Events;

/// <summary>
///     A request-resolution use of a response ended. <paramref name="Disposition" /> repeats the disposition of the
///     settlement intent so the response can install the closed-parent fence from its own ordered stream: a
///     <see cref="SourceUseSettlementDisposition.ParentClosed" /> settlement keeps the response under the resolved
///     request generation, and releasing the temporary use never restores an unrestricted writable response.
/// </summary>
[Discriminator("bdgrz.auditor-response.auditor-response-request-resolution-use-settled", 1)]
public sealed record AuditorResponseRequestResolutionUseSettled(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult,
    SourceUseSettlementDisposition Disposition) : DomainEvent, ISourceUseFact;
