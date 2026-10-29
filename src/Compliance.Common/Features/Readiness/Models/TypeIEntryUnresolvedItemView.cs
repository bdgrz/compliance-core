using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Models;

/// <summary>One gap frozen into a Type I entry decision with its plan at decision time.</summary>
public sealed record TypeIEntryUnresolvedItemView(Uuid GapId, string Kind, string Subject,
    string RuleId, string Explanation, Uuid? OwnerMemberId, DateOnly? TargetDate,
    bool Acknowledged);
