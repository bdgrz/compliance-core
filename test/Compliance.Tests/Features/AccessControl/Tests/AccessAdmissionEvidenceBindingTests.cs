using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class AccessAdmissionEvidenceBindingTests
{
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Operation = Uuid.CreateVersion4();
    static readonly ActorAttribution Actor = new("actor", "issuer");
    static readonly string Digest = new('a', 64);
    const string Target = "stream://bdgrz/criteria/entry";

    [Fact]
    public void ShouldBindCriteriaEvidenceGivenExactIntentAndOperatorPath()
    {
        // Arrange
        var proof = Evidence() with { Operator = Source("operators") };

        // Act
        var result = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(
            proof, User, Actor, Operation, Target, Digest);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRejectEvidenceGivenDifferentTargetOrIntent()
    {
        // Arrange
        var proof = Evidence() with { Operator = Source("operators") };

        // Act
        var targetMismatch = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(
            proof, User, Actor, Operation, "stream://bdgrz/criteria/other", Digest);
        var digestMismatch = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(
            proof, User, Actor, Operation, Target, new string('b', 64));

        // Assert
        Assert.False(targetMismatch);
        Assert.False(digestMismatch);
    }

    [Fact]
    public void ShouldRejectOperatorEvidenceGivenMissingGrantOrUnversionedIdentity()
    {
        // Arrange
        var missingGrant = Evidence();
        var missingGeneration = Evidence() with
        {
            Operator = Source("operators"),
            Identity = new AccessAdmissionSourceVersion("stream://bdgrz/identities/user", "", 1)
        };

        // Act
        var withoutGrant = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(
            missingGrant, User, Actor, Operation, Target, Digest);
        var withoutGeneration = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(
            missingGeneration, User, Actor, Operation, Target, Digest);

        // Assert
        Assert.False(withoutGrant);
        Assert.False(withoutGeneration);
    }

    [Fact]
    public void ShouldRejectProgramEvidenceGivenIncompleteTeamPath()
    {
        // Arrange
        var proof = Evidence() with
        {
            Tenant = Source("tenant"),
            Member = Source("member"),
            TeamMember = Source("team-member"),
            Team = Source("team"),
            TeamRole = Source("team-role"),
            Role = Source("role")
        };

        // Act
        var result = AccessAdmissionEvidenceBinding.BindsProgramCreation(
            proof, User, Actor, Operation, Target, Digest);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldBindProgramEvidenceGivenOneCompletePrimaryPath()
    {
        // Arrange
        var proof = Evidence() with
        {
            Tenant = Source("tenant"),
            Member = Source("member"),
            Primary = Source("primary")
        };

        // Act
        var result = AccessAdmissionEvidenceBinding.BindsProgramCreation(
            proof, User, Actor, Operation, Target, Digest);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldRejectProgramSelectionEvidenceGivenDifferentOperation()
    {
        // Arrange
        var proof = Evidence() with
        {
            Tenant = Source("tenant"),
            Member = Source("member"),
            Primary = Source("primary")
        };

        // Act
        var result = AccessAdmissionEvidenceBinding.BindsProgramSelection(
            proof, User, Actor, Uuid.CreateVersion4(), Target, Digest);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldPreserveLegacyReplayGivenLegacyDataAndRejectItForNewAdmission()
    {
        // Arrange
        var legacy = Evidence() with { Operator = Source("operators") };

        // Act
        var replay = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(legacy, User, Actor, Operation, Target, Digest);
        var current = AccessAdmissionEvidenceBinding.IsCurrentFormat(legacy);

        // Assert
        Assert.True(replay);
        Assert.False(current);
    }

    [Theory]
    [InlineData(0, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(1, null)]
    [InlineData(1, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(1, "abc")]
    [InlineData(2, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ShouldRejectMalformedHistoricalAndNewEvidenceGivenInvalidVersionOrDigest(int version, string? digest)
    {
        // Arrange
        var evidence = Evidence() with { Operator = Source("operators"), FormatVersion = version, PolicyContentSha256 = digest };

        // Act
        var replay = AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, User, Actor, Operation, Target, Digest);
        var current = AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence);

        // Assert
        Assert.False(replay);
        Assert.False(current);
    }

    [Fact]
    public void ShouldAcceptCurrentAndReplayGivenVersionOnePolicyDigest()
    {
        // Arrange
        var evidence = Evidence() with { Operator = Source("operators"), FormatVersion = 1, PolicyContentSha256 = Digest };

        // Act
        // Assert
        Assert.True(AccessAdmissionEvidenceBinding.IsCurrentFormat(evidence));
        Assert.True(AccessAdmissionEvidenceBinding.BindsCriteriaOperator(evidence, User, Actor, Operation, Target, Digest));
    }

    static AccessAdmissionEvidence Evidence() => new(User, Actor.Subject, Actor.Issuer,
        Operation, Target, Digest, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero),
        "policy-v1", Source("identity"), Source("policy"));

    static AccessAdmissionSourceVersion Source(string name) =>
        new($"stream://bdgrz/access/{name}", "source-generation-1", 1);
}
