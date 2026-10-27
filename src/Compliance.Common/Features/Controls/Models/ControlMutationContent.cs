using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl;

namespace Bdgrz.Compliance.Features.Controls.Models;

/// <summary>All business changes of one accepted command; successor/withdrawal changes publish atomically.</summary>
public sealed record ControlMutationContent(IReadOnlyList<IControlMutationContent> Changes,
    IReadOnlyList<SeparationOfDutiesWaiverProof>? Waivers = null, IControlMutationContent? Receipt = null);
