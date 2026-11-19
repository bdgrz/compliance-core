using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>Fails closed until the deployed source supplies a qualified coverage guarantee.</summary>
public sealed class UnavailableCriteriaSourceCutCoverage : ICriteriaSourceCutCoverage
{
    /// <inheritdoc />
    public ValueTask<bool> CoversAsync(EventStreamAddress stream, ulong nextResourceOffset,
        ulong cutAreaOffset, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}
