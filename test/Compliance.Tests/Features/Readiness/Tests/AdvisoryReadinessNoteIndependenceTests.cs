using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Services;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.Artifacts.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.Boundaries.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.Programs.Authorizers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Authorizers;
using Bdgrz.Compliance.Features.Readiness.Commands;
using Bdgrz.Compliance.Features.Readiness.Queries;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class AdvisoryReadinessNoteIndependenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldDenyAdvisoryNotesButPreserveSharedAssessmentGivenActualAttestHistory(bool closed, bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var baseline = await fixture.NotesAsync(mcp);
        Assert.True(baseline.IsSuccess, baseline.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(baseline.Value!.Items).Body);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, closed);

        // Act
        var notes = await fixture.NotesAsync(mcp);
        var assessment = await fixture.SendAsync(new GetReadinessAssessment(fixture.Tenant, fixture.Program,
            fixture.Assessment), mcp);
        var gaps = await fixture.SendAsync(new ListReadinessGaps(fixture.Tenant, fixture.Program,
            fixture.Assessment), mcp);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, notes.Error?.Kind);
        Assert.True(assessment.IsSuccess, assessment.Error?.Message);
        Assert.True(gaps.IsSuccess, gaps.Error?.Message);
        Assert.Single(gaps.Value!.Items);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPreserveOrdinaryNoteReadGivenHistoryOutsideTheClientsAttestWall(string practice, bool sameClient)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider,
            sameClient ? fixture.Tenant : Uuid.CreateVersion4(), fixture.User, true, practice);

        // Act
        var notes = await fixture.NotesAsync(true);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldDenyNoteReadGivenNoOrdinaryProgramReadGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldLimitGuestProgramAccessToCurrentOpenHandoffGivenScopedGuestMembership()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Memberships.Affiliation = "guest";
        var handoffId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant,
            Uuid.CreateVersion4()), "Compliance Lead");
        var createdAt = fixture.Clock.GetUtcNow().AddDays(-1);
        await AdvisorSeed.HandoffAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId,
            [new(recordId, versionId)], actor, createdAt,
            DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime).AddDays(7), purpose: "Guest review");
        await using var scope = fixture.Provider.CreateAsyncScope();
        var authorizer = new ProgramManagementAuthorizer(fixture.Memberships, fixture.Tenants,
            scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<IProgramResourceScopeResolver>(),
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        var guest = ProgramManagementServices.Actor(fixture.User);

        // Act
        var handoff = await authorizer.AuthorizeAsync(new RequestContext<IProgramManagementRequest>(
            new GetAdvisorRequest(fixture.Tenant, fixture.Program, handoffId), guest),
            CancellationToken.None);
        var handoffs = await authorizer.AuthorizeAsync(new RequestContext<IProgramManagementRequest>(
            new ListAdvisorRequests(fixture.Tenant, fixture.Program), guest),
            CancellationToken.None);
        var annotations = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(new ListReadinessAnnotations(
                fixture.Tenant, fixture.Program, fixture.Assessment), guest), CancellationToken.None);

        // Assert
        Assert.True(handoff.IsSuccess);
        Assert.True(handoffs.IsSuccess, handoffs.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, Assert.IsType<RequestError>(annotations.Error).Kind);
    }

    [Fact]
    public async Task ShouldDenyGuestHandoffListGivenNoProgramScopedViewerGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Memberships.Affiliation = "guest";
        fixture.Permissions.Allowed = false;
        await using var scope = fixture.Provider.CreateAsyncScope();
        var authorizer = new ProgramManagementAuthorizer(fixture.Memberships, fixture.Tenants,
            scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<IProgramResourceScopeResolver>(),
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());

        // Act
        var result = await authorizer.AuthorizeAsync(new RequestContext<IProgramManagementRequest>(
            new ListAdvisorRequests(fixture.Tenant, fixture.Program),
            ProgramManagementServices.Actor(fixture.User)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAllowAssignedAdvisorToReadAdvisoryNotesGivenNoClientMembershipOrProgramGrant(bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync(mcp);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Theory]
    [InlineData("draft", false, false)]
    [InlineData("ready_for_review", false, true)]
    [InlineData("closed", false, false)]
    [InlineData("ready_for_review", true, false)]
    public async Task ShouldEnforceAdvisorHandoffReadVisibilityGivenHandoffStatusAndExpiry(
        string status, bool expired, bool expectedAllowed)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider,
            fixture.Tenant, fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var recordVersionId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant,
            Uuid.CreateVersion4()), "Compliance lead");
        var advisorAuthor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant,
            fixture.User), "Assigned Advisor");
        var createdAt = fixture.Clock.GetUtcNow().AddDays(-2);
        var expiresOn = DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime)
            .AddDays(expired ? -1 : 30);
        var selected = new AdvisorRecordVersion(recordId, recordVersionId);
        await AdvisorSeed.HandoffAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, [selected],
            actor, createdAt, expiresOn, ready: status is "ready_for_review" or "closed", purpose: "Readiness review");
        if (status is "ready_for_review" or "closed")
            await AdvisorSeed.FeedbackAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, feedbackId,
                selected, "comment", "Clarify the control owner.", advisorAuthor, createdAt.AddMinutes(2));
        if (status == "closed")
            await AdvisorSeed.EndAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, actor,
                createdAt.AddMinutes(3));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var authorizer = new ProgramManagementAuthorizer(fixture.Memberships, fixture.Tenants,
            scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<IProgramResourceScopeResolver>(),
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        var clientMutationAuthorizer = new ClientManagementMutationAuthorizer(
            scope.ServiceProvider.GetRequiredService<ClientManagementIndependenceGuard>(),
            fixture.Memberships,
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        IProgramManagementRequest handoffRequest = new GetAdvisorRequest(fixture.Tenant,
            fixture.Program, handoffId);
        IProgramManagementRequest feedbackRequest = new ListAdvisorFeedback(fixture.Tenant,
            fixture.Program, handoffId);
        var correctionRequest = new CorrectAdvisorFeedback(fixture.Tenant,
            fixture.Program, handoffId, feedbackId, Uuid.CreateVersion4(), "Corrected wording.",
            "The original wording was incomplete.", 1);
        var feedbackPostRequest = new RecordAdvisorFeedback(fixture.Tenant, fixture.Program,
            handoffId, Uuid.CreateVersion4(), recordId, recordVersionId, "comment",
            "Please clarify the control owner.", 2);
        var feedbackOutsideSelectionRequest = new RecordAdvisorFeedback(fixture.Tenant,
            fixture.Program, handoffId, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "comment", "This version was not shared.", 2);

        // Act
        var principal = ProgramManagementServices.Actor(fixture.User);
        var handoffResult = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(handoffRequest, principal), CancellationToken.None);
        var feedbackResult = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(feedbackRequest, principal), CancellationToken.None);
        var correctionResult = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(correctionRequest, principal), CancellationToken.None);
        var clientCorrectionResult = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(correctionRequest, principal),
            CancellationToken.None);
        var feedbackPostResult = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(feedbackPostRequest, principal),
            CancellationToken.None);
        var clientFeedbackPostResult = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(feedbackPostRequest, principal),
            CancellationToken.None);
        var feedbackOutsideSelectionResult = await authorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(feedbackOutsideSelectionRequest, principal),
            CancellationToken.None);
        var clientFeedbackOutsideSelectionResult = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(feedbackOutsideSelectionRequest, principal),
            CancellationToken.None);

        // Assert
        if (expectedAllowed)
        {
            Assert.True(handoffResult.IsSuccess, handoffResult.Error?.Message);
            Assert.True(feedbackResult.IsSuccess, feedbackResult.Error?.Message);
            Assert.True(correctionResult.IsSuccess, correctionResult.Error?.Message);
            Assert.True(clientCorrectionResult.IsSuccess, clientCorrectionResult.Error?.Message);
            Assert.True(feedbackPostResult.IsSuccess, feedbackPostResult.Error?.Message);
            Assert.True(clientFeedbackPostResult.IsSuccess, clientFeedbackPostResult.Error?.Message);
        }
        else
        {
            Assert.Equal(RequestErrorKind.NotFound, handoffResult.Error?.Kind);
            Assert.Equal(RequestErrorKind.NotFound, feedbackResult.Error?.Kind);
            Assert.Equal(RequestErrorKind.NotFound, correctionResult.Error?.Kind);
            Assert.Equal(RequestErrorKind.NotFound, clientCorrectionResult.Error?.Kind);
        }
        Assert.Equal(RequestErrorKind.NotFound, feedbackOutsideSelectionResult.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, clientFeedbackOutsideSelectionResult.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorCorrectionGivenFeedbackAuthoredByAnotherMember()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider,
            fixture.Tenant, fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var recordVersionId = Uuid.CreateVersion4();
        var at = fixture.Clock.GetUtcNow().AddDays(-2);
        var lead = ActorReference.ForMember(RbacIds.Member(fixture.Tenant,
            Uuid.CreateVersion4()), "Compliance lead");
        var otherAuthor = ActorReference.ForMember(RbacIds.Member(fixture.Tenant,
            Uuid.CreateVersion4()), "Another Advisor");
        var other = new AdvisorRecordVersion(recordId, recordVersionId);
        await AdvisorSeed.HandoffAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, [other], lead, at,
            DateOnly.FromDateTime(at.UtcDateTime).AddDays(30), purpose: "Readiness review");
        await AdvisorSeed.FeedbackAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, feedbackId,
            other, "comment", "Clarify the control owner.", otherAuthor, at.AddMinutes(2));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var authorizer = new ProgramManagementAuthorizer(fixture.Memberships, fixture.Tenants,
            scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<IProgramResourceScopeResolver>(),
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        var clientMutationAuthorizer = new ClientManagementMutationAuthorizer(
            scope.ServiceProvider.GetRequiredService<ClientManagementIndependenceGuard>(),
            fixture.Memberships,
            scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>());
        var request = new CorrectAdvisorFeedback(fixture.Tenant,
            fixture.Program, handoffId, feedbackId, Uuid.CreateVersion4(), "Corrected wording.",
            "The original wording was incomplete.", 1);

        // Act
        var result = await authorizer.AuthorizeAsync(new RequestContext<IProgramManagementRequest>(
            request, ProgramManagementServices.Actor(fixture.User)), CancellationToken.None);
        var clientMutationResult = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(request,
                ProgramManagementServices.Actor(fixture.User)), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, clientMutationResult.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenAcceptanceBoundToAnotherProgram()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(Uuid.CreateVersion4());
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var recordId = Uuid.CreateVersion4();
        var recordVersionId = Uuid.CreateVersion4();
        var author = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.User),
            "Assigned Advisor");
        var lead = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4()),
            "Compliance lead");
        var at = fixture.Clock.GetUtcNow().AddDays(-1);
        var shared = new AdvisorRecordVersion(recordId, recordVersionId);
        await AdvisorSeed.HandoffAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, [shared], lead, at,
            DateOnly.FromDateTime(fixture.Clock.GetUtcNow().UtcDateTime).AddDays(30), purpose: "Readiness review");
        await AdvisorSeed.FeedbackAsync(fixture.Provider, fixture.Tenant, fixture.Program, handoffId, feedbackId,
            shared, "comment", "Please clarify the control owner.", author, at.AddMinutes(2));
        await using var scope = fixture.Provider.CreateAsyncScope();
        var advisoryRead = scope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>();
        var programAuthorizer = new ProgramManagementAuthorizer(fixture.Memberships, fixture.Tenants,
            scope.ServiceProvider.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            scope.ServiceProvider.GetRequiredService<IProgramResourceScopeResolver>(), advisoryRead);
        var clientMutationAuthorizer = new ClientManagementMutationAuthorizer(
            scope.ServiceProvider.GetRequiredService<ClientManagementIndependenceGuard>(),
            fixture.Memberships, advisoryRead);
        var feedbackPostRequest = new RecordAdvisorFeedback(fixture.Tenant, fixture.Program,
            handoffId, Uuid.CreateVersion4(), recordId, recordVersionId, "comment",
            "Please clarify the evidence owner.", 2);
        var correctionRequest = new CorrectAdvisorFeedback(fixture.Tenant, fixture.Program,
            handoffId, feedbackId, Uuid.CreateVersion4(), "Corrected wording.",
            "The original wording was incomplete.", 1);
        var principal = ProgramManagementServices.Actor(fixture.User);

        // Act
        var notes = await fixture.NotesAsync();
        var feedbackPost = await programAuthorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(feedbackPostRequest, principal),
            CancellationToken.None);
        var feedbackPostClient = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(feedbackPostRequest, principal),
            CancellationToken.None);
        var correction = await programAuthorizer.AuthorizeAsync(
            new RequestContext<IProgramManagementRequest>(correctionRequest, principal),
            CancellationToken.None);
        var correctionClient = await clientMutationAuthorizer.AuthorizeAsync(
            new RequestContext<IClientManagementMutationRequest>(correctionRequest, principal),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, feedbackPost.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, feedbackPostClient.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, correction.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, correctionClient.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAllowAssignedAdvisorReadGivenExactApprovedBoundaryInRequestedProgram()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program);
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync(true);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unapproved")]
    [InlineData("foreign_tenant")]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("approval")]
    public async Task ShouldDenyAssignedAdvisorReadGivenMissingOrMismatchedApprovedBoundarySource(string mismatch)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program,
            seedApproval: mismatch != "unapproved",
            sourceTenantId: mismatch == "foreign_tenant" ? Uuid.CreateVersion4() : null);
        var version = binding.Version;
        var approval = binding.Approval;
        if (mismatch == "missing")
        {
            var missingId = Uuid.CreateVersion4();
            version = version with { BoundaryId = missingId };
            approval = approval with { BoundaryId = missingId };
        }
        else if (mismatch == "version")
        {
            var otherVersion = Uuid.CreateVersion4();
            version = version with { VersionId = otherVersion };
            approval = approval with { VersionId = otherVersion };
        }
        else if (mismatch == "revision")
        {
            version = version with { Revision = 2 };
            approval = approval with { Revision = 2 };
        }
        else if (mismatch == "approval")
            approval = approval with { DecisionId = Uuid.CreateVersion4() };
        // Synthetic acceptance capsules exercise current authoritative source verification.
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: version, boundaryApproval: approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPreserveAssignedAdvisorReadGivenSuccessorToAcceptedBoundaryVersion()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program);
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        await fixture.SeedBoundarySuccessorAsync(binding.Version);

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldPreserveAcceptedAdvisorAccessGivenLaterUnratifiedRuleDraft()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        await fixture.SeedLaterUnratifiedRuleDraftAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenRetainedAttestHistoryForTheSameClient()
    {
        // Arrange
        await using var advisorFixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(advisorFixture.Provider,
            advisorFixture.Tenant, advisorFixture.User, practice: "advisory");
        await advisorFixture.SeedStaffDirectoryAsync(staff);
        advisorFixture.Memberships.State = "absent";
        advisorFixture.Permissions.Allowed = false;
        await using var attestFixture = await Fixture.CreateAsync(advisorFixture.Tenant, advisorFixture.User);
        await AttestAssignmentHistoryFixture.SeedAsync(attestFixture.Provider, advisorFixture.Tenant,
            advisorFixture.User);
        await using var advisorScope = advisorFixture.Provider.CreateAsyncScope();
        await using var attestScope = attestFixture.Provider.CreateAsyncScope();
        var professionalAccess = advisorScope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>();
        var authorizer = new ReadinessAnnotationCompartmentAuthorizer(
            advisorScope.ServiceProvider.GetRequiredService<ITenantMembershipDirectoryReader>(),
            attestScope.ServiceProvider.GetRequiredService<ClientCompartmentIndependenceGuard>(),
            professionalAccess);
        var request = new ListReadinessAnnotations(advisorFixture.Tenant, advisorFixture.Program,
            advisorFixture.Assessment);

        // Act
        var isAssignedAndCurrent = await professionalAccess.CanReadAsync(advisorFixture.Tenant,
            advisorFixture.Program, advisorFixture.User);
        var result = await authorizer.AuthorizeAsync(new RequestContext<ListReadinessAnnotations>(request,
            ProgramManagementServices.Actor(advisorFixture.User)), CancellationToken.None);

        // Assert
        Assert.True(isAssignedAndCurrent);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("mismatched")]
    public async Task ShouldDenyAssignedAdvisorReadGivenInactiveOrMismatchedCurrentDirectoryIdentity(string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff, active: state != "inactive",
            userId: state == "mismatched" ? Uuid.CreateVersion4() : staff.UserId);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenInactiveTenant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        fixture.Tenants.IsActive = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenClosedAcceptedEngagement()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, revoked: true, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenExpiredEngagementPeriod()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        fixture.Clock.UtcNow = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("suspended")]
    [InlineData("deprovisioned")]
    [InlineData("wrong_user")]
    [InlineData("wrong_tenant")]
    public async Task ShouldPreserveMembershipPrivacyGivenHistoricalAttestUserWithoutExactCurrentMembership(string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, true);
        fixture.Memberships.State = state;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
        Assert.DoesNotContain("Attest", notes.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("compartment")]
    public async Task ShouldFailClosedGivenInvalidCompartmentEvaluationInputs(string invalid)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var allowed = await scope.ServiceProvider.GetRequiredService<ClientCompartmentIndependenceGuard>().CanReadAsync(
            invalid == "tenant" ? Uuid.Empty : fixture.Tenant,
            invalid == "user" ? Uuid.Empty : fixture.User,
            invalid == "compartment" ? (RecordCompartment)999 : RecordCompartment.AdvisoryWorkingNotes);

        // Assert
        Assert.False(allowed);
    }

    sealed class Fixture : IAsyncDisposable
    {
        Fixture(Uuid? tenant = null, Uuid? user = null)
        {
            Tenant = tenant ?? Uuid.CreateVersion4();
            User = user ?? Uuid.CreateVersion4();
        }

        public Uuid Tenant { get; }
        public Uuid User { get; }
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Assessment { get; } = Uuid.CreateVersion4();
        public ServiceProvider Provider { get; private set; } = null!;
        public Permissions Permissions { get; } = new();
        public Memberships Memberships { get; private set; } = null!;
        public TenantActivity Tenants { get; } = new();
        public MutableTimeProvider Clock { get; } = new();

        public static async Task<Fixture> CreateAsync(Uuid? tenant = null, Uuid? user = null)
        {
            var fixture = new Fixture(tenant, user);
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var events = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>(events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<ITenantActivity>(fixture.Tenants);
            services.AddSingleton<TimeProvider>(fixture.Clock);
            fixture.Memberships = new Memberships(fixture.Tenant, fixture.User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(fixture.Memberships);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(fixture.Permissions));
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var now = DateTimeOffset.UtcNow;
            var member = RbacIds.Member(fixture.Tenant, fixture.User);
            await ProgramManagementServices.SeedAsync(fixture.Provider, new ProgramRules(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), member, "Client administrator", now));
                return Result.Success;
            });
            var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risk", "risk_reviewed", "Shared management gap", []);
            await using var scope = fixture.Provider.CreateAsyncScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
            var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
            await ReadinessSeed.AdoptAsync(reader, executor, content, fixture.Tenant, fixture.Program,
                fixture.Assessment, new ReadinessEvaluation([], [], [gap], new string('a', 64)), now, member, now);
            var annotationId = Uuid.CreateVersion4();
            var actor = ActorReference.ForMember(member, "Attributed feedback author");
            var plan = new ReadinessGapPlan(fixture.Tenant, fixture.Program, gap.GapId, annotationId);
            var annotation = new ReadinessAnnotationView(annotationId, fixture.Assessment, gap.GapId,
                "Advisor working feedback", member, actor, now);
            var body = await ReadinessContent.WriteAsync(content, fixture.Tenant, ReadinessContent.AnnotationSchema,
                annotation, CancellationToken.None);
            var (response, result) = await ReadinessContent.FreezeAsync(content, fixture.Tenant,
                ReadinessContent.AnnotationViewSchema, annotation, CancellationToken.None);
            var first = await FirstResponses.FreezeAsync(content, fixture.Tenant.ToString(), response);
            var operation = ReadinessContent.Provenance(annotationId, "AnnotateReadinessGap", plan.Stream, actor, 0,
                new string('b', 64), now);
            await ProgramManagementServices.SeedAsync(fixture.Provider, plan,
                item => item.Annotate(operation, body, first, result));
            return fixture;
        }

        public Task<Result<Page<ReadinessAnnotationView>>> NotesAsync(bool mcp = false) =>
            SendAsync(new ListReadinessAnnotations(Tenant, Program, Assessment), mcp);

        public async Task<(BoundaryVersionView Version, BoundaryDecisionView Approval)> SeedApprovedBoundaryAsync(
            Uuid programId, bool seedApproval = true, Uuid? sourceTenantId = null)
        {
            var at = new DateTimeOffset(2026, 10, 7, 11, 0, 0, TimeSpan.Zero);
            var author = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            var boundaryId = Uuid.CreateVersion4();
            var versionId = Uuid.CreateVersion5(boundaryId, "draft-1");
            var reviewId = Uuid.CreateVersion4();
            var approvalId = Uuid.CreateVersion4();
            var sourceTenant = sourceTenantId ?? Tenant;
            var content = new BoundaryContent("Synthetic scoped advisory work", "readiness", ["security"], []);
            if (programId != Program)
                await ProgramManagementServices.SeedAsync(Provider, new ProgramRules(sourceTenant, programId), program =>
                {
                    Assert.Null(program.Create("Other program", new ProgramPlan(null, null, null, null, null, null),
                        author, "Synthetic author", at));
                    return Result.Success;
                });
            await ProgramManagementServices.SeedAsync(Provider, new BoundaryRules(sourceTenant, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(programId, versionId, content, author, "Synthetic author", at).IsSuccess);
                Assert.Null(boundary.Review(versionId, 1, reviewId, "accept", "Synthetic review", reviewer,
                    "Synthetic reviewer", at));
                if (seedApproval)
                    Assert.Null(boundary.Approve(versionId, 1, approvalId, reviewId, new DateOnly(2026, 1, 1),
                        "Synthetic approval", "synthetic-digest", reviewer, "Synthetic reviewer", at));
                return Result.Success;
            });
            return (new BoundaryVersionView(Tenant, boundaryId, programId, versionId, 1, content, "approved",
                new DateOnly(2026, 1, 1), author, "Synthetic author", at),
                new BoundaryDecisionView(Tenant, boundaryId, approvalId, versionId, 1, "approve", reviewer,
                    "Synthetic reviewer", "Synthetic approval", at, null, null, "synthetic-digest"));
        }

        public async Task SeedBoundarySuccessorAsync(BoundaryVersionView original)
        {
            var at = original.ChangedAt.AddDays(1);
            var successorCommand = Uuid.CreateVersion4();
            var successor = Uuid.CreateVersion5(successorCommand, "draft");
            var review = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(Provider, new BoundaryRules(Tenant, original.BoundaryId), boundary =>
            {
                Assert.Null(boundary.ProposeSuccessor(original.VersionId, successor,
                    original.Content with { Statement = "Synthetic successor scope" }, original.AuthorMemberId,
                    original.AuthorDisplay, at, commandId: successorCommand));
                Assert.Null(boundary.Review(successor, 1, review, "accept", "Synthetic successor review",
                    reviewer, "Synthetic reviewer", at));
                Assert.Null(boundary.Approve(successor, 1, Uuid.CreateVersion4(), review, new DateOnly(2026, 1, 2),
                    "Synthetic successor approval", "synthetic-successor-digest", reviewer, "Synthetic reviewer", at));
                Assert.Equal(successor, boundary.LatestApprovedVersionId);
                return Result.Success;
            });
        }

        public async Task SeedStaffDirectoryAsync(FirmStaffMemberView staff, bool active = true,
            Uuid? userId = null)
        {
            await FirmStaffFacts.RegisterAsync(Provider, staff.StaffMemberId, userId ?? staff.UserId,
                staff.Practice, "Synthetic current directory identity", staff.Actor, staff.RecordedAt);
            if (!active)
                await FirmStaffFacts.SetStatusAsync(Provider, staff.StaffMemberId, false,
                    "Synthetic inactive directory state", staff.RecordedAt.AddSeconds(1), staff.Actor);
        }

        public async Task SeedLaterUnratifiedRuleDraftAsync(FirmStaffMemberView staff)
        {
            await ProgramManagementServices.SeedAsync(Provider, new IndependenceRuleCatalogRules(), catalog =>
            {
                var content = new IndependenceRuleContent(12,
                    [new("design", "impairing", "impairing"),
                     new("readiness", "conditionally_compatible", "impairing")],
                    "Synthetic unratified rule draft");
                var first = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, content,
                    staff.Actor, staff.RecordedAt.AddMinutes(1));
                Assert.True(first.IsSuccess, first.Error?.Message);
                var second = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence,
                    content with { SourceReference = "Synthetic later unratified rule draft" },
                    staff.Actor, staff.RecordedAt.AddMinutes(2));
                Assert.True(second.IsSuccess, second.Error?.Message);
                Assert.False(second.Value!.IsRatified);
                return Result.Success;
            });
        }

        public async Task<Result<T>> SendAsync<T>(IRequest<T> request, bool mcp = false)
        {
            await using var scope = Provider.CreateAsyncScope();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(User),
                mcp ? new McpInvocation("synthetic.readiness.read") : new HttpInvocation("GET", "/synthetic/readiness", "/synthetic/readiness", "synthetic"));
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, context, CancellationToken.None);
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenant, Uuid user, Uuid member, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(Allowed && permission == IProgramReadRequest.ReadPermission);
    }

    sealed class Memberships(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public string State { get; set; } = "active";
        public string Affiliation { get; set; } = "client_personnel";
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user && State != "absent"
                ? new TenantMembershipView(State == "wrong_user" ? Uuid.CreateVersion4() : user,
                    State == "wrong_tenant" ? Uuid.CreateVersion4() : tenant, Affiliation,
                    IsSuspended: State == "suspended", IsDeprovisioned: State == "deprovisioned") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class TenantActivity : ITenantActivity
    {
        public bool IsActive { get; set; } = true;

        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(IsActive);
    }

    sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
