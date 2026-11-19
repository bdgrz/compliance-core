using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Qualifies whether an exact resource read includes every fact through an area cut.</summary>
/// <remarks>
/// A true result means that the configured ordered event source yielded an
/// unfiltered contiguous area prefix through the cut, and each record for this
/// exact resource before it lies below the caller's next resource position.
/// The cut position must be observed; a quiet page does not prove it. The caller
/// validates the resource facts and business outcomes independently. This does
/// not certify a rollback or storage replacement.
/// </remarks>
public interface ICriteriaSourceCutCoverage
{
    /// <summary>Checks coverage of the exact completed resource read through the cut.</summary>
    ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset,
        ulong cutAreaOffset, CancellationToken ct = default);
}
