using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.AuditPackages.Models;

/// <summary>Packages for the authorized engagement or period.</summary>
public sealed record AuditPackageSummaryView(Uuid TenantId, Uuid ProgramId, Uuid AuditPackageId, string Name, string Status, DateTimeOffset UpdatedAt);
