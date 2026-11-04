using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness.Events;

/// <summary>One assessment's immutable result, including source versions, completeness and verdict.</summary>
[Discriminator("bdgrz.readiness.assessment.recorded", 2)]
public sealed record ReadinessAssessmentRecordedV2(Uuid TenantId, Uuid ProgramId, Uuid AssessmentId,
    string RuleVersion, DateTimeOffset AsOf, ImmutableContentRef ResultContent, ActorReference RequestedBy,
    DateTimeOffset RecordedAt, DateOnly? ScheduledDay, string AuthoredIntentSha256) : DomainEvent;
