using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>One evaluated change inside a Type II period.</summary>
public sealed record SystemDescriptionPeriodChange(Uuid ChangeId, bool IsMaterial,
    string AssessmentSha256);
