using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Incidents.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Incidents.Events;

/// <summary>One bounded native incident fact with exact immutable business payload and original accepted response.</summary>
[Discriminator("bdgrz.incidents.incident-resolution-recorded", 3)]
public sealed record IncidentResolutionRecorded(Uuid TenantId, Uuid SubjectId, long Revision, Uuid? ActorMemberId,
    ComplianceIncidentAuthorityHeader Header, ImmutableContentRef CurrentContent, ImmutableContentRef Change, long NativeSequence,
    int ChangeCount, OperationProvenance Operation, ImmutableContentRef FirstResponse, ImmutableContentRef OperationResult,
    DateTimeOffset OccurredAt) : DomainEvent, IComplianceIncidentFact;
