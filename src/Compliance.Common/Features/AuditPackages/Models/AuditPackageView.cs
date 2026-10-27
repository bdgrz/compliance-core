using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One examination package stream with immutable package versions and retained delivery history.</summary>
public sealed record AuditPackageView(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid AuditPackageId,
    Uuid ExaminationId,
    string PackageKind,
    string Name,
    string Status,
    long Revision,
    int CurrentVersion,
    DateTimeOffset UpdatedAt,
    Uuid CreatedByMemberId,
    string CreatedByDisplay,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AuditPackageVersionView> Versions);
