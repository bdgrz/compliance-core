using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The irreversible authorization of a personal engagement decision, appended only after every required source
///     use was acquired. <paramref name="DecisionAt" /> is the server-frozen decision instant that every temporal
///     validity interval is judged at; the child decision materializes from this fact and nothing earlier.
/// </summary>
[Discriminator("bdgrz.service-engagement-independence.engagement-personal-decision-authorized", 1)]
public sealed record EngagementPersonalDecisionAuthorized(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    DateTimeOffset DecisionAt, ImmutableContentRef AuthorizationProofs, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
