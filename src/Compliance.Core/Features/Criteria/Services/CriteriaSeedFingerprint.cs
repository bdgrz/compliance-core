using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bdgrz.Compliance.Features.Criteria.Models;

namespace Bdgrz.Compliance.Features.Criteria.Services;

/// <summary>
/// Binds one compiled seed release to its exact initial facts. This is a
/// compatibility guard, not proof that a durable edition is complete.
/// </summary>
static class CriteriaSeedFingerprint
{
    public const string Version = "bdgrz-tsc-2017-2022-v1";

    public static string Compute(CriteriaCatalogEdition edition, IEnumerable<Criterion> entries)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(entries);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "bdgrz.criteria-seed.fingerprint.v1");
        Append(hash, edition.EditionId.ToString());
        Append(hash, edition.Framework);
        Append(hash, edition.EditionLabel);
        Append(hash, edition.PublishedAt.ToString("O", CultureInfo.InvariantCulture));
        Append(hash, edition.IsComplete ? "true" : "false");
        Append(hash, edition.CoverageNote);
        Append(hash, edition.SourceUrl);
        Append(hash, edition.ContentRights);
        foreach (var gap in edition.SupportGaps)
        {
            Append(hash, "support-gap");
            Append(hash, gap.Category);
            Append(hash, gap.Code);
            Append(hash, gap.Note);
        }

        Append(hash, "entries");
        foreach (var entry in entries)
        {
            if (entry.EditionId != edition.EditionId || entry.LicensedText is not null || entry.Overlay is not null)
                throw new InvalidOperationException("A platform seed entry must belong to its edition and contain no licensed overlay.");
            Append(hash, "entry");
            Append(hash, entry.EditionId.ToString());
            Append(hash, entry.Identifier);
            Append(hash, entry.SourceIdentifier);
            Append(hash, entry.Category);
            Append(hash, entry.Kind);
            Append(hash, entry.ParentIdentifier);
            Append(hash, entry.Summary);
        }

        Append(hash, "end");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    static void Append(IncrementalHash hash, string? value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        if (value is null)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, -1);
            hash.AppendData(length);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
