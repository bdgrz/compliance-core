using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Queries;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceRequestHandlerTests
{
    const string Sha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Theory]
    [InlineData(" ", "Upload the export.", 0)]
    [InlineData("Access export", " ", 0)]
    [InlineData("Access export", "Upload the export.", -1)]
    public async Task ShouldPreserveRequestAbsenceGivenInvalidOpeningContent(string title, string instructions,
        int dueOffsetDays)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, title,
            instructions, fixture.OwnerMemberId, fixture.Today.AddDays(dueOffsetDays));

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(request).ExpectFailure(RequestErrorKind.Validation);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new EvidenceRequest(fixture.TenantId, fixture.ProgramId, request.EvidenceRequestId));

        // Assert
        Assert.False(retained.IsOpened);
        Assert.Equal(0UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldReplayOpenWithoutAppendingGivenSameCommandIdentity()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            "Q3 export", "Upload the export.", fixture.OwnerMemberId, fixture.Today.AddDays(7));
        var first = await fixture.AsAsync(fixture.LeadUserId, request);

        // Act
        var replay = await fixture.AsAsync(fixture.LeadUserId, request);
        await fixture.Scenario(fixture.LeadUserId).When(request with { Title = "Changed" })
            .ExpectFailure(RequestErrorKind.Conflict);
        var retained = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new EvidenceRequest(fixture.TenantId, fixture.ProgramId, request.EvidenceRequestId));

        // Assert
        Assert.Equal(first, replay);
        Assert.Equal(1, retained.Revision);
        Assert.Equal(1UL, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveOpenRequestGivenEmptyCancellationRationale()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);

        // Act
        await fixture.Scenario(fixture.LeadUserId).When(new CancelEvidenceRequest(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, " "))
            .ExpectFailure(RequestErrorKind.Validation);
        var retained = await fixture.AsAsync(fixture.LeadUserId, new GetEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId));

        // Assert
        Assert.Equal(opened, retained);
    }

    [Fact]
    public async Task ShouldOpenAndLetTheOwnerFulfilGivenAnAvailableArtifact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);

        // Act
        var fulfilled = await fixture.AsAsync(fixture.OwnerUserId, new FulfilEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        Assert.Equal(EvidenceRequest.Open, opened.Status);
        Assert.Equal(fixture.ControlId, opened.ControlId);
        Assert.Equal(EvidenceRequest.Fulfilled, fulfilled.Status);
        Assert.Equal(artifactId, fulfilled.ArtifactId);
        var listed = await fixture.AsAsync(fixture.LeadUserId, new ListEvidenceRequests(fixture.TenantId,
            fixture.ProgramId, EvidenceRequest.Fulfilled));
        Assert.Equal(opened.EvidenceRequestId, Assert.Single(listed.Items).EvidenceRequestId);
    }

    [Fact]
    public async Task ShouldForbidFulfilmentGivenNeitherOwnerNorManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);

        // Act
        var result = fixture.Scenario(fixture.ReviewerUserId).When(new FulfilEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRefuseFulfilmentGivenARejectedArtifact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.SecretDetected);

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new FulfilEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRefuseFulfilmentAndPreserveOpenRequestGivenPendingInspection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, null);

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new FulfilEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Conflict);
        var unchanged = await fixture.AsAsync(fixture.LeadUserId, new GetEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId));
        Assert.Equal(EvidenceRequest.Open, unchanged.Status);
        Assert.Equal(opened.Revision, unchanged.Revision);
        Assert.Null(unchanged.ArtifactId);
    }

    [Fact]
    public async Task ShouldRefuseFulfilmentAndPreserveOpenRequestGivenQuarantinedArtifact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Malware);

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new FulfilEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Conflict);
        var unchanged = await fixture.AsAsync(fixture.LeadUserId, new GetEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId));
        Assert.Equal(EvidenceRequest.Open, unchanged.Status);
        Assert.Equal(opened.Revision, unchanged.Revision);
        Assert.Null(unchanged.ArtifactId);
    }

    [Fact]
    public async Task ShouldRejectOpeningGivenAnUnknownOwner()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        var result = fixture.Scenario(fixture.LeadUserId).When(new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, "Export", "Upload it.", Uuid.CreateVersion4(), fixture.Today.AddDays(7)));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldForbidOpeningGivenANonManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, "Export", "Upload it.", fixture.OwnerMemberId, fixture.Today.AddDays(7)));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldCancelWithRationaleGivenAManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);

        // Act
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, new CancelEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, "Superseded by the Q4 request"));

        // Assert
        Assert.Equal(EvidenceRequest.Cancelled, cancelled.Status);
        Assert.Equal("Superseded by the Q4 request", cancelled.CancellationRationale);
    }

    static Task<EvidenceRequestView> OpenAsync(OperationsFixture fixture) =>
        fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            "Q3 access review export", "Upload the signed review record.", fixture.OwnerMemberId,
            fixture.Today.AddDays(14), fixture.ControlId));

    static async Task<Uuid> ArtifactAsync(OperationsFixture fixture, EvidenceInspectionOutcome? outcome)
    {
        var artifactId = Uuid.CreateVersion4();
        var collector = ActorReference.ForMember(fixture.OwnerMemberId, "Owner");
        await ProgramManagementServices.SeedAsync<EvidenceArtifact, EvidenceArtifactRegistration>(fixture.Provider,
            new EvidenceArtifact(fixture.TenantId, artifactId), artifact => artifact.Register(
                new EvidenceArtifactContent("Signed review", null, "document", "Okta", DateTimeOffset.UtcNow,
                    fixture.Today, fixture.Today, "internal"), Sha256, 4, collector, DateTimeOffset.UtcNow));
        if (outcome is { } inspection)
            await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, artifactId),
                artifact => artifact.RecordInspection(inspection, DateTimeOffset.UtcNow) is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict, "inspection")));
        return artifactId;
    }
}
