using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.AcceptanceFacts;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
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
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class ServiceEngagementAcceptanceTests
{
    [Fact]
    public async Task ShouldAcceptEngagementAfterEveryPermanentWallIsCommittedGivenCompletePairedDecisions()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var admitted = await host.HttpAsync(command, partner.Actor);
        var beforeProcess = await host.EventsAsync<DomainEvent>(stream);
        var record = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(command.CommandId, admitted.Value!.OperationId);
        Assert.Equal(nameof(ServiceEngagementAcceptanceRequested), beforeProcess[^1].GetType().Name);
        Assert.Equal(ProcessState.Completed, record.State);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal("accepted", owner.Status);
        Assert.Equal(2UL, owner.Revision);
        var operation = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).OperationOf(command.CommandId)!;
        Assert.NotNull(operation.Authorized);
        Assert.NotNull(operation.Completed);
        Assert.Null(operation.Rejected);
        Assert.False(owner.HasPendingOperation);
        var view = await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement));
        Assert.True(view.IsSuccess, view.Error?.Message);
        Assert.Equal("active", view.Value!.Status);
        Assert.Equal(partner.Staff.UserId, view.Value.PartnerUserId);
        Assert.Equal(operation.Authorized!.DecisionAt, view.Value.RecordedAt);
        var lead = Assert.Single(view.Value.Assignments);
        foreach (var kind in new[] { PracticeSubjectKind.Staff, PracticeSubjectKind.CanonicalUser })
        {
            var subject = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, kind,
                kind == PracticeSubjectKind.Staff ? lead.StaffMemberId : lead.UserId));
            Assert.Equal("attest", subject.Practice);
            Assert.NotNull((await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), subject)).Commitments.SingleOrDefault(value => value.Operation.RootOperationId == command.CommandId));
            Assert.False(subject.Uses.HasActiveUse);
        }

        var engagementView = await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement));
        Assert.Equal("accepted", engagementView.Value!.Status);
        Assert.Equal(2, engagementView.Value.Revision);
        var history = await host.SendAsync(new GetServiceEngagementAcceptanceHistory(host.Tenant, engagement));
        Assert.Single(history.Value!);
        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldAcceptAndAssignGivenTheRootReactorStartsEachProcessFromItsCommittedAdmission()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host, null, "attest");
        var engagement = await SeedOpenEngagementAsync(host);
        await PairedDecisionsAsync(host, engagement, partner);
        var accept = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(accept, partner.Actor)).IsSuccess);
        Assert.Null(await RecordOfAsync(host, engagement, accept.CommandId));

        // Act: the reactor, not the test, starts and drives the acceptance.
        await RunRootReactorAsync(host);
        var accepted = await RecordOfAsync(host, engagement, accept.CommandId);
        var assignee = await host.RegisterStaffAsync("attest");
        var assign = new AssignServiceEngagementStaff(Uuid.CreateVersion4(), host.Tenant, engagement,
            assignee.StaffMemberId, 2, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.True((await host.HttpAsync(assign, partner.Actor)).IsSuccess);
        await RunRootReactorAsync(host);
        var assigned = await RecordOfAsync(host, engagement, assign.CommandId);

        // Assert
        Assert.Equal(ProcessState.Completed, accepted!.State);
        Assert.Equal(ProcessState.Completed, assigned!.State);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal("accepted", owner.Status);
        Assert.Equal(3UL, owner.Revision);
        Assert.Equal(2, (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!
            .Assignments.Count);
    }

    [Fact]
    public async Task ShouldAcceptAdvisoryEngagementWithoutPartnerEvaluationGivenAcknowledgementOnly()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host, "advisory");
        var command = AcceptCommand(host, engagement);

        // Act
        var admitted = await host.HttpAsync(command, partner.Actor);
        var record = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, record.State);
        var view = (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!;
        Assert.Null(view.PartnerEvaluation);
        Assert.Equal("not_applicable", view.EvaluationOutcome);
        var lead = Assert.Single(view.Assignments);
        Assert.Equal("advisory", (await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.CanonicalUser, lead.UserId))).Practice);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcknowledgementGivenExactRetryAfterLaterServiceChange()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        var first = await host.HttpAsync(command, partner.Actor);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service()))).IsSuccess);

        // Act
        var retried = await host.HttpAsync(command, partner.Actor);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(IndependenceHost.Json(first.Value), IndependenceHost.Json(retried.Value));
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var requested = Assert.Single(await host.EventsAsync<ServiceEngagementAcceptanceRequested>(stream));
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, host.Tenant.ToString(),
            requested.FirstResponse!)).HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameIdentityWithChangedRevisionOrCaller()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var revision = await host.HttpAsync(command with { ExpectedRevision = 2 }, partner.Actor);
        var otherEngagement = await host.HttpAsync(command with { EngagementId = Uuid.CreateVersion4() }, partner.Actor);
        var otherCaller = await host.HttpAsync(command, IndependenceHost.ActorFor(Uuid.CreateVersion4()));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, revision.Error?.Kind);
        Assert.False(otherEngagement.IsSuccess);
        Assert.False(otherCaller.IsSuccess);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenStaleRevisionUnknownEngagementOrCallerWithoutPersonalPartnerAuthority()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var stale = await host.HttpAsync(AcceptCommand(host, engagement, 0), partner.Actor);
        var ahead = await host.HttpAsync(AcceptCommand(host, engagement, 5), partner.Actor);
        var unknown = await host.HttpAsync(AcceptCommand(host, Uuid.CreateVersion4()), partner.Actor);
        var negative = await host.HttpAsync(AcceptCommand(host, engagement, -1), partner.Actor);
        var empty = await host.HttpAsync(AcceptCommand(host, engagement) with { CommandId = Uuid.Empty }, partner.Actor);
        var client = await host.HttpAsync(AcceptCommand(host, engagement));
        var nonHttp = await host.SendAsync(AcceptCommand(host, engagement), partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, ahead.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, empty.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, client.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, nonHttp.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRequested>(stream));
    }

    [Theory]
    [InlineData("acknowledgement")]
    [InlineData("evaluation")]
    [InlineData("partner_acknowledgement")]
    public async Task ShouldRefuseAcceptanceWithoutAppendGivenPairedDecisionsIncomplete(string missing)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        switch (missing)
        {
            case "acknowledgement":
                var onlyEvaluation = await EvaluationAsync(host, engagement, partner);
                Assert.True((await host.HttpAsync(onlyEvaluation, partner.Actor)).IsSuccess);
                Assert.Equal(ProcessState.Completed, (await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant,
                    engagement, onlyEvaluation.EvaluationId)).State);
                break;
            case "evaluation":
                await PairedDecisionsAsync(host, engagement, partner, evaluate: false);
                break;
            default:
                var byPartner = await AcknowledgementAsync(host, engagement);
                Assert.True((await host.HttpAsync(byPartner, partner.Actor)).IsSuccess);
                Assert.Equal(ProcessState.Completed, (await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant,
                    engagement, byPartner.AcknowledgementId)).State);
                var evaluation = await EvaluationAsync(host, engagement, partner);
                Assert.True((await host.HttpAsync(evaluation, partner.Actor)).IsSuccess);
                Assert.Equal(ProcessState.Completed, (await PersonalDecisionFacts.RunAsync(host.Provider, host.Tenant,
                    engagement, evaluation.EvaluationId)).State);
                break;
        }

        // Act
        var refused = await host.HttpAsync(AcceptCommand(host, engagement), partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRequested>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenAssigneeOrPartnerPermanentlyServesTheOppositePractice()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var lead = (await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement))).Value!.Staff.Single();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.CanonicalUser,
            lead.UserId, "advisory", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            ActorReference.ForFirmStaff(partner.Staff.UserId, "Synthetic partner"), IndependenceHost.At);

        // Act
        var refused = await host.HttpAsync(AcceptCommand(host, engagement), partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, refused.Error?.Kind);
        Assert.Contains(IndependenceCompartments.PersonExclusivityRuleId, refused.Error!.Message);
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRequested>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    [Fact]
    public async Task ShouldRejectAndLeaveNoWallGivenOppositePracticeCommittedAfterAdmissionBeforeReservation()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var lead = (await host.SendAsync(new GetServiceEngagement(host.Tenant, engagement))).Value!.Staff.Single();
        await PracticeCommitmentFacts.CommitSubjectAsync(host.Provider, host.Tenant, PracticeSubjectKind.Staff,
            lead.StaffMemberId, "advisory", Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            ActorReference.ForFirmStaff(partner.Staff.UserId, "Synthetic partner"), IndependenceHost.At);

        // Act
        var record = await RunAsync(host, engagement, command.CommandId);

        // Assert
        Assert.Equal(ProcessState.Aborted, record.State);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        Assert.Single(await host.EventsAsync<ServiceEngagementAcceptanceRejected>(stream));
        Assert.Empty(await host.EventsAsync<EngagementPracticeCommitmentsAuthorized>(stream));
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRecorded>(stream));
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Equal("draft", owner.Status);
        Assert.Equal(1UL, owner.Revision);
        Assert.False(owner.HasPendingOperation);
        var user = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant,
            PracticeSubjectKind.CanonicalUser, lead.UserId).ForSourceUse(candidate => EngagementAcceptanceEffects.UseKey(command.CommandId, stream, candidate.Stream)));
        Assert.Null(user.Practice);
        Assert.Null((await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), user)).Commitments.SingleOrDefault(value => value.Operation.RootOperationId == command.CommandId));
        var staff = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, PracticeSubjectKind.Staff,
            lead.StaffMemberId).ForSourceUse(candidate => EngagementAcceptanceEffects.UseKey(command.CommandId, stream, candidate.Stream)));
        Assert.Equal("advisory", staff.Practice);
        Assert.Null((await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), staff)).Commitments.SingleOrDefault(value => value.Operation.RootOperationId == command.CommandId));
        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
        foreach (var subject in new[] { user, staff })
            Assert.True(subject.Uses.Find(EngagementAcceptanceEffects.UseKey(command.CommandId, stream, subject.Stream))
                is { IsSettled: true });
    }

    [Fact]
    public async Task ShouldKeepEveryPermanentWallGivenRejectionSettlementAndClosureAfterAcceptance()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await RunAsync(host, engagement, command.CommandId);
        var lead = (await host.SendAsync(new GetServiceEngagementAcceptance(host.Tenant, engagement))).Value!
            .Assignments.Single();
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var late = await SendAsync(host, (await BuildAsync(host, record,
            nameof(RejectServiceEngagementAcceptance)))!);
        var settleAgain = new List<Result<ReceiverOutcome>>();
        foreach (var receiver in new[] { nameof(SettlePracticeIndependenceReservation), nameof(ReleaseEngagementServiceEpoch) })
            foreach (var target in record.Targets)
                if (await BuildAsync(host, record, receiver, target) is { } built)
                    settleAgain.Add(await SendAsync(host, built));
        var closed = await host.SendAsync(new RevokeServiceEngagementActualStaff(Uuid.CreateVersion4(), host.Tenant,
            engagement, lead.StaffMemberId, 2, "Synthetic client closure"));

        // Assert
        Assert.False(late.IsSuccess);
        Assert.False(late.Error!.IsTransient);
        Assert.NotEmpty(settleAgain);
        Assert.All(settleAgain, outcome => Assert.IsType<ReceiverOutcome.Replayed>(outcome.Value));
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        Assert.Empty(await host.EventsAsync<ServiceEngagementAcceptanceRejected>(stream));
        foreach (var kind in new[] { PracticeSubjectKind.Staff, PracticeSubjectKind.CanonicalUser })
        {
            var subject = await host.HydrateAsync(new PracticeIndependenceCommitment(host.Tenant, kind,
                kind == PracticeSubjectKind.Staff ? lead.StaffMemberId : lead.UserId));
            Assert.Equal("attest", subject.Practice);
            Assert.Equal(command.CommandId, Assert.Single((await PracticeCommitmentHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), subject)).Commitments).Operation.RootOperationId);
        }
    }

    [Fact]
    public async Task ShouldRefuseEngagementChangeGivenAcceptancePinsContentUntilSettlement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var lead = await host.RegisterStaffAsync("attest");

        // Act
        var amend = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Amended while pinned")));
        var close = await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            "Closed while pinned"));
        var second = await host.HttpAsync(AcceptCommand(host, engagement), partner.Actor);
        await RunAsync(host, engagement, command.CommandId);
        var amendAfter = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement,
            2, IndependenceHost.Draft(lead, "Amended after acceptance")));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, amend.Error?.Kind);
        Assert.True(amend.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, close.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, second.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, amendAfter.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReplayEveryReceiverAndSupersedeAcquireGivenRedeliveryAfterCompletion()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, partner) = await ReadyAsync(host);
        var command = AcceptCommand(host, engagement);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await RunAsync(host, engagement, command.CommandId);
        var definition = EngagementAcceptanceProcess.Definition();

        // Act
        var outcomes = new List<(string Receiver, Result<ReceiverOutcome> Outcome)>();
        foreach (var stage in definition.Stages)
            foreach (var receiver in stage.Receivers)
                foreach (var target in stage.IsPerTarget ? record.Targets.Cast<string?>() : new string?[] { null })
                    if (await BuildAsync(host, record, receiver, target) is { } built)
                        outcomes.Add((receiver, await SendAsync(host, built)));

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.NotEmpty(outcomes);
        foreach (var (receiver, outcome) in outcomes)
        {
            Assert.True(outcome.IsSuccess, $"{receiver}: {outcome.Error?.Message}");
            if (definition.Stages[0].Receivers.Contains(receiver) || receiver == nameof(AuthorizeEngagementPracticeCommitments))
            {
                // A grant or the authorization redelivered after authorization is a proven superseded no-op.
                Assert.True(outcome.Value is ReceiverOutcome.Superseded or ReceiverOutcome.Replayed, receiver);
                continue;
            }

            Assert.IsType<ReceiverOutcome.Replayed>(outcome.Value);
        }
    }
}
