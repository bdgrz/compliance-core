using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Exact program link between one available artifact and the record revision it supports.</summary>
public sealed record EvidenceSupportView(Uuid TenantId, Uuid ProgramId, Uuid EvidenceSupportId,
    Uuid ArtifactId, long ArtifactRevision, string ArtifactContentSha256,
    string SupportedRecordType, Uuid SupportedRecordId, long SupportedRecordRevision,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Purpose, string SufficiencyRationale,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset LinkedAt);
