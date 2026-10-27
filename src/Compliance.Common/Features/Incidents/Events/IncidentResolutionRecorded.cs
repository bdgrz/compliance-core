using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>One bounded native incident fact with exact immutable business payload and original accepted response.</summary>
[Discriminator("bdgrz.incidents.incident-resolution-recorded", 3)]
public sealed record IncidentResolutionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ComplianceIncidentAuthorityHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Change, long NativeSequence,
    int ChangeCount, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    DateTimeOffset OccurredAt) : DomainEvent, IComplianceIncidentFact;
