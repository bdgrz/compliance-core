using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     A response with an accepted external disposition is held for a request resolution. The use does not advance the
///     public response revision: <paramref name="Revision" /> is the held revision and <paramref name="Generation" /> the
///     request generation the resolution seals.
/// </summary>
[Discriminator("bdgrz.auditor-response.auditor-response-request-resolution-use-authorized", 1)]
public sealed record AuditorResponseRequestResolutionUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
