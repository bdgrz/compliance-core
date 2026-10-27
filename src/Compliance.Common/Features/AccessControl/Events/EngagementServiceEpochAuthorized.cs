using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     Holds the exact complete service history and epoch an acceptance or staff assignment relies on until the
///     operation reaches a terminal fact. The hold does not advance the epoch, so <paramref name="Revision" /> is the
///     held epoch, but a held epoch refuses further services.
/// </summary>
[Discriminator("bdgrz.client-service-independence.engagement-service-epoch-authorized", 1)]
public sealed record EngagementServiceEpochAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, ISourceUseFact;
