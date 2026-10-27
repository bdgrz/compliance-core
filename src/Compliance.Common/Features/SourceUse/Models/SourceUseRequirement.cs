using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.SourceUse.Models;

/// <summary>
///     The exact business fact a use pins on its source owner. The pinned content event and revision stay the held
///     fact even when later use events advance the owner's physical version. Values are extracted by the server from
///     the native event and its content, never supplied by a caller as policy.
/// </summary>
public sealed record SourceUseRequirement(EventStreamAddress SourceOwner, NativeFactReference ContentEvent,
    ulong ResourceRevision, SourceUsePurpose Purpose, ImmutableContentRef RequiredFacts)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "source-use-requirement.v1";
}
