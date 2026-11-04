using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class CorrectiveActionCancellationWorkflowTests
{
    [Fact]
    public async Task ShouldPreserveReasonAndExactRetryGivenLeadCancelsOpenAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);
        var request = Cancel(fixture, finding, "  Duplicate; other action removes access.  ");

        // Act
        var first = await fixture.AsAsync(fixture.LeadUserId, request);
        var retry = await fixture.AsAsync(fixture.LeadUserId, request);
        var read = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        var action = Assert.Single(read.CorrectiveActions);
        Assert.Equal("cancelled", action.Status);
        Assert.Equal("Duplicate; other action removes access.", action.CancellationReason);
        Assert.Equal(fixture.LeadMemberId, action.CancelledByMemberId);
        Assert.NotNull(action.CancelledAt);
        Assert.Null(action.CompletedAt);
        Assert.Null(action.Evidence);
        Assert.Equal(2, action.Revision);
        Assert.Equal(WireJson.Of(first), WireJson.Of(retry));
        Assert.Equal(WireJson.Of(first), WireJson.Of(read));
        Assert.Equal("corrective_action_cancelled", read.History[^1].Change);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenActionOwnerHasNoCancellationPermission()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);

        // Act
        await fixture.Scenario(fixture.OwnerUserId).When(Cancel(fixture, finding, "No longer needed."))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var after = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal("open", Assert.Single(after.CorrectiveActions).Status);
        Assert.Equal(finding.History.Count, after.History.Count);
    }

    [Fact]
    public async Task ShouldRejectChangedIntentGivenCancellationCommandRetried()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);
        var request = Cancel(fixture, finding, "Duplicate action.");
        await fixture.AsAsync(fixture.LeadUserId, request);

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(request with { Reason = "Changed explanation." })
            .ExpectFailure(RequestErrorKind.Conflict);
        var after = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal("Duplicate action.", Assert.Single(after.CorrectiveActions).CancellationReason);
        Assert.Single(after.History, entry => entry.Change == "corrective_action_cancelled");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldRefuseWithoutHistoryGivenMissingCancellationReason(string reason)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(Cancel(fixture, finding, reason))
            .ExpectFailure(RequestErrorKind.Validation);
        var after = await fixture.GetFindingAsync(finding.FindingId);

        // Assert
        Assert.Equal("open", Assert.Single(after.CorrectiveActions).Status);
        Assert.Equal(finding.History.Count, after.History.Count);
    }

    [Fact]
    public async Task ShouldRefuseCompletionGivenCancelledAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, Cancel(fixture, finding, "Duplicate action."));
        var action = Assert.Single(cancelled.CorrectiveActions);

        // Act
        await fixture.Scenario(fixture.OwnerUserId).When(fixture.Complete(cancelled, action.ActionId,
                "Completed later.", OperationsFixture.FullSupport))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal("cancelled", Assert.Single((await fixture.GetFindingAsync(finding.FindingId)).CorrectiveActions).Status);
    }

    [Fact]
    public async Task ShouldCloseFindingGivenEveryActionCancelledWithReasonAndIndependentCloser()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await WithActionAsync(fixture);
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, Cancel(fixture, finding,
            "Duplicate; resolution evidence proves access was removed under the retained action."));

        // Act
        var closed = await fixture.CloseAsync(fixture.ApproverUserId, cancelled);

        // Assert
        Assert.Equal("closed", closed.Status);
        Assert.Equal("cancelled", Assert.Single(closed.CorrectiveActions).Status);
        Assert.NotNull(closed.Closure);
    }

    static async Task<FindingView> WithActionAsync(OperationsFixture fixture)
    {
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        return await fixture.AddActionAsync(finding, fixture.OwnerMemberId, fixture.Today.AddDays(4));
    }

    static CancelCorrectiveAction Cancel(OperationsFixture fixture, FindingView finding, string reason)
    {
        var action = Assert.Single(finding.CorrectiveActions);
        return new CancelCorrectiveAction(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            finding.FindingId, action.Revision, action.ActionId, reason);
    }
}
