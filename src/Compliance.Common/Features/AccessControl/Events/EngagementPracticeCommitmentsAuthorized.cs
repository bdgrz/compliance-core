using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     The irreversible authorization of an acceptance or positive staff assignment, appended only when every
///     temporary reservation and every source use is held by its exact grant. <paramref name="DecisionAt" /> is the
///     server-frozen instant every temporal validity interval is judged at. Only after this fact are permanent practice
///     walls committed; before it, every temporary grant can still be fenced and settled.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.engagement-practice-commitments-authorized", 1)]
public sealed record EngagementPracticeCommitmentsAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    DateTimeOffset DecisionAt, ImmutableContentRef AuthorizationProofs, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
