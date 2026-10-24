using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Testing.AcceptanceFacts;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ServiceEngagementStaffAssignmentTests
{
    static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task ShouldAssignStaffAfterTheirPermanentWallsAreCommittedGivenAcceptedEngagement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner, _) = await AcceptedAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");
        var command = Assign(host, engagement, assignee, 2);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var admitted = await host.HttpAsync(command, partner.Actor);
        var beforeProcess = await host.EventsAsync<DomainEvent>(stream);
        var record = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(command.CommandId, admitted.Value!.OperationId);
        Assert.Equal(nameof(ServiceEngagementStaffAssignmentRequested), beforeProcess[^1].GetType().Name);
        Assert.Equal(ProcessState.Completed, record.State);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal("accepted", owner.Status);
        var assigned = Assert.Single(await host.EventsAsync<ServiceEngagementStaffAssigned>(stream));
        Assert.Equal(command.EffectiveFrom, assigned.EffectiveFrom);
        Assert.Equal(command.CommandId, assigned.OperationId);
        var view = (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!;
        Assert.Equal(2, view.Assignments.Count);
        Assert.Equal(2, view.Revision);
        var added = Assert.Single(view.Assignments, item => item.StaffMemberId == assignee.StaffMemberId);
        Assert.True(added.IsCurrent);
        Assert.Equal(command.EffectiveFrom, added.EffectiveFrom);
        Assert.Equal(owner.OperationOf(command.CommandId)!.Authorized!.DecisionAt, added.AssignedAt);
        Assert.Equal(2, (await host.SendAsync(new GetServiceEngagementAcceptanceHistory(host.Tenant, engagement)))
            .Value!.Count);
        foreach (var kind in new[] { PracticeSubjectKind.Staff, PracticeSubjectKind.CanonicalUser })
        {
            var subject = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, kind,
                kind == PracticeSubjectKind.Staff ? assignee.StaffMemberId : assignee.UserId));
            Assert.Equal("attest", subject.Practice);
            Assert.NotNull(subject.CommitmentOf(command.CommandId));
            Assert.False(subject.Uses.HasActiveUse);
        }

        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldRefuseAssignmentGivenEngagementIsStillADraft()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await PersonalDecisionFacts.SeedPartnerAsync(host);
        var engagement = await SeedOpenEngagementAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");

        // Act
        var refused = await host.HttpAsync(Assign(host, engagement, assignee, 1), partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ServiceEngagementStaffAssignmentRequested>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Fact]
    public async Task ShouldRefuseAssignmentWithoutAppendGivenStaleRevisionBackdatedDateDuplicateOrWrongPractice()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner, _) = await AcceptedAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");
        var advisory = await host.RegisterStaffAsync("advisory");
        var lead = (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!
            .Assignments.Single();
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var stale = await host.HttpAsync(Assign(host, engagement, assignee, 1), partner.Actor);
        var backdated = await host.HttpAsync(Assign(host, engagement, assignee, 2) with
        {
            EffectiveFrom = Today.AddDays(-1),
        }, partner.Actor);
        var duplicate = await host.HttpAsync(new AssignServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, lead.StaffMemberId, 2, Today), partner.Actor);
        var wrongPractice = await host.HttpAsync(Assign(host, engagement, advisory, 2), partner.Actor);
        var unknown = await host.HttpAsync(new AssignServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, Uuid.CreateVersion4(), 2, Today), partner.Actor);
        var client = await host.HttpAsync(Assign(host, engagement, assignee, 2));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, backdated.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, wrongPractice.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, client.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ServiceEngagementStaffAssignmentRequested>(stream));
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgementAndConflictOnChangeGivenExactAssignmentRetry()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner, _) = await AcceptedAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");
        var command = Assign(host, engagement, assignee, 2);
        var first = await host.HttpAsync(command, partner.Actor);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service()))).IsSuccess);

        // Act
        var retried = await host.HttpAsync(command, partner.Actor);
        var changedDate = await host.HttpAsync(command with { EffectiveFrom = Today.AddDays(1) }, partner.Actor);
        var changedStaff = await host.HttpAsync(command with { StaffMemberId = Uuid.CreateVersion4() }, partner.Actor);
        var changedRevision = await host.HttpAsync(command with { ExpectedRevision = 3 }, partner.Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(IndependenceHost.Json(first.Value), IndependenceHost.Json(retried.Value));
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        Assert.Equal(RequestErrorKind.Conflict, changedDate.Error?.Kind);
        Assert.False(changedStaff.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedRevision.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRefuseAssignmentGivenPersonPermanentlyServesTheOppositePractice()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner, _) = await AcceptedAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.CanonicalUser,
            assignee.UserId, "advisory", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            ActorReference.ForFirmStaff(partner.Staff.UserId, "Synthetic partner"), IndependenceHost.At);

        // Act
        var refused = await host.HttpAsync(Assign(host, engagement, assignee, 2), partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Contains(IndependenceCompartments.PersonExclusivityRuleId, refused.Error!.Message);
        Assert.Empty(await host.EventsAsync<ServiceEngagementStaffAssignmentRequested>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Fact]
    public async Task ShouldRejectAssignmentAndKeepAcceptedEngagementGivenOppositePracticeCommittedBeforeReservation()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner, accepted) = await AcceptedAsync(host);
        var assignee = await host.RegisterStaffAsync("attest");
        var command = Assign(host, engagement, assignee, 2);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            assignee.StaffMemberId, "advisory", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            ActorReference.ForFirmStaff(partner.Staff.UserId, "Synthetic partner"), IndependenceHost.At);

        // Act
        var record = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.Equal(ProcessState.Aborted, record.State);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        Assert.Single(await host.EventsAsync<ServiceEngagementStaffAssignmentRejected>(stream));
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRejected>(stream));
        Assert.Empty(await host.EventsAsync<ServiceEngagementStaffAssigned>(stream));
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal("accepted", owner.Status);
        Assert.Equal(2UL, owner.Revision);
        Assert.False(owner.HasPendingOperation);
        var lead = (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!
            .Assignments.Single();
        var leadWall = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.CanonicalUser, lead.UserId));
        Assert.Equal("attest", leadWall.Practice);
        Assert.NotNull(leadWall.CommitmentOf(accepted));
        var user = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.CanonicalUser, assignee.UserId));
        Assert.Null(user.Practice);
        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
    }

    static AssignServiceEngagementStaff Assign(IndependenceHost host, Uuid engagement, FirmStaffMemberView assignee,
        long revision) => new(Uuid.CreateVersion4(), host.Tenant, engagement, assignee.StaffMemberId, revision, Today);
}
