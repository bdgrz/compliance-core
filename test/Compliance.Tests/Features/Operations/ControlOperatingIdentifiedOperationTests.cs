using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Operations;

/// <summary>
///     The six plan and occurrence commands are identified 200 operations: an exact retry returns the original
///     frozen view even after the owner changed, and the same identity with a changed intent conflicts.
/// </summary>
public sealed class ControlOperatingIdentifiedOperationTests
{
    static readonly ControlCadence AdHoc = new("ad_hoc", DueWithinDays: 10);

    static string Wire<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!);

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactProposalRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = fixture.Propose(0);
        var first = await fixture.AsAsync(fixture.LeadUserId, request);
        await fixture.AsAsync(fixture.ApproverUserId, new ApproveControlOperatingPlan(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first.Revision, first.PlanVersionId,
            "Independent approval."));

        // Act
        var retried = await fixture.AsAsync(fixture.LeadUserId, request);
        var plans = await fixture.GetPlansAsync();

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal("pending_approval", retried.Status);
        Assert.Equal(2, plans.Revision);
        Assert.Equal("approved", plans.Current!.Status);
    }

    [Fact]
    public async Task ShouldConflictGivenSameProposalIdentityWithChangedIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = fixture.Propose(0);
        await fixture.AsAsync(fixture.LeadUserId, request);

        // Act
        var changed = request with { Rationale = "A different rationale." };

        // Assert
        await fixture.Scenario(fixture.LeadUserId).When(changed).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.LeadUserId).When(request with { Cadence = AdHoc })
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(1, (await fixture.GetPlansAsync()).Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactApprovalRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var proposed = await fixture.ProposeAsync(0);
        var approval = new ApproveControlOperatingPlan(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, proposed.Revision, proposed.PlanVersionId, "Independent approval.");
        var first = await fixture.AsAsync(fixture.ApproverUserId, approval);
        await fixture.ProposeAsync(2, cadence: new ControlCadence("recurring", "monthly", fixture.Today, 5),
            effectiveFrom: fixture.Today);

        // Act
        var retried = await fixture.AsAsync(fixture.ApproverUserId, approval);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal("approved", retried.Status);
        Assert.Equal(3, (await fixture.GetPlansAsync()).Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameApprovalIdentityWithChangedIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var proposed = await fixture.ProposeAsync(0);
        var approval = new ApproveControlOperatingPlan(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, proposed.Revision, proposed.PlanVersionId, "Independent approval.");
        await fixture.AsAsync(fixture.ApproverUserId, approval);

        // Act
        var denied = await PersonalOperatingPlanApprovalTransportTests.HttpAsync(fixture.Provider,
            fixture.ApproverUserId, approval with { Rationale = "A different rationale." },
            RequestErrorKind.Conflict);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(2, (await fixture.GetPlansAsync()).Revision);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactOpenRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(cadence: AdHoc);
        var request = fixture.Open("Release 4.2", fixture.Today);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);
        var attested = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(first));

        // Act
        var retried = await fixture.AsAsync(fixture.OwnerUserId, request);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(1, retried.Revision);
        Assert.Equal("open", retried.State);
        Assert.Equal(2, attested.Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameOpenIdentityWithChangedIntentOrSecondCommandForSameWindow()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync(cadence: AdHoc);
        var window = new ControlEventWindow("release", "4.2");
        var request = fixture.Open("Release 4.2", fixture.Today, window);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);

        // Act
        var changedTrigger = request with { Trigger = "Another trigger" };
        var secondCommand = fixture.Open("Release 4.2 again", fixture.Today.AddDays(-1), window);

        // Assert
        await fixture.Scenario(fixture.OwnerUserId).When(changedTrigger).ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.OwnerUserId).When(secondCommand).ExpectFailure(RequestErrorKind.Conflict);
        var occurrence = Assert.Single(await fixture.OccurrencesAsync());
        Assert.Equal(first.OccurrenceId, occurrence.OccurrenceId);
        Assert.Equal(ControlOccurrenceIdentity.Of(fixture.TenantId, fixture.ProgramId, fixture.ControlId, window),
            first.OccurrenceId);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactAttestationRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var request = fixture.Attest(occurrence);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);
        await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(first, "returned"));

        // Act
        var retried = await fixture.AsAsync(fixture.OwnerUserId, request);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal("submitted", retried.State);
        Assert.Equal(2, retried.Revision);
        Assert.Equal(3, (await fixture.GetOccurrenceAsync(occurrence.OccurrenceId)).Revision);
    }

    [Fact]
    public async Task ShouldConflictGivenSameAttestationIdentityWithChangedIntent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var request = fixture.Attest(occurrence);
        await fixture.AsAsync(fixture.OwnerUserId, request);

        // Act
        var denied = await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId,
            request with { Notes = "A different note." }, RequestErrorKind.Conflict);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Single((await fixture.GetOccurrenceAsync(occurrence.OccurrenceId)).Attestations);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactCorrectionRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var returned = await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(submitted, "returned"));
        var correction = new CorrectControlAttestation(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, returned.OccurrenceId, returned.Revision, "complete",
            DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Added missing support.", null,
            OperationsFixture.FullSupport, "Reviewer requested support.");
        var first = await fixture.AsAsync(fixture.OwnerUserId, correction);
        await fixture.AsAsync(fixture.ReviewerUserId, fixture.Review(first, "approved"));

        // Act
        var retried = await fixture.AsAsync(fixture.OwnerUserId, correction);
        var denied = await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId,
            correction with { CorrectionReason = "A different reason." }, RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal(2, retried.Attestations.Count);
        Assert.Equal(retried.Attestations[0].AttestationId, retried.Attestations[1].SupersedesAttestationId);
        Assert.False(denied.IsSuccess);
    }

    [Fact]
    public async Task ShouldReturnOriginalResultGivenExactReviewRetryAfterLaterOwnerChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));
        var review = fixture.Review(submitted, "returned");
        var first = await fixture.AsAsync(fixture.ReviewerUserId, review);
        await fixture.AsAsync(fixture.OwnerUserId, new CorrectControlAttestation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, first.OccurrenceId, first.Revision, "complete",
            DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Added missing support.", null,
            OperationsFixture.FullSupport, "Reviewer requested support."));

        // Act
        var retried = await fixture.AsAsync(fixture.ReviewerUserId, review);
        var denied = await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.ReviewerUserId,
            review with { Rationale = "A different rationale." }, RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(Wire(first), Wire(retried));
        Assert.Equal("returned", retried.State);
        Assert.Equal(3, retried.Revision);
        Assert.False(denied.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenSecondAttestationOfSubmittedOccurrence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync("missed"))[0];
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(occurrence));

        // Act
        var stale = await PersonalOccurrenceProofTransportTests.HttpAsync(fixture.Provider, fixture.OwnerUserId,
            fixture.Attest(occurrence), RequestErrorKind.Conflict);

        // Assert
        Assert.Contains("attestation", stale.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await fixture.GetOccurrenceAsync(occurrence.OccurrenceId)).Revision);
    }
}
