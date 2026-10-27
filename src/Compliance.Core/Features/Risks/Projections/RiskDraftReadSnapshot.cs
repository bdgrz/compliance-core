using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Projections;

public sealed record RiskDraftReadSnapshot(RiskDraft Owner, RiskDraftRules Rules, RiskDraftRevisionView? Current)
{
    public bool IsCreated => Current is not null;
    public RiskDraftView? View => Current is null ? null : RiskDraftFactContent.View(Current);
}
