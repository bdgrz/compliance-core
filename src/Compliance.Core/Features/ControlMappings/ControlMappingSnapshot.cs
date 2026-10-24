using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>Every mapping line of one program, in the order of their first proposals, and the fence they were read at.</summary>
public sealed record ControlMappingSnapshot(IReadOnlyList<ControlCriterionMappingView> Mappings, SourceFence Fence);
