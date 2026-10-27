using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     The target application grants the exclusive hold of one batch group. <paramref name="Revision" /> is the
///     published content revision at the hold, which a hold never advances; the group's frozen baseline and
///     candidate revisions are carried so the hold rebuilds from the ordered stream alone.
/// </summary>
[Discriminator("bdgrz.declared_application.import_target_authorized", 1)]
public sealed record ApplicationImportTargetAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, Uuid BatchId, Uuid ApplicationId,
    ulong InitialApplicationRevision, ulong CandidateContentRevision) : DomainEvent, INativeOperationFact;
