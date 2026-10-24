namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The complete, immutable selection of one generation: every selected occurrence in canonical identity order,
///     with no count cap.
/// </summary>
public sealed record ControlOccurrenceSelectionManifest(IReadOnlyList<ControlOccurrenceSelection> Selections)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "DueControlOccurrencesGenerated.CreationIntentManifest.v1";
}
