using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The permanent practice wall of one subject: this staff member or canonical user serves the practice for this
///     client for ever. No fact releases, revokes or reactivates it, whatever happens to the engagement, the staff
///     record or the reservation that preceded it. <paramref name="Revision" /> counts the permanent commitments of the
///     subject, and the practice and engagement are carried because the owner's reducer cannot read immutable content.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.practice-independence-committed", 1)]
public sealed record PracticeIndependenceCommitted(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, string Practice, Uuid EngagementId)
    : DomainEvent, INativeOperationFact;
