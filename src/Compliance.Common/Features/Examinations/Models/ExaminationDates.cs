
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Unknown dates remain null; a Type I as-of date is separate from Type II dates.</summary>
public sealed record ExaminationDates(DateOnly? AsOfDate, DateOnly? PeriodStart, DateOnly? PeriodEnd);
