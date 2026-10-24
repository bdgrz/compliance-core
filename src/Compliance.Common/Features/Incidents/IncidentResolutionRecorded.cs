using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Incidents;

/// <summary>
///     The accepted incident resolution. The fact is the native acceptance of the logical <paramref name="CommandId" />
///     on the incident stream.
/// </summary>
[Discriminator("bdgrz.incidents.incident-resolution-recorded", 2)]
public sealed record IncidentResolutionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ComplianceIncidentView State,
    DateTimeOffset OccurredAt,
    Uuid CommandId,
    OperationProvenance Operation,
    ImmutableContentRef? FirstResponse) : DomainEvent, INativeOperationFact;
