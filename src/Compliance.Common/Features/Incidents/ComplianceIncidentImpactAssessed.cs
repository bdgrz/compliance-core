using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     The accepted incident impact assessment. The fact is the native acceptance of the logical <paramref name="CommandId" />
///     on the incident stream.
/// </summary>
[Discriminator("bdgrz.incidents.compliance-incident-impact-assessed", 2)]
public sealed record ComplianceIncidentImpactAssessed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ComplianceIncidentView State,
    DateTimeOffset OccurredAt,
    Uuid CommandId,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
