using Bdgrz.Compliance.Features.Criteria.Services;

namespace Bdgrz.Compliance.Tests.Features.Criteria.Tests;

public sealed class PlatformCriteriaSeedStreamTests
{
    [Fact]
    public void ShouldPreserveEveryLegacySeedEntryGivenStreamingSeed()
    {
        // Arrange
        var catalog = CriteriaCatalog.Platform;

        // Act
        var streamed = PlatformCriteriaCatalog.EnumerateSeedEntries().ToArray();

        // Assert
        Assert.Equal(catalog.Entries.OrderBy(static entry => entry.Identifier, StringComparer.Ordinal),
            streamed.OrderBy(static entry => entry.Identifier, StringComparer.Ordinal));
        Assert.Equal(61, streamed.Count(static entry => entry.Kind == "criterion"));
        Assert.Equal(6, streamed.Count(static entry => entry.Kind == "point_of_focus"));
    }

    [Fact]
    public void ShouldPreserveSeedMetadataGivenStreamingSeed()
    {
        // Arrange
        var expected = CriteriaCatalog.Platform.Edition;

        // Act
        var actual = PlatformCriteriaCatalog.SeedEdition();

        // Assert
        Assert.Equal(expected.EditionId, actual.EditionId);
        Assert.Equal(expected.EditionLabel, actual.EditionLabel);
        Assert.Equal(expected.PublishedAt, actual.PublishedAt);
        Assert.Equal(expected.ContentRights, actual.ContentRights);
        Assert.Equal(expected.CoverageNote, actual.CoverageNote);
        Assert.Equal(expected.SupportGaps, actual.SupportGaps);
        Assert.Contains(actual.SupportGaps, static gap =>
            gap.Category == "privacy" && gap.Code == "privacy_lifecycle_unsupported");
    }

    [Fact]
    public void ShouldBindSeedContentGivenIdenticalEditionAndEntries()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();

        // Act
        var first = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var second = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());

        // Assert
        Assert.Equal(first, second);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void ShouldChangeFingerprintGivenSeedEntryChangesUnderSameEdition()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var entries = PlatformCriteriaCatalog.EnumerateSeedEntries().ToArray();
        var changed = entries.Select((entry, index) => index == 0
            ? entry with { Summary = entry.Summary + " Changed." }
            : entry);

        // Act
        var original = CriteriaSeedFingerprint.Compute(edition, entries);
        var revised = CriteriaSeedFingerprint.Compute(edition, changed);

        // Assert
        Assert.NotEqual(original, revised);
    }

    [Fact]
    public void ShouldChangeFingerprintGivenSeedDisclosureChanges()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var altered = edition with { CoverageNote = edition.CoverageNote + " Changed." };

        // Act
        var original = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var revised = CriteriaSeedFingerprint.Compute(altered, PlatformCriteriaCatalog.EnumerateSeedEntries());

        // Assert
        Assert.NotEqual(original, revised);
    }

    [Fact]
    public void ShouldRejectLicensedTextGivenPlatformSeedFingerprint()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var entries = PlatformCriteriaCatalog.EnumerateSeedEntries().ToArray();
        entries[0] = entries[0] with { LicensedText = "A licensed overlay must never be shipped." };

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => CriteriaSeedFingerprint.Compute(edition, entries));
    }

    [Fact]
    public void ShouldRejectChangedSeedEntryGivenValidReleaseBinding()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var original = PlatformCriteriaCatalog.EnumerateSeedEntries().First();
        var changed = original with { Summary = "A different initial summary." };

        // Act and Assert
        Assert.True(PlatformCriteriaSeedDefinition.MatchesEntry(edition.EditionId,
            CriteriaSeedFingerprint.Version, fingerprint, original));
        Assert.False(PlatformCriteriaSeedDefinition.MatchesEntry(edition.EditionId,
            CriteriaSeedFingerprint.Version, fingerprint, changed));
    }

    [Fact]
    public void ShouldRejectAnotherFingerprintGivenARealSeedEntry()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var entry = PlatformCriteriaCatalog.EnumerateSeedEntries().First();

        // Act
        var accepted = PlatformCriteriaSeedDefinition.MatchesEntry(edition.EditionId,
            CriteriaSeedFingerprint.Version, new string('0', 64), entry);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public void ShouldRejectChangedMetadataGivenValidReleaseBinding()
    {
        // Arrange
        var edition = PlatformCriteriaCatalog.SeedEdition();
        var fingerprint = CriteriaSeedFingerprint.Compute(edition, PlatformCriteriaCatalog.EnumerateSeedEntries());
        var header = new CriteriaSeedHeader(edition.EditionId, edition.Framework, edition.EditionLabel,
            edition.PublishedAt, edition.IsComplete, edition.CoverageNote, edition.SourceUrl,
            edition.ContentRights, CriteriaSeedFingerprint.Version, fingerprint);

        // Act and Assert
        Assert.True(PlatformCriteriaSeedDefinition.MatchesHeader(header));
        Assert.False(PlatformCriteriaSeedDefinition.MatchesHeader(header with { CoverageNote = "No support gaps." }));
    }
}
