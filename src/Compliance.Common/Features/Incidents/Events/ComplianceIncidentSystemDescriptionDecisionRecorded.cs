using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents.Events;

/// <summary>One bounded native incident fact with exact immutable business payload and original accepted response.</summary>
[Discriminator("bdgrz.incidents.compliance-incident-system-description-decision-recorded", 3)]
public sealed record ComplianceIncidentSystemDescriptionDecisionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ComplianceIncidentAuthorityHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Change, long NativeSequence,
    int ChangeCount, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    DateTimeOffset OccurredAt) : DomainEvent, IComplianceIncidentFact;
