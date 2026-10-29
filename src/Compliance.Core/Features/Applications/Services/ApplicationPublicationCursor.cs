using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.Applications.Services;

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
