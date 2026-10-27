namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Authored narrative content and its exact source and period-change references.</summary>
public sealed record SystemDescriptionDraftContent(string Title,
    IReadOnlyList<SystemDescriptionSection> Sections,
    IReadOnlyList<SystemDescriptionSourceReference> Sources,
    IReadOnlyList<SystemDescriptionPeriodChange> PeriodChanges,
    IReadOnlyList<Cntryl.Portia.Uuid> SignificantChangeIds);
