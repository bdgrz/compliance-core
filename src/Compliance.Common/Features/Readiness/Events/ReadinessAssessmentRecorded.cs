using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Readiness.Models;
namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>
///     The immutable result of computing one readiness assessment from its frozen inputs, at the assessment owner's own
///     revision one. Each manifest is the complete original collection; <c>Intent</c> is the frozen
///     <see cref="ReadinessRunIntent" /> the computation used and nothing else.
/// </summary>
[Discriminator("bdgrz.readiness.assessment.recorded", 1)]
public sealed record ReadinessAssessmentRecorded(Uuid TenantId, Uuid ProgramId, long Revision,
    Uuid AssessmentId, string RuleVersion, DateTimeOffset AsOf, Uuid? EditionId,
    string InputFingerprint, ImmutableContentRef InputsManifest, ImmutableContentRef FindingsManifest,
    ImmutableContentRef GapsManifest, ImmutableContentRef AccessReviewScopesManifest, Uuid RunnerMemberId,
    ActorReference RunBy, DateTimeOffset RunAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
