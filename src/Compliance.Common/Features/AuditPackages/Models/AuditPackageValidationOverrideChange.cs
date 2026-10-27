using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>One independent override decision on one exact validation.</summary>
public sealed record AuditPackageValidationOverrideChange(Uuid ValidationId, AuditPackageValidationOverrideView Override);
