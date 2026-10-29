using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Evidence.Models;

/// <summary>Exact bounded SoD exception provenance, without the administrative rationale.</summary>
public sealed record EvidenceRedactionWaiverSnapshot(Uuid TenantId, Uuid WaiverId,
    SeparationOfDutiesWaiverScope Scope, Uuid BeneficiaryMemberId, Uuid RequesterMemberId,
    Uuid ApproverMemberId, DateTimeOffset RequestedAt, DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt);
