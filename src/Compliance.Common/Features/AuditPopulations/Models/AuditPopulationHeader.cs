using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Bounded current authority; complete rows and historical copies reside in verified immutable content.</summary>
public sealed record AuditPopulationHeader(Uuid TenantId, Uuid ProgramId, Uuid PopulationId, Uuid DefinitionId,
    Uuid PeriodId, Uuid ExaminationId, string Status, long Revision, int DefinitionVersion, int PopulationVersion,
    int RowCount, int SourceCount, Uuid CloseSnapshotId, string CloseSnapshotSha256, bool SampleEligible,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset UpdatedAt);
