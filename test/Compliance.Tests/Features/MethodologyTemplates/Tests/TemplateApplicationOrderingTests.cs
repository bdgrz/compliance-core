using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.MethodologyTemplates.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Authorizers;
using Bdgrz.Compliance.Features.MethodologyTemplates.Commands;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Tests.Testing;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;

namespace Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;

public sealed class TemplateApplicationOrderingTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly Uuid Template = Uuid.CreateVersion4();
    static readonly Uuid Actor = Uuid.CreateVersion4();
    static readonly ImmutableContentRef Manifest = new(Uuid.CreateVersion4(), new string('a', 64), 20,
        "application/json", MethodologyTemplateDraft.EntriesSchema);

    [Fact]
    public void ShouldRefuseNextVersionWithoutFactGivenPreviousVersionIncomplete()
    {
        // Arrange
        var scenario = Scenario();
        Assert.True(Apply(scenario.Aggregate, Uuid.CreateVersion4(), 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = Apply(scenario.Aggregate, Uuid.CreateVersion4(), 2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(1, scenario.Aggregate.LastAppliedVersion);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldApplyIncreasingVersionsGivenMatchingCompletionAndKeepOldRetryFactFree()
    {
        // Arrange
        var scenario = Scenario();
        var first = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, first, 1).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteVersion(first, 1, new(1, 0, 0), Uuid.CreateVersion4()).IsSuccess);

        // Act
        var next = Apply(scenario.Aggregate, Uuid.CreateVersion4(), 3);
        var beforeRetry = scenario.PendingEvents.Count;
        var oldRetry = Apply(scenario.Aggregate, first, 1);
        var changedRetry = Apply(scenario.Aggregate, first, 2);

        // Assert
        Assert.True(next.IsSuccess);
        Assert.True(oldRetry.IsSuccess);
        Assert.False(changedRetry.IsSuccess);
        Assert.Equal(3, scenario.Aggregate.LastAppliedVersion);
        Assert.False(scenario.Aggregate.IsVersionCompleted);
        Assert.Equal(beforeRetry, scenario.PendingEvents.Count);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    public void ShouldRefuseInvalidTotalsWithoutFactGivenOnePlannedEntry(int created, int proposed, int refused)
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var result = scenario.Aggregate.CompleteVersion(command, 1,
            new(created, proposed, refused), Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.False(scenario.Aggregate.IsVersionCompleted);
        Assert.Equal(before, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldRefuseWrongCompletionAndChangedRetryGivenExactVersionBinding()
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        var completion = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        var before = scenario.PendingEvents.Count;

        // Act
        var wrong = scenario.Aggregate.CompleteVersion(Uuid.CreateVersion4(), 1, new(1, 0, 0), completion);
        var accepted = scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), completion);
        var retry = scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), completion);
        var changed = scenario.Aggregate.CompleteVersion(command, 1, new(0, 1, 0), completion);

        // Assert
        Assert.False(wrong.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(before + 1, scenario.PendingEvents.Count);
    }

    [Fact]
    public void ShouldReplayOrderingAndCompletionGivenRetainedFacts()
    {
        // Arrange
        var scenario = Scenario();
        var command = Uuid.CreateVersion4();
        Assert.True(Apply(scenario.Aggregate, command, 1).IsSuccess);
        Assert.True(scenario.Aggregate.CompleteVersion(command, 1, new(1, 0, 0), Uuid.CreateVersion4()).IsSuccess);
        var replay = Scenario().Given(scenario.PendingEvents.ToArray());

        // Act
        var result = Apply(replay.Aggregate, Uuid.CreateVersion4(), 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(replay.Aggregate.IsVersionCompleted);
        Assert.Equal(1, replay.Aggregate.LastAppliedVersion);
        Assert.Empty(replay.PendingEvents);
    }

    [Fact]
    public void ShouldDeriveOneTenantProgramTemplateIdentityGivenCanonicalApplication()
    {
        // Arrange
        var expected = TemplateApplication.CanonicalIdentity(Tenant, Program, Template);

        // Act
        var owner = new TemplateApplication(Tenant, Program, Template, true);
        var other = TemplateApplication.CanonicalIdentity(Uuid.CreateVersion4(), Program, Template);

        // Assert
        Assert.Equal(expected, owner.Id);
        Assert.NotEqual(expected, other);
    }

    [Fact]
    public void ShouldFreezeProposalBaseAndReplayItGivenDestinationDispatchMayRetry()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var scenario = new AggregateScenario<TemplateVersionApplication>(new TemplateVersionApplication(tenant, Program, Template, 1));
        Assert.True(scenario.Aggregate.Open(Uuid.CreateVersion4(), ["control"], Uuid.CreateVersion4()).IsSuccess);
        var basis = new TemplateEntryProposalBase("control", "control", Uuid.CreateVersion4(), Uuid.CreateVersion4(), null);

        // Act
        var prepared = scenario.Aggregate.PrepareProposal(basis);
        var count = scenario.PendingEvents.Count;
        var retry = scenario.Aggregate.PrepareProposal(basis);
        var changed = scenario.Aggregate.PrepareProposal(basis with { ApprovedVersionId = Uuid.CreateVersion4() });
        var replay = new AggregateScenario<TemplateVersionApplication>(new TemplateVersionApplication(tenant, Program, Template, 1))
            .Given(scenario.PendingEvents.ToArray());

        // Assert
        Assert.True(prepared.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(count, scenario.PendingEvents.Count);
        Assert.Equal(basis, replay.Aggregate.PreparedProposalFor("control"));
        Assert.Null(replay.Aggregate.OutcomeFor("control"));
    }

    [Fact]
    public void ShouldPlanFullDifferencesGivenSkippedPublicationAndRemovedEntry()
    {
        // Arrange
        IReadOnlyList<MethodologyTemplateRecordContent> previous =
        [
            new("changed", "control", "Original", "Old summary", []),
            new("unchanged", "policy", "Stable", "Same summary", []),
            new("removed", "risk", "Retained client content", "No overwrite", []),
        ];
        IReadOnlyList<MethodologyTemplateRecordContent> selected =
        [
            new("changed", "control", "New content", "Changed two publications ago", []),
            new("unchanged", "policy", "Stable", "Same summary", []),
            new("added", "risk", "New draft", "New summary", []),
        ];

        // Act
        var plan = Bdgrz.Compliance.Features.MethodologyTemplates.Services.TemplateApplicationEntries.Plan(previous, selected);

        // Assert
        Assert.Equal(["added"], plan.Create);
        Assert.Equal(["changed"], plan.Propose);
    }

    [Theory]
    [InlineData("client_personnel", false, false, true, true)]
    [InlineData("client_personnel", false, false, false, false)]
    [InlineData("firm_staff", false, false, true, false)]
    [InlineData("guest", false, false, true, false)]
    [InlineData("client_personnel", true, false, true, false)]
    [InlineData("client_personnel", false, true, true, false)]
    public async Task ShouldRequireCurrentClientLeadGivenApplicationActor(string affiliation, bool suspended,
        bool ended, bool lead, bool allowed)
    {
        // Arrange
        var request = new ApplyMethodologyTemplateV4(Uuid.CreateVersion4(), Tenant, Program, Template, 1);
        var membership = new Membership(new TenantMembershipView(Actor, Tenant, affiliation, suspended, IsDeprovisioned: ended));
        var authorizer = new ApplyMethodologyTemplateV4Authorizer(membership, new Permissions(lead), TimeProvider.System);

        // Act
        var result = await authorizer.AuthorizeAsync(MethodologyTemplateScenario.Context(request, Actor), CancellationToken.None);

        // Assert
        Assert.Equal(allowed, result.IsSuccess);
    }

    sealed class Membership(TenantMembershipView member) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(member);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(true);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    sealed class Permissions(bool lead) : IAccessGrantRolePermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsRoleAsync(Uuid tenantId, Uuid userId, Uuid memberId, Uuid programId,
            Uuid roleId, string permission, CancellationToken ct = default) => ValueTask.FromResult(lead);
    }

    static AggregateScenario<TemplateApplication> Scenario() => new(new TemplateApplication(Tenant, Program, Template, true));

    static Result Apply(TemplateApplication owner, Uuid command, long version) => owner.ApplyVersion(command,
        version, Actor, new NativeFactReference(new MethodologyTemplate(Template).Stream,
            Uuid.CreateVersion5(Template, "publication:" + version), (ulong)version), (ulong)version - 1,
        Manifest, ["control"], []);
}
