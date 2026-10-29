namespace Bdgrz.Compliance.Features.Programs.Models;

public sealed record ProgramPlan(DateOnly? TargetReadinessDate, DateOnly? TargetTypeIAsOfDate,
    DateOnly? TargetTypeIIStartDate, DateOnly? TargetTypeIIEndDate,
    string? ReadinessAdvisor, string? AuditFirm);
