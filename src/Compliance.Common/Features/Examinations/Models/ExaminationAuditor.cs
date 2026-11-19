using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>The auditor is explicitly unknown, external, or our named attest engagement.</summary>
public sealed record ExaminationAuditor(string Kind, Uuid? ServiceEngagementId, string? FirmName);
