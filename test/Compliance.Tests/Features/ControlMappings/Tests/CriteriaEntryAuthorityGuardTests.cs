using Bdgrz.Compliance.Features.ControlMappings.Services;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;

public sealed class CriteriaEntryAuthorityGuardTests
{
    [Fact]
    public async Task ShouldKeepDecisionPendingGivenEntryOutcomeIsUnresolved()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var authority = new FixedAuthority(new(CriteriaEntryAuthorityStatus.Pending, null));

        // Act
        var result = await CriteriaEntryAuthorityGuard.RequireAsync(authority, edition, "CC6.1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.True(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseGivenSourceProvesEntryIsNotPublishedMember()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var authority = new FixedAuthority(new(CriteriaEntryAuthorityStatus.NotMember, null));

        // Act
        var result = await CriteriaEntryAuthorityGuard.RequireAsync(authority, edition, "CC6.1");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.False(result.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseGivenPointOfFocusLacksExactParentEvidence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var entry = new Criterion(edition, "bdgrz:focus:one", null, "security", "point_of_focus",
            "CC6.1", "The system protects access.");
        var proof = new CriteriaEntryProof(entry, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, null);
        var authority = new FixedAuthority(new(CriteriaEntryAuthorityStatus.Accepted, proof));

        // Act
        var result = await CriteriaEntryAuthorityGuard.RequireAsync(authority, edition, entry.Identifier);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public async Task ShouldReturnCriterionGivenExactAcceptedSourceEvidence()
    {
        // Arrange
        var edition = Uuid.CreateVersion4();
        var entry = new Criterion(edition, "CC6.1", "CC6.1", "security", "criterion", null,
            "The system protects access.");
        var proof = new CriteriaEntryProof(entry, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, null, null);
        var authority = new FixedAuthority(new(CriteriaEntryAuthorityStatus.Accepted, proof));

        // Act
        var result = await CriteriaEntryAuthorityGuard.RequireAsync(authority, edition, entry.Identifier);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(entry, result.Value);
    }

    sealed class FixedAuthority(CriteriaEntryAuthorityResult value) : ICriteriaEntryAuthority
    {
        public ValueTask<Result<CriteriaEntryAuthorityResult>> ReadAsync(Uuid editionId, string identifier,
            CancellationToken ct = default) => ValueTask.FromResult(Result<CriteriaEntryAuthorityResult>.Success(value));
    }
}
