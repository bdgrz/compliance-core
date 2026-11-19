namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>The source-verified result of one fixed seed operation.</summary>
enum CriteriaSeedWriteStatus
{
    Unknown = 0,
    Confirmed = 1,
    Pending = 2,
    Conflict = 3,
}
