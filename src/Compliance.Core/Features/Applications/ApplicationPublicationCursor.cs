using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>
///     Carries the exclusive global watermark of a publication cut inside a list cursor, so every later page of one
///     logical read observes the same frontier as the first. The inner cursor keeps its own format and validation.
/// </summary>
static class ApplicationPublicationCursor
{
    const string Prefix = "publication_cut_v1:";

    /// <summary>Wraps the next page's inner cursor with the watermark; no further page needs no cursor.</summary>
    public static string? Wrap(ulong watermark, string? inner) => inner is null
        ? null
        : $"{Prefix}{watermark.ToString(CultureInfo.InvariantCulture)}:{inner}";

    /// <summary>Splits a request cursor into its watermark (absent on the first page) and the inner cursor.</summary>
    public static Result<(ulong? Watermark, string? Inner)> Unwrap(string? cursor)
    {
        if (cursor is null || !cursor.StartsWith(Prefix, StringComparison.Ordinal))
            return Result<(ulong?, string?)>.Success((null, cursor));
        var rest = cursor[Prefix.Length..];
        var separator = rest.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 && separator < rest.Length - 1 &&
               ulong.TryParse(rest[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var watermark)
            ? Result<(ulong?, string?)>.Success((watermark, rest[(separator + 1)..]))
            : Result<(ulong?, string?)>.Failure(new RequestError(RequestErrorKind.Validation,
                "The publication cursor is invalid."));
    }
}
