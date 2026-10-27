using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Applications.Events;

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
