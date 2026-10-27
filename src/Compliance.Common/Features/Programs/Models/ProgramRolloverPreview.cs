using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>Carry-forward candidates and unresolved dependencies before proposal.</summary>
public sealed record ProgramRolloverPreview(Uuid TenantId, Uuid ProgramId, string Summary, IReadOnlyList<Uuid> AffectedRecordIds, DateTimeOffset CalculatedAt);
