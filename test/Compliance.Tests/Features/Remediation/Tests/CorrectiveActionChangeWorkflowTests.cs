using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionChangeWorkflowTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldChangeOwnedTermsGivenLeadOrFindingOwner(bool lead)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var request = Change(fixture, finding);

        // Act
        var changed = await fixture.AsAsync(lead ? fixture.LeadUserId : fixture.OwnerUserId, request);
        var read = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        var action = Assert.Single(read.CorrectiveActions);
        Assert.Equal(request.Description.Trim(), action.Description);
        Assert.Equal(request.OwnerMemberId, action.OwnerMemberId);
        Assert.Equal(request.DueOn, action.DueOn);
        Assert.Equal(2, action.Revision);
        Assert.Equal("open", action.Status);
        Assert.Equal(finding.Source, read.Source);
        Assert.Equal(Assert.Single(finding.History, static entry => entry.Change == "corrective_action_added").Summary,
            Assert.Single(read.History, static entry => entry.Change == "corrective_action_added").Summary);
        Assert.Contains(request.Description.Trim(),
            Assert.Single(read.History, static entry => entry.Change == "corrective_action_changed").Summary, StringComparison.Ordinal);
        Assert.Equal(WireJson.Of(changed), WireJson.Of(read));
        Assert.Equal(1, (await fixture.FindingStreamAsync(finding.FindingId)).UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutChangeGivenOnlyActionOwnership()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);

        // Act
        await fixture.Scenario(fixture.BackupUserId).When(Change(fixture, finding)).ExpectFailure(RequestErrorKind.Forbidden);
        var read = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal(1, Assert.Single(read.CorrectiveActions).Revision);
        Assert.Equal(finding.History.Count, read.History.Count);
    }

    [Fact]
    public async Task ShouldReplayFrozenResponseGivenRetryAfterLaterChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var request = Change(fixture, finding);
        var first = await fixture.AsAsync(fixture.LeadUserId, request);
        await fixture.AsAsync(fixture.LeadUserId, request with
        {
            CommandId = Uuid.CreateVersion4(),
            ExpectedRevision = 2,
            Description = "A later correction.",
        });
        var before = await fixture.ActionAsync(finding.FindingId, request.ActionId);

        // Act
        var retry = await fixture.AsAsync(fixture.LeadUserId, request);
        var after = await fixture.ActionAsync(finding.FindingId, request.ActionId);

        // Assert
        Assert.Equal(WireJson.Of(first), WireJson.Of(retry));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(3, after.Revision);
        Assert.Equal("A later correction.", after.Changed!.Description);
    }

    [Fact]
    public async Task ShouldRefuseFrozenResponseWithoutAppendGivenChangePermissionRevoked()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var request = Change(fixture, finding);
        var accepted = await fixture.AsAsync(fixture.LeadUserId, request);
        var before = await fixture.ActionAsync(finding.FindingId, request.ActionId);
        Assert.NotEqual(fixture.LeadMemberId, finding.OwnerMemberId);
        Assert.True(fixture.Permissions.Managers.Remove(fixture.LeadMemberId));

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(request)
            .ExpectFailure(RequestErrorKind.Forbidden);
        var refused = await fixture.ActionAsync(finding.FindingId, request.ActionId);
        fixture.Permissions.Managers.Add(fixture.LeadMemberId);
        var restored = await fixture.AsAsync(fixture.LeadUserId, request);
        var after = await fixture.ActionAsync(finding.FindingId, request.ActionId);

        // Assert
        Assert.Equal(before.Version, refused.Version);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(WireJson.Of(accepted), WireJson.Of(restored));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRefuseWithoutAppendGivenFindingOwnerMembershipEnded(bool retry, bool deprovisioned)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var request = Change(fixture, finding);
        if (retry)
            await fixture.AsAsync(fixture.OwnerUserId, request);
        await ProgramManagementServices.SeedAsync(provider, new Member(fixture.TenantId, fixture.OwnerUserId),
            member => deprovisioned
                ? member.Deprovision(fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, "Left.")
                : member.Suspend(fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, "Suspended."));
        var before = await fixture.ActionAsync(finding.FindingId, request.ActionId);

        // Act
        await fixture.Scenario(fixture.OwnerUserId).When(request)
            .ExpectFailure(RequestErrorKind.Forbidden);
        var after = await fixture.ActionAsync(finding.FindingId, request.ActionId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Revision, after.Revision);
    }

    [Fact]
    public async Task ShouldRefuseChangedIntentGivenChangeCommandRetried()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var request = Change(fixture, finding);
        await fixture.AsAsync(fixture.LeadUserId, request);

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(request with { Description = "Changed retry intent." })
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(2, (await fixture.ActionAsync(finding.FindingId, request.ActionId)).Revision);
    }

    [Fact]
    public async Task ShouldRefuseWithoutChangeGivenTargetOwnerSuspended()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        await fixture.SuspendAsync(fixture.ReviewerUserId);

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(Change(fixture, finding)).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Equal(1, Assert.Single((await fixture.GetFindingAsync(finding.FindingId)).CorrectiveActions).Revision);
    }

    [Fact]
    public async Task ShouldRequireCurrentOwnerGivenChangedActionCompleted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var changed = await fixture.AsAsync(fixture.LeadUserId, Change(fixture, finding));
        var action = Assert.Single(changed.CorrectiveActions);

        // Act
        await fixture.Scenario(fixture.BackupUserId).When(fixture.Complete(changed, action.ActionId,
            "Old owner attempted completion.", OperationsFixture.FullSupport)).ExpectFailure(RequestErrorKind.Forbidden);
        var completed = await fixture.CompleteActionAsync(fixture.ReviewerUserId, changed, action.ActionId);

        // Assert
        var result = Assert.Single(completed.CorrectiveActions);
        Assert.Equal("completed", result.Status);
        Assert.Equal(fixture.ReviewerMemberId, result.CompletedByMemberId);
        Assert.Equal(3, result.Revision);
    }

    [Fact]
    public async Task ShouldRefuseClosureGivenCurrentActionOwnerAttemptsVerification()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await OpenAsync(fixture);
        var changed = await fixture.AsAsync(fixture.LeadUserId, Change(fixture, finding) with { OwnerMemberId = fixture.ApproverMemberId });
        changed = await fixture.CompleteActionAsync(fixture.LeadUserId, changed, Assert.Single(changed.CorrectiveActions).ActionId);
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId,
            fixture.Close(changed), RequestErrorKind.Forbidden);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.PendingClosure);
        Assert.Null(after.Closed);
    }

    internal static async Task<FindingView> OpenAsync(OperationsFixture fixture)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        return await fixture.AddActionAsync(finding, fixture.BackupMemberId, fixture.Today.AddDays(4));
    }

    internal static ChangeCorrectiveAction Change(OperationsFixture fixture, FindingView finding)
    {
        var action = Assert.Single(finding.CorrectiveActions);
        return new ChangeCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            finding.FindingId, action.ActionId, action.Revision, "  Remove VPN and SSO access.  ",
            fixture.ReviewerMemberId, fixture.Today.AddDays(2));
    }
}
