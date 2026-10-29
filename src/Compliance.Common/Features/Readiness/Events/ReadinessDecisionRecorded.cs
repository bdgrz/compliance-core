using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     The management readiness decision was recorded once every gap plan was held. <c>DecisionContent</c> is the
///     complete <see cref="ReadinessDecisionView" />, <c>DecisionProofs</c> the complete manifest of the held gap plan
///     proofs, and <c>OperationResult</c> the original typed result of the decision command.
/// </summary>
[Discriminator("bdgrz.readiness.decision.recorded", 1)]
public sealed record ReadinessDecisionRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef DecisionContent, OperationProvenance Operation, Uuid AssessmentId, Uuid DecisionId,
    ImmutableContentRef DecisionProofs, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
