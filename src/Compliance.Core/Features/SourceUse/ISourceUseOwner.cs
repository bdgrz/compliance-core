using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SourceUse;

/// <summary>An owner that embeds <see cref="SourceUseReservations" /> and accepts native use facts.</summary>
public interface ISourceUseOwner
{
    /// <summary>The owner's own stream.</summary>
    EventStreamAddress Stream { get; }

    /// <summary>The active grants, receiver generation and fences of the owner.</summary>
    SourceUseReservations Uses { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    NativeAcceptance Acceptance { get; }
}
