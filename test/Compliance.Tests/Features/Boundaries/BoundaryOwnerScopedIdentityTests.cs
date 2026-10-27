using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class BoundaryOwnerScopedIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepBothOwnersDiscoverableGivenReusedLocalDecisionOrSuccessorIdentity(bool sharedVersion)
    {
        // Arrange
        await using var native = new BoundaryProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var boundaries = new[] { Uuid.CreateVersion4(), Uuid.CreateVersion4() };
        var command = Uuid.CreateVersion4();
        var shared = sharedVersion ? Uuid.CreateVersion5(command, "draft") : command;
        var author = Uuid.CreateVersion4();
        var reviewer = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        foreach (var boundary in boundaries)
        {
            var draft = Uuid.CreateVersion5(boundary, "draft-1");
            var rules = new BoundaryRules(tenant, boundary);
            Assert.True(rules.Create(program, draft, new BoundaryContent("Scope", "readiness", ["security"], []),
                author, "Author", now).IsSuccess);
            var review = sharedVersion ? Uuid.CreateVersion4() : shared;
            Assert.Null(rules.Review(draft, 1, review, "accept", "Independent review", reviewer, "Reviewer", now.AddMinutes(1)));
            if (sharedVersion)
            {
                Assert.Null(rules.Approve(draft, 1, Uuid.CreateVersion4(), review, new DateOnly(2027, 1, 1),
                    "Approved", "digest", approver, "Approver", now.AddMinutes(2)));
                Assert.Null(rules.ProposeSuccessor(draft, shared, new BoundaryContent("Successor scope", "readiness", ["security"], []),
                    author, "Author", now.AddMinutes(3), command));
                review = Uuid.CreateVersion4();
                Assert.Null(rules.Review(shared, 1, review, "accept", "Independent successor review", reviewer, "Reviewer", now.AddMinutes(4)));
                Assert.Null(rules.Approve(shared, 1, Uuid.CreateVersion4(), review, new DateOnly(2027, 2, 1),
                    "Approved", "digest", approver, "Approver", now.AddMinutes(5)));
            }
            await native.PublishAsync(rules);
        }
        var directory = new FitzBoundaryDirectory(native.Kv, native.Boundaries);

        // Act
        await native.ProjectAsync(directory, directory.ApplyAsync, FitzBoundaryDirectory.ProjectorName, tenant);

        // Assert
        foreach (var boundary in boundaries)
        {
            if (sharedVersion)
                Assert.Equal(boundary, (await directory.GetVersionAsync(tenant, boundary, shared))?.BoundaryId);
            else
                Assert.Equal(boundary, (await directory.GetDecisionAsync(tenant, boundary, shared))?.BoundaryId);
        }
    }
}
