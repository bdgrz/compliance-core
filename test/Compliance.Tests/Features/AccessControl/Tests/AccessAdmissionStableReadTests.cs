using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessAdmissionStableReadTests
{
    const string Identity = "stream://bdgrz/identities/user";
    const string Policy = "stream://bdgrz/access-policy/current";
    static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldCaptureOverlappingSourcePositionsGivenStableQualifiedReads()
    {
        // Arrange
        var reader = new FakeReader(true, [Version(Identity, 1), Version(Policy, 3),
            Version(Identity, 1), Version(Policy, 3)]);
        var clock = new FixedClock(At, () => Assert.Equal(2, reader.Calls));
        var verifier = new AccessAdmissionStableRead(reader, clock);

        // Act
        var result = await verifier.CaptureAsync(Path(), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(At, result.AdmittedAt);
        Assert.Equal(4, reader.Calls);
        Assert.Equal((ulong)1, result.Observations[AccessAdmissionStableRead.Slot.Identity].Version.Position);
    }

    [Fact]
    public async Task ShouldRejectAnAbaChangeGivenEqualValuesAndDifferentPositions()
    {
        // Arrange
        var reader = new FakeReader(true, [Version(Identity, 1), Version(Policy, 3),
            Version(Identity, 3), Version(Policy, 3)]);
        var verifier = new AccessAdmissionStableRead(reader, new FixedClock(At));

        // Act
        var result = await verifier.CaptureAsync(Path(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRejectAdmissionGivenUnqualifiedFreshness()
    {
        // Arrange
        var reader = new FakeReader(false, [Version(Identity, 1), Version(Policy, 3)]);
        var verifier = new AccessAdmissionStableRead(reader, new FixedClock(At));

        // Act
        var result = await verifier.CaptureAsync(Path(), CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task ShouldRejectAdmissionGivenSourceGenerationChange()
    {
        // Arrange
        var reader = new FakeReader(true, [Version(Identity, 1), Version(Policy, 3),
            Version(Identity, 1, "generation-2"), Version(Policy, 3)]);
        var verifier = new AccessAdmissionStableRead(reader, new FixedClock(At));

        // Act
        var result = await verifier.CaptureAsync(Path(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ShouldRejectAdmissionGivenMissingRequiredParticipant()
    {
        // Arrange
        var reader = new FakeReader(true, [Version(Identity, 1)]);
        var verifier = new AccessAdmissionStableRead(reader, new FixedClock(At));

        // Act
        var result = await verifier.CaptureAsync(
            [new(AccessAdmissionStableRead.Slot.Identity, Identity)], CancellationToken.None);

        // Assert
        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task ShouldRejectAdmissionGivenSourceLosesQualificationDuringReads()
    {
        // Arrange
        var reader = new LosingQualificationReader();
        var verifier = new AccessAdmissionStableRead(reader, new FixedClock(At));

        // Act
        var result = await verifier.CaptureAsync(Path(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    static AccessAdmissionStableRead.Participant[] Path() =>
    [
        new(AccessAdmissionStableRead.Slot.Identity, Identity),
        new(AccessAdmissionStableRead.Slot.Policy, Policy)
    ];

    static AccessAdmissionStableRead.Observation Version(string stream, ulong position,
        string generation = "generation-1") =>
        new(new AccessAdmissionSourceVersion(stream, generation, position), "unchanged-state");

    sealed class FixedClock(DateTimeOffset now, Action? beforeRead = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            beforeRead?.Invoke();
            return now;
        }
    }

    sealed class FakeReader(bool qualified, AccessAdmissionStableRead.Observation[] values)
        : AccessAdmissionStableRead.ISource
    {
        public bool IsFreshnessQualified => qualified;
        public int Calls { get; private set; }

        public ValueTask<AccessAdmissionStableRead.Observation?> ReadCurrentAsync(string stream,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var value = Calls < values.Length ? values[Calls] : null;
            Calls++;
            return ValueTask.FromResult(value);
        }
    }

    sealed class LosingQualificationReader : AccessAdmissionStableRead.ISource
    {
        int _calls;

        public bool IsFreshnessQualified => _calls < 4;

        public ValueTask<AccessAdmissionStableRead.Observation?> ReadCurrentAsync(string stream,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _calls++;
            return ValueTask.FromResult<AccessAdmissionStableRead.Observation?>(Version(stream, 1));
        }
    }
}
