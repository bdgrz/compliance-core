using Cntryl.Fitz.Extensions;

namespace Bdgrz.Compliance;

public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor)
{
    public static implicit operator Page<T>(KvDirectoryPage<T> page) => new(page.Items, page.NextCursor);
}
