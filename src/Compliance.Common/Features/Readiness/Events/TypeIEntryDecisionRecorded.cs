using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     The Type I entry decision was recorded once every gap plan was held. <c>DecisionContent</c> is the complete
///     <see cref="TypeIEntryDecisionView" /> with its frozen unresolved items, <c>DecisionProofs</c> the complete
///     manifest of the held gap plan proofs, and <c>OperationResult</c> the original typed result of the command.
/// </summary>
[Discriminator("bdgrz.readiness.type_i_entry.decided", 1)]
public sealed record TypeIEntryDecisionRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef DecisionContent, OperationProvenance Operation, Uuid AssessmentId, Uuid DecisionId,
    ImmutableContentRef DecisionProofs, ImmutableContentRef Intent, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
