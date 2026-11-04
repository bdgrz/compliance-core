using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>Computed result and the exact complete source set used to reach it.</summary>
public sealed record IndependenceAssessmentView(Uuid TenantId, long RuleSetVersion,
    DateOnly ExaminationPeriodStart, DateOnly ExaminationPeriodEnd, string Result,
    IReadOnlyList<NonattestServiceVersion> ServicesConsidered);
