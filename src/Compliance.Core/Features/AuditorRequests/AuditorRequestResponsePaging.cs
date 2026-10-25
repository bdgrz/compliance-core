using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Affines continuation to the exact scoped submitted view set and page size; each call still requires current auth.</summary>
static class AuditorRequestResponsePaging
{
    const string Prefix = "auditor_response_v1:";

    public static Result<Page<AuditorRequestResponseView>> Create(Uuid tenant, Uuid program, Uuid request,
        IReadOnlyList<AuditorRequestResponseView> values, int limit, string? cursor)
    {
        var rows = values.OrderByDescending(static row => row.SubmittedAt)
            .ThenBy(static row => row.AuditorRequestResponseId.ToString(), StringComparer.Ordinal).ToArray();
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
            rows.Select(static row => ContentDigest.OfJson(row))))));
        var scope = $"{tenant}\n{program}\n{request}\n{limit.ToString(CultureInfo.InvariantCulture)}\n";
        var offset = 0;
        if (cursor is not null)
        {
            if (cursor.Length > 2048 || !cursor.StartsWith(Prefix, StringComparison.Ordinal))
                return Invalid();
            try
            {
                var value = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(cursor[Prefix.Length..]));
                if (!value.StartsWith(scope, StringComparison.Ordinal))
                    return Invalid();
                var parts = value[scope.Length..].Split('\n');
                if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out offset) ||
                    offset < 0 || offset > rows.Length)
                    return Invalid();
                if (parts[0] != fingerprint)
                    return Result<Page<AuditorRequestResponseView>>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The submitted responses changed; restart paging.", true));
            }
            catch (FormatException) { return Invalid(); }
            catch (DecoderFallbackException) { return Invalid(); }
        }
        var page = rows.Skip(offset).Take(limit).ToArray();
        var next = offset + page.Length;
        var continuation = next < rows.Length ? Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(scope + fingerprint + "\n" +
            next.ToString(CultureInfo.InvariantCulture))) : null;
        return Result<Page<AuditorRequestResponseView>>.Success(new Page<AuditorRequestResponseView>(page, continuation));
    }

    static Result<Page<AuditorRequestResponseView>> Invalid() => Result<Page<AuditorRequestResponseView>>.Failure(
        new RequestError(RequestErrorKind.Validation, "The request-response cursor is invalid for this scope or page size."));
}
