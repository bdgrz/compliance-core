using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Policies.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyDistributionCampaignTests
{
    static readonly Uuid Ada = Uuid.CreateVersion4();
    static readonly Uuid Bob = Uuid.CreateVersion4();
    static readonly Uuid Cy = Uuid.CreateVersion4();
    static readonly Uuid Dee = Uuid.CreateVersion4();
    static readonly Uuid Eve = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = CampaignOwnerHarness.Now;
    static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    static readonly DateOnly Due = Today.AddDays(30);
    static readonly CampaignSubject PolicySubject = new("policy", Uuid.CreateVersion4(),
        "POL-AC", "Access Control Policy", 2, "sha-v2");

    static FrozenWorkRelationship Job(Uuid personId, string workerType, string status,
        string? department, DateOnly? start = null) => new(Uuid.CreateVersion4(), 1, personId,
        personId.ToString()[..8], workerType, status, start ?? new DateOnly(2025, 1, 1), null,
        department, null, null);

    static WorkforceRosterSnapshotView Roster(params FrozenWorkRelationship[] jobs) => new(
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), null, "sha", jobs.Length, null,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Manager"), Now, jobs.Select(static job => new FrozenPerson(
            job.PersonId, 1, "Person " + job.PersonId.ToString()[..4], null)).DistinctBy(static p => p.PersonId)
            .ToArray(), jobs, true);

    static async Task<Uuid> LaunchAsync(CampaignOwnerHarness harness, WorkforceRosterSnapshotView roster,
        string kind = PolicyAudience.CoreSecurity, IReadOnlyList<string>? teams = null,
        CampaignSubject? subject = null)
    {
        teams ??= [];
        var campaignId = Uuid.CreateVersion4();
        await CampaignSeed.LaunchAsync(harness.Provider, harness.TenantId, harness.ProgramId, campaignId,
            subject ?? PolicySubject, kind, teams, roster.SnapshotId, roster.ContentSha256,
            RosterAudience.Evaluate(roster, kind, teams), Due, "Read and acknowledge", harness.Manager,
            harness.ManagerId, Now);
        return campaignId;
    }

    static async Task<CommandFailure?> AcknowledgeAsync(CampaignOwnerHarness harness, Uuid campaignId, Uuid person,
        long version = 2, string hash = "sha-v2", string? text = null, DateTimeOffset? at = null)
    {
        var state = await harness.StateAsync(campaignId);
        var request = new AcknowledgePolicy(Uuid.CreateVersion4(), harness.TenantId, harness.ProgramId, campaignId,
            person, version, hash, text ?? PolicyDistributionCampaign.DefaultAcknowledgementText,
            state.Campaign.Revision);
        var failure = CampaignRules.CheckAcknowledgement(state, request);
        if (failure is null)
        {
            var applied = await CampaignSeed.AcknowledgeAsync(harness.Provider, harness.TenantId, harness.ProgramId,
                campaignId, person, version, hash, harness.Manager,
                new ActorReference("workforce_person", person.ToString(), "Person"), true, at ?? Now.AddDays(1),
                request.CommandId, request.AcknowledgementText);
            Assert.True(applied.IsSuccess, applied.Error?.Message);
        }

        return failure;
    }

    [Fact]
    public async Task ShouldFreezeEmployeesAndContractorsGivenCoreSecurityRule()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var roster = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "contractor", "on_leave", "Sales"),
            Job(Cy, "external_collaborator", "active", "Engineering"),
            Job(Dee, "employee", "ended", "Engineering"));

        // Act
        var campaignId = await LaunchAsync(harness, roster);
        var state = await harness.StateAsync(campaignId);

        // Assert
        var people = state.ParticipantViews(Today, null);
        Assert.Equal(new[] { Ada, Bob }.Order(), people.Select(static p => p.PersonId).Order());
        Assert.All(people, person => Assert.Equal("launch", person.Inclusion));
        Assert.All(people, person => Assert.Equal(roster.SnapshotId, person.IncludedBySnapshotId));
        Assert.Equal(new CampaignTotalsView(2, 2, 0, 0, 2, 0, 0, 0), state.Totals(Today));
        Assert.Equal(1, state.Campaign.Revision);
    }

    [Fact]
    public void ShouldSelectNamedRosterTeamsGivenRoleTargetedRule()
    {
        // Arrange
        var roster = Roster(Job(Ada, "employee", "active", " engineering "),
            Job(Bob, "contractor", "active", "Sales"),
            Job(Cy, "external_collaborator", "active", "Engineering"));

        // Act
        var audience = RosterAudience.Evaluate(roster, PolicyAudience.RoleTargeted, ["Engineering"]);

        // Assert
        Assert.Equal(new[] { Ada, Cy }.Order(), audience.Matching.Keys.Order());
    }

    [Fact]
    public async Task ShouldAmendWithoutRewritingLaunchAudienceGivenLaterRoster()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var launch = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "employee", "active", "Engineering"),
            Job(Cy, "employee", "active", "Sales"),
            Job(Dee, "employee", "active", "Engineering"));
        var campaignId = await LaunchAsync(harness, launch, PolicyAudience.RoleTargeted, ["Engineering"]);
        Assert.Null(await AcknowledgeAsync(harness, campaignId, Bob));
        var later = Roster(Job(Ada, "employee", "active", "Engineering"),
            Job(Bob, "employee", "active", "Sales"),
            Job(Cy, "employee", "active", "Engineering"),
            Job(Eve, "contractor", "pending", "Engineering", Today.AddDays(5)));
        var previous = RosterAudience.Evaluate(launch, PolicyAudience.RoleTargeted, ["Engineering"]).Present;
        var current = RosterAudience.Evaluate(later, PolicyAudience.RoleTargeted, ["Engineering"]);

        // Act
        var result = await CampaignSeed.ReconcileAsync(harness.Provider, harness.TenantId, harness.ProgramId,
            campaignId, later.SnapshotId, later.ContentSha256, current, previous, harness.Manager, Now.AddDays(2));
        var repeated = await CampaignSeed.ReconcileAsync(harness.Provider, harness.TenantId, harness.ProgramId,
            campaignId, later.SnapshotId, later.ContentSha256, current, previous, harness.Manager, Now.AddDays(3));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var reasons = result.Value.Amendments.ToDictionary(static a => a.PersonId, static a => a.Reason);
        Assert.Equal("mover_out", reasons[Bob]);
        Assert.Equal("leaver", reasons[Dee]);
        Assert.Equal("mover_in", reasons[Cy]);
        Assert.Equal("joiner", reasons[Eve]);
        Assert.Equal(Today.AddDays(35), result.Value.Amendments.Single(a => a.PersonId == Eve).DueOn);
        var state = await harness.StateAsync(campaignId);
        var people = state.ParticipantViews(Today, null).ToDictionary(static p => p.PersonId);
        Assert.Equal("removed", people[Bob].State);
        Assert.NotNull(people[Bob].Acknowledgement);
        Assert.Equal("launch", people[Ada].Inclusion);
        Assert.Equal(later.SnapshotId, people[Eve].IncludedBySnapshotId);
        var totals = state.Totals(Today);
        Assert.Equal(new CampaignTotalsView(5, 3, 2, 2, 3, 0, 0, 0), totals);
        Assert.Equal(totals.Population, totals.Pending + totals.Overdue + totals.Satisfied + totals.Excepted +
                                        totals.Removed);
        Assert.Equal(4, state.Roster.Amendments.Count);
        Assert.False(repeated.IsSuccess);
        Assert.Equal(1, state.Campaign.Reconciliations);
    }

    [Fact]
    public async Task ShouldRejectAcknowledgementGivenWrongVersionHashOrText()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var campaignId = await LaunchAsync(harness, Roster(Job(Ada, "employee", "active", null)));

        // Act
        var laterVersion = await AcknowledgeAsync(harness, campaignId, Ada, version: 3);
        var wrongHash = await AcknowledgeAsync(harness, campaignId, Ada, hash: "sha-v3");
        var wrongText = await AcknowledgeAsync(harness, campaignId, Ada, text: "I agree.");
        var outsider = await AcknowledgeAsync(harness, campaignId, Bob);
        var exact = await AcknowledgeAsync(harness, campaignId, Ada);
        var duplicate = await AcknowledgeAsync(harness, campaignId, Ada);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(laterVersion).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongHash).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongText).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(outsider).Code);
        Assert.Null(exact);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(duplicate).Code);
        var state = await harness.StateAsync(campaignId);
        var acknowledgement = CampaignState.ViewOf(state.Participants[Ada].Acknowledgement!);
        Assert.Equal("sha-v2", acknowledgement.ContentSha256);
        Assert.Equal(PolicyDistributionCampaign.DefaultAcknowledgementText, acknowledgement.AcknowledgementText);
        Assert.True(acknowledgement.RecordedOnBehalf);
        Assert.Equal("workforce_person", acknowledgement.Performer.Kind);
        Assert.Equal(harness.Manager, acknowledgement.Recorder);
        var member = state.Roster.Find(Ada)!;
        Assert.Equal("acknowledged", state.StateOf(member, Today.AddDays(1)));
        Assert.Equal("pending", state.StateOf(member, Today));
    }

    [Fact]
    public async Task ShouldCountExceptedUntilWaiverExpiresGivenApprovedWaiver()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var campaignId = await LaunchAsync(harness, Roster(Job(Ada, "employee", "active", null)));
        var expires = Today.AddMonths(2);
        await using var scope = harness.Provider.CreateAsyncScope();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var state = await harness.StateAsync(campaignId);

        // Act
        var tooLong = await CampaignRules.CheckWaiverAsync(content, state, new ApproveCampaignWaiver(
            Uuid.CreateVersion4(), harness.TenantId, harness.ProgramId, campaignId, Ada, "Leave",
            Today.AddMonths(12).AddDays(1), state.Campaign.Revision), Now, CancellationToken.None);
        var request = new ApproveCampaignWaiver(Uuid.CreateVersion4(), harness.TenantId, harness.ProgramId,
            campaignId, Ada, "Extended leave", expires, state.Campaign.Revision);
        var approved = await CampaignRules.CheckWaiverAsync(content, state, request, Now, CancellationToken.None);
        var applied = await CampaignSeed.ApproveWaiverAsync(harness.Provider, harness.TenantId, harness.ProgramId,
            campaignId, Ada, "Extended leave", expires, harness.Manager, Now, request.CommandId);
        var after = await harness.StateAsync(campaignId);
        var duplicate = await CampaignRules.CheckWaiverAsync(content, after, request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedCampaignRevision = after.Campaign.Revision,
        }, Now, CancellationToken.None);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(tooLong).Code);
        Assert.Null(approved);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(duplicate).Code);
        var member = after.Roster.Find(Ada)!;
        Assert.Equal("excepted", after.StateOf(member, expires.AddDays(-1)));
        Assert.Equal("overdue", after.StateOf(member, expires));
        Assert.Equal(1, after.Totals(Today).Excepted);
        Assert.Equal(0, after.Totals(Today).Satisfied);
        Assert.Equal("pending", after.StateOf(member, Today.AddDays(-1)));
    }

    [Fact]
    public async Task ShouldRecordExactCompletionAndRejectDuplicateGivenTrainingCampaign()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var subject = new CampaignSubject("training", Uuid.CreateVersion4(), "SAT", "Security awareness",
            1, "sha-course");
        var campaignId = await LaunchAsync(harness, Roster(Job(Ada, "employee", "active", null),
            Job(Bob, "employee", "active", null)), subject: subject);
        var state = await harness.StateAsync(campaignId);
        RecordTrainingCompletion Completion(long version, DateOnly completedOn, string source, string evidence) =>
            new(Uuid.CreateVersion4(), harness.TenantId, harness.ProgramId, campaignId, Ada, version, completedOn,
                source, evidence, state.Campaign.Revision);

        // Act
        var wrongVersion = CampaignRules.CheckCompletion(state, Completion(2, Today, "lms_export", "export#12"), Now);
        var badSource = CampaignRules.CheckCompletion(state, Completion(1, Today, "connector", "row"), Now);
        var future = CampaignRules.CheckCompletion(state, Completion(1, Today.AddDays(1), "manual", "certificate.pdf"), Now);
        var exact = Completion(1, Today, "lms_export", "export-2026-10.csv#12");
        var recorded = CampaignRules.CheckCompletion(state, exact, Now);
        var applied = await CampaignSeed.CompleteAsync(harness.Provider, harness.TenantId, harness.ProgramId,
            campaignId, Ada, 1, Today, "lms_export", "export-2026-10.csv#12", harness.Manager, Now, exact.CommandId);
        var after = await harness.StateAsync(campaignId);
        var duplicate = CampaignRules.CheckCompletion(after, Completion(1, Today, "manual", "certificate.pdf"), Now);
        var acknowledgement = CampaignRules.CheckAcknowledgement(after, new AcknowledgePolicy(Uuid.CreateVersion4(),
            harness.TenantId, harness.ProgramId, campaignId, Bob, 1, "sha-course",
            PolicyDistributionCampaign.DefaultAcknowledgementText, after.Campaign.Revision));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(wrongVersion).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(badSource).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(future).Code);
        Assert.Null(recorded);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(duplicate).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(acknowledgement).Code);
        Assert.Equal("completed", after.StateOf(after.Roster.Find(Ada)!, Today));
        Assert.Equal("overdue", after.StateOf(after.Roster.Find(Bob)!, Due.AddDays(1)));
    }

    [Fact]
    public async Task ShouldFreezeClosingTotalsAndRejectLaterResultsGivenClosedCampaign()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var campaignId = await LaunchAsync(harness, Roster(Job(Ada, "employee", "active", null),
            Job(Bob, "employee", "active", null)));
        Assert.Null(await AcknowledgeAsync(harness, campaignId, Ada));

        // Act
        await CampaignSeed.CloseAsync(harness.Provider, harness.TenantId, campaignId, "Annual cycle complete",
            harness.Manager, Now.AddDays(40));
        var late = await AcknowledgeAsync(harness, campaignId, Bob, at: Now.AddDays(41));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(late).Code);
        var view = (await harness.StateAsync(campaignId)).ToView(Today.AddDays(40));
        Assert.Equal("closed", view.Status);
        Assert.Equal(new CampaignTotalsView(2, 2, 0, 0, 0, 1, 1, 0), view.ClosingTotals);
    }

    [Fact]
    public async Task ShouldFreezeAudienceBeyondFormerCapsAndReplayGivenLargeCampaign()
    {
        // Arrange
        await using var harness = new CampaignOwnerHarness();
        var jobs = Enumerable.Range(0, 10_003).Select(_ => Job(Uuid.CreateVersion4(), "employee", "active", null))
            .ToArray();
        var roster = Roster(jobs);

        // Act
        var campaignId = await LaunchAsync(harness, roster);
        var first = await harness.StateAsync(campaignId);
        var replayed = await harness.StateAsync(campaignId);

        // Assert
        Assert.Equal(jobs.Length, first.Totals(Today).Population);
        Assert.Equal(jobs.Length, first.Campaign.Launch!.AudienceCount);
        Assert.Equal(first.Totals(Today), replayed.Totals(Today));
        Assert.Equal(1, first.Campaign.Frozen!.Batch);
    }
}
