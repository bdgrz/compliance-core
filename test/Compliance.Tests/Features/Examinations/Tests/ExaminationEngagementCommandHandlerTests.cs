using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Readers;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Examinations.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
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
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Responsibilities.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Commands;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Handlers;
using Bdgrz.Compliance.Features.Examinations.Queries;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Readiness.Aggregates;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Authorizers;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ExaminationEngagementCommandHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 17, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReplayOriginalCreateAndRevisionGivenLaterEngagementTerms()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId, 1, "Revised LLP", new DateOnly(2026, 9, 30),
            null, ["security"]);
        Assert.True((await fixture.ReviseHandler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var next = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, AuditFirm = "Latest LLP" };
        Assert.True((await fixture.ReviseHandler().HandleAsync(fixture.Context(next), CancellationToken.None)).IsSuccess);
        var before = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Act
        var replay = await fixture.ReviseHandler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var changed = await fixture.ReviseHandler().HandleAsync(fixture.Context(request with { ExpectedRevision = 2 }),
            CancellationToken.None);
        var after = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);
        var originalCreate = after.Rules.Create(new ExaminationEngagementContent("type_i", "Original LLP",
            new DateOnly(2026, 9, 30), null, ["security"], fixture.BoundaryId, []),
            RbacIds.Member(fixture.TenantId, fixture.UserId), "Renamed member", Now.AddHours(1));

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Null(originalCreate);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
        Assert.Equal("Latest LLP", after.View!.AuditFirm);
    }

    [Fact]
    public async Task ShouldReplayOriginalEngagementCreateGivenLaterDeferredEntryDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Approve);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);
        Assert.True((await fixture.CreateHandler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Defer);

        // Act
        var replay = await fixture.CreateHandler().HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(1, saved.Owner.Revision);
    }

    [Fact]
    public async Task ShouldReplayOriginalNoOpRevisionGivenLaterTermsAndUnavailableHistoricalContent()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, fixture.EngagementId, 1, "Original LLP", new DateOnly(2026, 9, 30), null, ["security"]);
        Assert.True((await fixture.ReviseHandler().HandleAsync(fixture.Context(request), CancellationToken.None)).IsSuccess);
        var later = request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 2, AuditFirm = "Later LLP" };
        Assert.True((await fixture.ReviseHandler().HandleAsync(fixture.Context(later), CancellationToken.None)).IsSuccess);
        var before = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);
        var original = Assert.Single(before.History.Facts.OfType<ExaminationEngagementRevised>(), fact =>
            fact.Operation.LogicalId == request.CommandId);
        var unavailable = new UnavailableStateContent(fixture.Content);
        var operations = new ExaminationEngagementOperations(new NativeOperationExecutor(fixture.Reader, fixture.Executor,
            unavailable), new ExaminationEngagementReader(fixture.Reader, fixture.Events, unavailable), unavailable, fixture.Clock);
        var handler = new ReviseExaminationEngagementHandler(operations, fixture.Reader);

        // Act
        var replay = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var after = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);
        var response = await FirstResponses.ReplayAsync(fixture.Content, fixture.TenantId.ToString(), original.FirstResponse!);
        var result = await fixture.Content.ReadJsonAsync<MutationResultContent>(fixture.TenantId.ToString(), original.OperationResult!);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(0, unavailable.StateReads);
        Assert.Equal(3, after.Owner.Revision);
        Assert.Equal(before.Owner.CommittedStreamPosition, after.Owner.CommittedStreamPosition);
        Assert.Equal("Later LLP", after.View!.AuditFirm);
        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(UnitResult.Schema, result.Schema);
    }

    [Fact]
    public async Task ShouldResumeOriginalRevisionWithoutDuplicateFactGivenLostAppendAcknowledgment()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated LLP", new DateOnly(2026, 9, 30), null, ["security"]);
        var operations = new ExaminationEngagementOperations(new NativeOperationExecutor(fixture.Reader,
            new CommitThenFailOnceExecutor(fixture.Executor), fixture.Content), fixture.Examinations, fixture.Content, fixture.Clock);
        var handler = new ReviseExaminationEngagementHandler(operations, fixture.Reader);

        // Act
        var first = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var replay = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(2, saved.Owner.Revision);
        Assert.Single(saved.History.Facts.OfType<ExaminationEngagementRevised>());
    }

    [Fact]
    public async Task ShouldRejectCreateGivenNoApprovedTypeIEntryDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Owner.Revision);
    }

    [Fact]
    public async Task ShouldRejectCreateGivenDeferredTypeIEntryDecision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Defer);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, saved.Owner.Revision);
    }

    [Fact]
    public async Task ShouldCreateTypeIEngagementGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Approve);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.View!.Status);
        Assert.Equal(1, saved.Owner.Revision);
        Assert.Equal("Independent Audit LLP", saved.View.AuditFirm);
        Assert.Equal(fixture.BoundaryId, saved.View.BoundaryId);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.View.ActorMemberId);
    }

    [Fact]
    public async Task ShouldCreateTypeIEngagementGivenAuthenticatedMcpCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Approve);
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request,
            new McpInvocation("bdgrz.examinations.create-examination-engagement")),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("draft", saved.View?.Status);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.View?.ActorMemberId);
    }

    [Fact]
    public async Task ShouldCaptureActiveManagementContactsGivenCreate()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Approve);
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "client_personnel");
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId,
            ManagementContactMemberIds: [contactMemberId]);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([contactMemberId], saved.View!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldRejectInactiveManagementContactGivenCreate()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedTypeIEntryDecisionAsync(ProgramReadiness.Approve);
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "firm_staff");
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security", "availability"], fixture.BoundaryId,
            ManagementContactMemberIds: [contactMemberId]);

        // Act
        var result = await fixture.CreateHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, saved.Owner.Revision);
    }

    [Fact]
    public async Task ShouldReviseDraftAtExpectedRevisionGivenPersonalCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Updated Independent Audit LLP", saved.View!.AuditFirm);
        Assert.Equal(2, saved.Owner.Revision);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.UserId), saved.View.ActorMemberId);
    }

    [Fact]
    public async Task ShouldReplaceManagementContactsGivenActiveClientMembers()
    {
        // Arrange
        await using var fixture = new Fixture();
        var previousUserId = Uuid.CreateVersion4();
        var replacementUserId = Uuid.CreateVersion4();
        var previousMemberId = RbacIds.Member(fixture.TenantId, previousUserId);
        var replacementMemberId = RbacIds.Member(fixture.TenantId, replacementUserId);
        await fixture.SeedMemberAsync(previousUserId, "client_personnel");
        await fixture.SeedMemberAsync(replacementUserId, "client_personnel");
        await fixture.SeedEngagementAsync([previousMemberId]);
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"],
            ManagementContactMemberIds: [replacementMemberId]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([replacementMemberId], saved.View!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldPreserveManagementContactsGivenRevisionWithoutContactField()
    {
        // Arrange
        await using var fixture = new Fixture();
        var contactUserId = Uuid.CreateVersion4();
        var contactMemberId = RbacIds.Member(fixture.TenantId, contactUserId);
        await fixture.SeedMemberAsync(contactUserId, "client_personnel");
        await fixture.SeedEngagementAsync([contactMemberId]);
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 1, "Updated Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([contactMemberId], saved.View!.ManagementContactMemberIds);
    }

    [Fact]
    public async Task ShouldReadCurrentEngagementGivenTenantProgramAndEngagementIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new GetExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Examinations)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(fixture.EngagementId, result.Value.ExaminationEngagementId);
        Assert.Equal("draft", result.Value.Status);
        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownEngagement()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new GetExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Examinations)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenEngagementFromDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new GetExaminationEngagement(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.EngagementId);

        // Act
        var result = await new GetExaminationEngagementHandler(fixture.Examinations)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReadFrozenBaselineGivenTenantProgramAndBaselineIdentity()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var request = new GetTypeIBaseline(fixture.TenantId, fixture.ProgramId,
            fixture.BaselineId);

        // Act
        var result = await new GetTypeIBaselineHandler(fixture.Directory, fixture.Events, fixture.Examinations, fixture.Content)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(fixture.TenantId, result.Value.TenantId);
        Assert.Equal(fixture.ProgramId, result.Value.ProgramId);
        Assert.Equal(fixture.EngagementId, result.Value.ExaminationEngagementId);
        Assert.Equal(fixture.BaselineId, result.Value.TypeIBaselineId);
        Assert.Equal("frozen", result.Value.Status);
        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenBaselineFromDifferentProgram()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedBaselineAsync();
        var request = new GetTypeIBaseline(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.BaselineId);

        // Act
        var result = await new GetTypeIBaselineHandler(fixture.Directory, fixture.Events, fixture.Examinations, fixture.Content)
            .HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionWithoutAppendingGivenDraftRevision()
    {
        // Arrange
        await using var fixture = new Fixture();
        await fixture.SeedEngagementAsync();
        var request = new ReviseExaminationEngagement(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, 0, "Stale change", new DateOnly(2026, 9, 30), null,
            ["security"]);

        // Act
        var result = await fixture.ReviseHandler().HandleAsync(fixture.Context(request),
            CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(1, saved.Owner.Revision);
    }

    [Fact]
    public async Task ShouldRequirePersonalHttpActorGivenCreateCommand()
    {
        // Arrange
        await using var fixture = new Fixture();
        var request = new CreateExaminationEngagement(fixture.TenantId, fixture.ProgramId,
            fixture.EngagementId, "type_i", "Independent Audit LLP", new DateOnly(2026, 9, 30),
            null, ["security"], fixture.BoundaryId);
        var context = new Cntryl.Portia.RequestContext<CreateExaminationEngagement>(request,
            new RequestDispatchContext(RequestActor.System,
                new HttpInvocation("POST", "/examinations", "/examinations", "examination-test")));

        // Act
        var result = await fixture.CreateHandler().HandleAsync(context, CancellationToken.None);
        var saved = await fixture.Examinations.ReadAsync(fixture.TenantId, fixture.ProgramId, fixture.EngagementId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, saved.Owner.Revision);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid EngagementId { get; } = Uuid.CreateVersion4();
        public Uuid BaselineId { get; } = Uuid.CreateVersion4();
        public Uuid BoundaryId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        public ExaminationEngagementReader Examinations => new(Reader, Events, Content);
        public ExaminationEngagementOperations Operations => new(new NativeOperationExecutor(Reader, Executor, Content),
            Examinations, Content, Clock);
        public IAggregateReader Reader { get; }
        public IAggregateWriter Writer { get; }
        public IAggregateExecutor Executor { get; }
        public FitzExaminationEngagementDirectory Directory { get; }
        public InMemoryEventStore Events { get; } = new();
        public IImmutableContentStore Content { get; } = new FitzKvImmutableContentStore(new InMemoryKvClient());
        public TimeProvider Clock { get; } = new FixedTimeProvider(Now);

        ClaimsPrincipal Actor => new(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", UserId.ToString()),
                new Claim("email", "examiner@example.test")], "BdgrzSession"));

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(Events);
            services.AddPortia();
            ExaminationSeeds.Configure(services, Events);
            Directory = new FitzExaminationEngagementDirectory(new InMemoryKvClient(), Content);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            Writer = _scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
            Executor = _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        }

        public CreateExaminationEngagementHandler CreateHandler() => new(Operations, Reader, new ReadinessReader(Reader, Content, Events));

        public ReviseExaminationEngagementHandler ReviseHandler() => new(Operations, Reader);

        public Cntryl.Portia.RequestContext<T> Context<T>(T request,
            RequestInvocation? invocation = null) where T : IRequestBase => new(request,
            new RequestDispatchContext(Actor, invocation ?? new HttpInvocation("POST",
                "/api/v1/examinations", "/api/v1/examinations", "examination-engagement-test")));

        public async Task SeedEngagementAsync(IReadOnlyList<Uuid>? managementContactMemberIds = null)
        {
            var aggregate = new ExaminationEngagementRules(TenantId, ProgramId, EngagementId);
            Assert.Null(aggregate.Create(new ExaminationEngagementContent("type_i", "Original LLP",
                new DateOnly(2026, 9, 30), null, ["security"], BoundaryId,
                managementContactMemberIds ?? []),
                RbacIds.Member(TenantId, UserId), "Examiner", Now));
            await ExaminationSeeds.WriteAsync(Reader, Writer, Events, Content, aggregate);
            await ProjectAsync();
        }

        public async Task SeedMemberAsync(Uuid userId, string affiliation)
        {
            var member = new Member(TenantId, userId);
            Assert.True(member.Register(affiliation).IsSuccess);
            await Writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        }

        public async Task SeedTypeIEntryDecisionAsync(string outcome)
        {
            var assessmentId = Uuid.CreateVersion4();
            var runnerMemberId = RbacIds.Member(TenantId, UserId);
            var approverMemberId = RbacIds.Member(TenantId, Uuid.CreateVersion4());
            await ReadinessSeed.AdoptAsync(Reader, Executor, Content, TenantId, ProgramId, assessmentId,
                new ReadinessEvaluation([], [], [], new string('a', 64)), Now, runnerMemberId, Now);
            if (outcome != ProgramReadiness.Defer)
                await ReadinessSeed.DecideAsync(Reader, Executor, Content, TenantId, ProgramId, assessmentId,
                    ReadinessDecisionKind.Readiness, ProgramReadiness.Proceed, "Management decided to proceed",
                    approverMemberId, Now);
            await ReadinessSeed.DecideAsync(Reader, Executor, Content, TenantId, ProgramId, assessmentId,
                ReadinessDecisionKind.TypeIEntry, outcome, "Type I entry decision", approverMemberId, Now);

        }

        public async Task SeedBaselineAsync()
        {
            await SeedEngagementAsync();
            var snapshot = await Examinations.ReadAsync(TenantId, ProgramId, EngagementId);
            var source = new TypeIBaselineSourceReference("approved_boundary", BoundaryId,
                Uuid.CreateVersion4(), 1, new string('b', 64));
            var preview = TypeIBaselinePreviewTestFactory.Create(TenantId, ProgramId, EngagementId, [source]);
            Assert.Null(snapshot.Rules.FreezeBaseline(snapshot.Owner.Revision, BaselineId, preview, "Confirmed",
                RbacIds.Member(TenantId, UserId), "Examiner", Now.AddMinutes(1)));
            await ExaminationSeeds.WriteAsync(Reader, Writer, Events, Content, snapshot.Rules);
            await ProjectAsync();
        }

        async Task ProjectAsync()
        {
            var pattern = EventStreamPattern.ForPattern(TenantId.ToString(), "examinations");
            var identity = new CheckpointIdentity(FitzExaminationEngagementDirectory.ProjectorName, pattern);
            var before = await Directory.LoadCheckpointAsync(TenantId);
            await using var batch = await Directory.BeginAsync(new ProjectionBatchContext(identity, before));
            var cursor = before.Cursor;
            await foreach (var record in Events.ReadAsync(pattern, before.Cursor, CancellationToken.None))
            {
                await Directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class UnavailableStateContent(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int StateReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == ExaminationEngagementState.Schema)
            {
                StateReads++;
                throw new ImmutableContentUnavailableException("The historical state body is not readable yet.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
