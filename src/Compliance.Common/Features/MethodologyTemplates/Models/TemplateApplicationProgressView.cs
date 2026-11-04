using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Current canonical application plan and terminal entry totals from the ordering owner.</summary>
public sealed record TemplateApplicationProgressView(Uuid TenantId, Uuid ProgramId, Uuid TemplateId,
    long TemplateVersion, long PreviousVersion, Uuid ApplicationCommandId, Uuid AppliedBy,
    IReadOnlyList<string> EntriesToCreate, IReadOnlyList<string> EntriesToPropose,
    bool IsCompleted, TemplateApplicationOutcomeCounts? Counts);
