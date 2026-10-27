using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ResponsibilityScopedIdentityTests
{
    [Fact]
    public async Task ShouldRetainBothNativeScopesAndRevokeOnlyTargetGivenReusedLocalAssignmentCommand()
    {
        // Arrange
        await using var native = new CommitmentProjectionScenario();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var shared = Uuid.CreateVersion4();
        var member = Uuid.CreateVersion4();
        var administrator = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var drafts = new List<CommitmentRules>();
        foreach (var identifier in new[] { "SC-ONE", "SC-TWO" })
        {
            var draft = CommitmentDraft.IdFor(tenant, program, "service_commitment", identifier);
            var rules = new CommitmentRules(tenant, draft);
            Assert.True(rules.Create(program, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", identifier,
                "Protect customer data", "Security commitment", "MSA 4.1", Uuid.CreateVersion4(), "Author", now).IsSuccess);
            var scope = new ResponsibilityScope("commitment", draft, draft, 1);
            Assert.Null(rules.AssignResponsibility(scope, shared, member, ResponsibilityType.AssignedReviewer,
                administrator, "Administrator", now, now, null, []));
            await native.PublishAsync(rules);
            drafts.Add(rules);
        }
        var assigned = (await native.ReadAsync(tenant)).OfType<ResponsibilityAssigned>().ToArray();
        Assert.Equal(2, assigned.Length);
        Assert.All(assigned, fact => Assert.NotNull(fact.Operation));
        var index = new FitzMemberResponsibilityIndex(native.Kv);
        var identity = new CheckpointIdentity(FitzMemberResponsibilityIndex.ProjectorName, EventStreamPattern.ForPattern(tenant.ToString()));

        // Act
        await ProjectAsync(index, identity, assigned);
        var before = await index.GetAsync(tenant, member);
        var firstScope = assigned.Single(fact => fact.Scope.RecordId == drafts[0].Id).Scope;
        Assert.Null(drafts[0].RevokeResponsibility(firstScope, shared, administrator, "Administrator", now.AddMinutes(1), "Transferred"));
        var revoked = Assert.Single((await native.PublishAsync(drafts[0])).OfType<ResponsibilityRevoked>());
        await ProjectAsync(index, identity, [revoked]);
        var after = await index.GetAsync(tenant, member);

        // Assert
        Assert.Equal(2, before.Count);
        Assert.Equal(2, before.Select(assignment => assignment.Scope).Distinct().Count());
        Assert.All(before, assignment => Assert.Equal(shared, assignment.AssignmentId));
        Assert.Equal(2, after.Count);
        Assert.NotNull(after.Single(assignment => assignment.Scope == firstScope).RevokedAt);
        Assert.Null(after.Single(assignment => assignment.Scope != firstScope).RevokedAt);
        Assert.Empty(await index.GetAsync(Uuid.CreateVersion4(), member));
    }

    static async Task ProjectAsync(FitzMemberResponsibilityIndex index, CheckpointIdentity identity,
        IReadOnlyList<DomainEvent> facts)
    {
        await using var batch = await index.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        foreach (var fact in facts)
            await index.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
