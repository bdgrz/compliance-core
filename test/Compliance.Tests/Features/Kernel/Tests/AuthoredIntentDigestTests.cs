using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Kernel.Tests;

public sealed class AuthoredIntentDigestTests
{
    static readonly Uuid Id = Uuid.Parse("11111111-2222-3333-4444-555555555555", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    static string Digest(DigestIntent intent) => AuthoredIntentDigest.Compute(intent, DigestIntentJsonContext.Default);

    [Fact]
    public void ShouldReturnSameDigestGivenEqualValues()
    {
        // Arrange
        var first = new DigestIntent(Id, "n", "note", 3, At, 1.5m);
        var second = new DigestIntent(Id, "n", "note", 3, At, 1.5m);

        // Act
        var a = Digest(first);
        var b = Digest(second);

        // Assert
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void ShouldReturnDifferentDigestGivenOptionalFlipsFromNullToValue()
    {
        // Arrange
        var absent = new DigestIntent(Id, "n", null, null, At, 1m);

        // Act
        var baseline = Digest(absent);
        var withEmptyNote = Digest(absent with { Note = "" });
        var withZeroRevision = Digest(absent with { Revision = 0 });

        // Assert
        Assert.NotEqual(baseline, withEmptyNote);
        Assert.NotEqual(baseline, withZeroRevision);
    }

    [Fact]
    public void ShouldNormalizeToUtcGivenOffsetTimestamp()
    {
        // Arrange
        var utc = new DigestIntent(Id, "n", null, null, At, 1m);
        var offset = utc with { At = At.ToOffset(TimeSpan.FromHours(-7)) };

        // Act
        var a = Digest(utc);
        var b = Digest(offset);

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void ShouldTreatDecimalsAsEqualGivenDifferentScale()
    {
        // Arrange
        var one = new DigestIntent(Id, "n", null, null, At, 1.0m);
        var sameValue = one with { Amount = 1.00m };

        // Act
        var a = Digest(one);
        var b = Digest(sameValue);

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void ShouldChangeDigestGivenAuthoredValueChanges()
    {
        // Arrange
        var intent = new DigestIntent(Id, "n", null, 3, At, 1m);

        // Act
        var baseline = Digest(intent);
        var renamed = Digest(intent with { Name = "m" });
        var laterRevision = Digest(intent with { Revision = 4 });

        // Assert
        Assert.NotEqual(baseline, renamed);
        Assert.NotEqual(baseline, laterRevision);
    }
}
