using System.Text;

namespace Bdgrz.Compliance.Features.Kernel;

/// <summary>Orders keys by unsigned UTF-8 byte comparison, which differs from UTF-16 ordinal for supplementary characters.</summary>
public sealed class StreamKeyOrder : IComparer<string>
{
    /// <summary>The shared comparer.</summary>
    public static StreamKeyOrder Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return Encoding.UTF8.GetBytes(x).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y));
    }
}
