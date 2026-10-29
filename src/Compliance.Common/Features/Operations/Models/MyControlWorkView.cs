using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>The acting member's current and upcoming responsibilities and ordered work.</summary>
public sealed record MyControlWorkView(IReadOnlyList<ControlResponsibilityView> Responsibilities,
    IReadOnlyList<WorkItemView> Items);
