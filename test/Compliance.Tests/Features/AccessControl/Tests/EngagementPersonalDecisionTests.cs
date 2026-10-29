using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using static Bdgrz.Compliance.Tests.Testing.PersonalDecisionFacts;
using Bdgrz.Compliance.Features.AccessControl.History;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
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
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Content.Services;
namespace Bdgrz.Compliance.Tests.Features.AccessControl.Tests;

public sealed class EngagementPersonalDecisionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShouldSettleHeldUsesGivenPersonalDecisionCanceledBeforeProcessCheckpoint(int stageIndex)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        var record = await StartAsync(host.Provider, host.Tenant, engagement, request.EvaluationId);
        var facts = new Dictionary<string, NativeFactReference>();
        foreach (var receiver in EngagementPersonalDecisionProcess.Definition().Stages[0].Receivers)
        {
            if (await BuildAsync(host, record, receiver) is not { } command)
                continue;
            var held = Assert.IsType<ReceiverOutcome.Applied>((await SendReceiverAsync(host.Provider, command)).Value);
            facts[receiver] = held.Fact;
        }
        record = await host.Scope.ServiceProvider.GetRequiredService<IProcessRecordStore>().ReplaceAsync(
            record with { StageIndex = stageIndex, Facts = facts }, record.Version);
        var canceled = await SendReceiverAsync(host.Provider, new CancelEngagementPersonalDecision(
            Uuid.CreateVersion4(), record.Source, record.Source.Stream, request.EvaluationId, 1, null,
            record.Intent, engagement));
        Assert.IsType<ReceiverOutcome.Applied>(canceled.Value);
        Assert.Contains(await UseEntriesAsync(host, partner, engagement, request.EvaluationId),
            item => item.Entry is { IsSettled: false });

        // Act
        var resumed = await RunAsync(host.Provider, host.Tenant, engagement, request.EvaluationId);

        // Assert
        Assert.All(await UseEntriesAsync(host, partner, engagement, request.EvaluationId),
            item => Assert.True(item.Entry is { IsSettled: true }));
        Assert.Equal(ProcessState.Aborted, resumed.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectServiceUseGivenWireEpochDiffersFromPinnedHistory(bool negative)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var request = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(request, partner.Actor)).IsSuccess);
        var record = await StartAsync(host.Provider, host.Tenant, engagement, request.EvaluationId);
        var command = Assert.IsType<AuthorizeClientServicePersonalDecisionUse>(
            await BuildAsync(host, record, nameof(AuthorizeClientServicePersonalDecisionUse)));
        var wire = JsonSerializer.SerializeToNode(command,
            ComplianceCoreJsonContext.Default.AuthorizeClientServicePersonalDecisionUse)!;
        wire["service_epoch"] = negative ? -1 : (long)((ISourceUseAuthorization)command).ExpectedContentRevision + 1;
        var changed = wire.Deserialize(ComplianceCoreJsonContext.Default.AuthorizeClientServicePersonalDecisionUse)!;

        // Act
        var result = await SendReceiverAsync(host.Provider, changed);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error?.IsTransient);
        Assert.False((await host.HydrateAsync(new ClientServiceIndependence(host.Tenant))).Uses.HasActiveUse);
    }

    [Fact]
    public async Task ShouldFencePersonalDecisionGivenSourceChangesBeforeAuthorization()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var engagementStream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act: step 1, D pins the engagement and freezes every required source use.
        var d = await EvaluationAsync(host, engagement, partner);
        var admitted = await host.HttpAsync(d, partner.Actor);
        var (intent, uses) = await IntentOfAsync(host, engagement, d.EvaluationId);

        // Act: step 2, the service epoch advances before any grant; the use rejects the stale epoch.
        var later = await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            ReadinessService()));
        var rejected = await RunAsync(host.Provider, host.Tenant, engagement, d.EvaluationId);

        // Act: step 3, delayed grant and authorize commands cannot resurrect D.
        var late = new List<Result<ReceiverOutcome>>();
        foreach (var receiver in new[]
                 {
                     nameof(AuthorizeClientServicePersonalDecisionUse),
                     nameof(AuthorizeFirmStaffRecordEngagementUse),
                     nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
                 })
            late.Add(await SendReceiverAsync(host.Provider, (await BuildAsync(host, rejected, receiver))!));
        var proofs = await host.Content.WriteJsonAsync(host.Tenant.ToString(), Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.ProofSchema, Array.Empty<NativeFactReference>());
        var delayedAuthorize = await SendReceiverAsync(host.Provider, new AuthorizeEngagementPersonalDecision(
            Uuid.CreateVersion4(), rejected.Source, engagementStream, d.EvaluationId, 1, null, rejected.Intent,
            engagement, proofs));

        // Act: step 4, D2 with current inputs acquires every use and authorizes.
        var d2 = await EvaluationAsync(host, engagement, partner);
        var admitted2 = await host.HttpAsync(d2, partner.Actor);
        var completed = await RunAsync(host.Provider, host.Tenant, engagement, d2.EvaluationId);

        // Act: step 5, an older authorized decision materializes after a newer personal result.
        var d3 = await EvaluationAsync(host, engagement, partner);
        var d4 = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(d3, partner.Actor)).IsSuccess);
        Assert.True((await host.HttpAsync(d4, partner.Actor)).IsSuccess);
        var newer = await RunAsync(host.Provider, host.Tenant, engagement, d4.EvaluationId);
        var older = await RunAsync(host.Provider, host.Tenant, engagement, d3.EvaluationId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(EngagementPersonalDecisionKind.PartnerEvaluation, intent.Kind);
        Assert.Equal(1UL, intent.ExpectedEngagementRevision);
        Assert.Equal(1UL, intent.ServiceEpoch);
        Assert.Equal(5, uses.Count);
        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.Equal(ProcessState.Aborted, rejected.State);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var first = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(d.EvaluationId)!;
        Assert.NotNull(first.Canceled);
        Assert.Null(first.Authorized);
        Assert.Null(first.Materialized);
        Assert.Empty(await host.EventsAsync<PartnerIndependenceEvaluationRecorded>(
            new PartnerIndependenceDecision(host.Tenant, d.EvaluationId).Stream));
        foreach (var entry in await UseEntriesAsync(host, partner, engagement, d.EvaluationId))
            Assert.True(entry.Entry is { IsSettled: true }, $"{entry.Area} was not settled");
        Assert.Contains(await UseEntriesAsync(host, partner, engagement, d.EvaluationId),
            entry => entry.Entry is { IsFence: true });
        Assert.All(late, outcome => Assert.IsType<ReceiverOutcome.Rejected>(outcome.Value));
        Assert.IsType<ReceiverOutcome.Rejected>(delayedAuthorize.Value);

        Assert.True(admitted2.IsSuccess, admitted2.Error?.Message);
        Assert.Equal(ProcessState.Completed, completed.State);
        owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var second = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(d2.EvaluationId)!;
        Assert.NotNull(second.Authorized);
        Assert.NotNull(second.Materialized);
        var child = (await host.HydrateAsync(new PartnerIndependenceDecision(host.Tenant, d2.EvaluationId))).Recorded!;
        var view = await host.Content.ReadJsonAsync<PartnerIndependenceEvaluationView>(host.Tenant.ToString(),
            child.EvaluationContent);
        Assert.Equal(second.Authorized!.DecisionAt, view.RecordedAt);
        Assert.Equal(ActorReference.ForFirmStaff(partner.Staff.UserId, view.Actor.Display), view.Actor);

        Assert.Equal(ProcessState.Completed, newer.State);
        Assert.Equal(ProcessState.Completed, older.State);
        owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.NotNull((await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(d3.EvaluationId)!.Materialized);
        Assert.NotNull(await HydrateRecordedAsync(host, d3.EvaluationId));
        Assert.Equal(d4.EvaluationId, owner.CurrentDecision(EngagementPersonalDecisionKind.PartnerEvaluation)!
            .Admitted.Operation.LogicalId);
    }

    [Fact]
    public async Task ShouldMaterializeManagementAcknowledgementOnlyAfterAuthorizationGivenCompleteServiceHistory()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, service) = await SeedEngagementAsync(host);
        var command = await AcknowledgementAsync(host, engagement);
        var engagementStream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var admitted = await host.HttpAsync(command);
        var beforeProcess = await host.EventsAsync<DomainEvent>(engagementStream);
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.AcknowledgementId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(command.AcknowledgementId, admitted.Value!.OperationId);
        Assert.Equal(nameof(EngagementPersonalDecisionAdmitted), beforeProcess[^1].GetType().Name);
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal([nameof(ServiceEngagementMutationRecorded), nameof(EngagementPersonalDecisionAdmitted),
            nameof(EngagementPersonalDecisionAuthorized), nameof(EngagementPersonalDecisionMaterialized)],
            (await host.EventsAsync<DomainEvent>(engagementStream)).Select(item => item.GetType().Name));
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var decision = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(command.AcknowledgementId)!;
        var acknowledged = (await host.HydrateAsync(new EngagementManagementAcknowledgement(host.Tenant,
            command.AcknowledgementId))).Acknowledged!;
        var view = await host.Content.ReadJsonAsync<EngagementManagementAcknowledgementView>(host.Tenant.ToString(),
            acknowledged.AcknowledgementContent);
        Assert.Equal(service, Assert.Single(view.CompleteServiceRecordIds));
        Assert.Equal(host.User, view.UserId);
        Assert.Equal(host.MemberReference.Id, view.Actor.Id);
        Assert.Equal(decision.Authorized!.DecisionAt, view.RecordedAt);
        Assert.Equal(1, view.EngagementRevision);
        Assert.Equal(command.AcknowledgementId, owner.CurrentDecision(EngagementPersonalDecisionKind.ManagementAcknowledgement)!
            .Admitted.Operation.LogicalId);
        Assert.False(owner.HasPendingPersonalDecision);
        var service0 = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant).ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(command.AcknowledgementId, engagementStream, candidate.Stream)));
        Assert.False(service0.Uses.HasActiveUse);
        Assert.True(service0.Uses.Find(EngagementPersonalDecisionEffects.UseKey(command.AcknowledgementId,
            engagementStream, service0.Stream)) is { IsSettled: true, GrantFact: not null });
        var listed = await host.SendAsync(new GetEngagementManagementAcknowledgements(host.Tenant, engagement));
        Assert.Equal(command.AcknowledgementId, Assert.Single(listed.Value!).AcknowledgementId);
    }

    [Fact]
    public async Task ShouldMaterializePartnerEvaluationAfterEveryUseIsHeldGivenCurrentSources()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner, rationale: "Services are conditionally compatible.");
        var engagementStream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var admitted = await host.HttpAsync(command, partner.Actor);
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);

        // Assert
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Assert.Equal(ProcessState.Completed, record.State);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var decision = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(command.EvaluationId)!;
        var proofs = await host.Content.ReadJsonAsync<NativeFactReference[]>(host.Tenant.ToString(),
            decision.Authorized!.AuthorizationProofs);
        var entries = await UseEntriesAsync(host, partner, engagement, command.EvaluationId);
        Assert.Equal(5, proofs.Length);
        Assert.All(entries, entry =>
        {
            Assert.True(entry.Entry is { IsSettled: true, GrantFact: not null, SettleFact: not null }, entry.Area);
            Assert.Contains(proofs, proof => proof.EventId == ((DomainEvent)entry.Entry!.GrantFact!).Metadata.EventId);
        });
        var child = (await HydrateRecordedAsync(host, command.EvaluationId))!;
        var view = await host.Content.ReadJsonAsync<PartnerIndependenceEvaluationView>(host.Tenant.ToString(),
            child.EvaluationContent);
        Assert.Equal(engagement, view.EngagementId);
        Assert.Equal(1, view.DraftRevision);
        Assert.Equal(command.ExpectedRuleContentDigest, view.RuleContentDigest);
        Assert.Equal(command.ExpectedServiceHistoryDigest, view.ServiceHistoryDigest);
        Assert.Equal(partner.Staff.StaffMemberId, view.PartnerStaffMemberId);
        Assert.Equal(partner.Duty.DesignationId, view.DutyDesignationId);
        Assert.Equal("partner_evaluation_required", view.DecisionCode);
        Assert.Equal("conditionally_compatible", view.Outcome);
        Assert.Equal(decision.Authorized.DecisionAt, view.RecordedAt);
        var rules = await host.Content.ReadJsonAsync<IndependenceRuleVersionView>(host.Tenant.ToString(),
            child.RatifiedRulesContent);
        Assert.True(rules.IsRatified);
        Assert.False(owner.HasPendingPersonalDecision);
        var released = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(await host.RegisterStaffAsync("attest"), "Amended after the decision")));
        Assert.True(released.IsSuccess, released.Error?.Message);
    }

    [Fact]
    public async Task ShouldNotAuthorizeGivenAnyRequiredUseMissingFromTheProofs()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await StartAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var none = await host.Content.WriteJsonAsync(host.Tenant.ToString(), Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.ProofSchema, Array.Empty<NativeFactReference>());

        // Act: nothing was acquired, then only the service history was.
        var withoutProofs = await SendReceiverAsync(host.Provider, new AuthorizeEngagementPersonalDecision(
            Uuid.CreateVersion4(), record.Source, stream, command.EvaluationId, 1, null, record.Intent, engagement, none));
        var service = await BuildAsync(host, record, nameof(AuthorizeClientServicePersonalDecisionUse));
        var granted = await SendReceiverAsync(host.Provider, service!);
        var partial = await host.Content.WriteJsonAsync(host.Tenant.ToString(), Uuid.CreateVersion4(),
            EngagementPersonalDecisionIntent.ProofSchema, new[] { ((ReceiverOutcome.Applied)granted.Value!).Fact });
        var withPartialProofs = await SendReceiverAsync(host.Provider, new AuthorizeEngagementPersonalDecision(
            Uuid.CreateVersion4(), record.Source, stream, command.EvaluationId, 1, null, record.Intent, engagement,
            partial));

        // Assert
        Assert.False(withoutProofs.IsSuccess);
        Assert.False(withPartialProofs.IsSuccess);
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        Assert.Null((await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(command.EvaluationId)!.Authorized);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAuthorized>(stream));
    }

    [Fact]
    public async Task ShouldRefuseCancellationGivenDecisionAlreadyAuthorized()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await AcknowledgementAsync(host, engagement);
        Assert.True((await host.HttpAsync(command)).IsSuccess);
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.AcknowledgementId);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;

        // Act
        var canceled = await SendReceiverAsync(host.Provider, new CancelEngagementPersonalDecision(
            Uuid.CreateVersion4(), record.Source, stream, command.AcknowledgementId, 1, null, record.Intent,
            engagement));

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.False(canceled.IsSuccess);
        Assert.False(canceled.Error!.IsTransient);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionCanceled>(stream));
    }

    [Fact]
    public async Task ShouldCancelBeforeAuthorizationAndSettleEveryUseGivenAbortRequest()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);

        // Act
        var aborting = await AbortAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);
        var aborted = await RunAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);

        // Assert
        Assert.Equal(ProcessState.Aborting, aborting.State);
        Assert.Equal(ProcessState.Aborted, aborted.State);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        Assert.Single(await host.EventsAsync<EngagementPersonalDecisionCanceled>(stream));
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAuthorized>(stream));
        Assert.All(await UseEntriesAsync(host, partner, engagement, command.EvaluationId),
            entry => Assert.True(entry.Entry is { IsFence: true }, entry.Area));
        Assert.False((await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement)))
            .HasPendingPersonalDecision);
    }

    [Fact]
    public async Task ShouldReplayOriginalAcceptanceGivenExactRetryAfterEngagementAndServiceChange()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await AcknowledgementAsync(host, engagement);
        var first = await host.HttpAsync(command);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;
        Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
            IndependenceHost.Service()))).IsSuccess);

        // Act
        var retried = await host.HttpAsync(command);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(IndependenceHost.Json(first.Value), IndependenceHost.Json(retried.Value));
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
        var admitted = Assert.Single(await host.EventsAsync<EngagementPersonalDecisionAdmitted>(stream));
        Assert.Equal(202, (await FirstResponses.ReplayAsync(host.Content, host.Tenant.ToString(), admitted.FirstResponse!))
            .HttpStatus);
    }

    [Fact]
    public async Task ShouldConflictWithoutAppendGivenSameIdentityWithChangedIntentActorOrRevision()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var acknowledge = await AcknowledgementAsync(host, engagement);
        var evaluate = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(acknowledge)).IsSuccess);
        Assert.True((await host.HttpAsync(evaluate, partner.Actor)).IsSuccess);
        var before = (await host.EventsAsync<DomainEvent>(stream)).Count;

        // Act
        var statement = await host.HttpAsync(acknowledge with { Statement = "A different statement." });
        var otherMember = await host.HttpAsync(acknowledge, IndependenceHost.ActorFor(Uuid.CreateVersion4()));
        var revision = await host.HttpAsync(acknowledge with { ExpectedEngagementRevision = 2 });
        var rationale = await host.HttpAsync(evaluate with { Rationale = "A different rationale." }, partner.Actor);
        var rules = await host.HttpAsync(evaluate with { RuleVersion = 2 }, partner.Actor);

        // Assert
        Assert.All(new[] { statement.Error, revision.Error, rationale.Error, rules.Error },
            error => Assert.Equal(RequestErrorKind.Conflict, error?.Kind));
        Assert.False(otherMember.IsSuccess);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Fact]
    public async Task ShouldRefuseAdmissionGivenStaleRevisionEpochOrIncompleteHistory()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        var acknowledge = await AcknowledgementAsync(host, engagement);
        var evaluate = await EvaluationAsync(host, engagement, partner);

        // Act
        var staleRevision = await host.HttpAsync(acknowledge with { ExpectedEngagementRevision = 0 });
        var staleEpoch = await host.HttpAsync(acknowledge with { ExpectedServiceEpoch = 0 });
        var incomplete = await host.HttpAsync(acknowledge with { CompleteServiceRecordIds = [] });
        var staleDigest = await host.HttpAsync(evaluate with { ExpectedServiceHistoryDigest = new string('0', 64) },
            partner.Actor);
        var staleRules = await host.HttpAsync(evaluate with { ExpectedRuleContentDigest = new string('0', 64) },
            partner.Actor);
        var unknown = await host.HttpAsync(acknowledge with { EngagementId = Uuid.CreateVersion4() });
        var negative = await host.HttpAsync(acknowledge with { ExpectedServiceEpoch = -1 });

        // Assert
        Assert.All(new[] { staleRevision, staleEpoch, incomplete }, result =>
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind));
        Assert.All(new[] { staleDigest.Error, staleRules.Error }, error =>
            Assert.Equal(RequestErrorKind.Conflict, error?.Kind));
        Assert.Equal(RequestErrorKind.NotFound, unknown.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, negative.Error?.Kind);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAdmitted>(stream));
    }

    [Fact]
    public async Task ShouldRefuseEngagementChangeGivenDecisionPinsContentUntilSettlement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await AcknowledgementAsync(host, engagement);
        Assert.True((await host.HttpAsync(command)).IsSuccess);
        var lead = await host.RegisterStaffAsync("attest");

        // Act
        var pinned = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement, 1,
            IndependenceHost.Draft(lead, "Amended while pinned")));
        var closePinned = await host.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), host.Tenant,
            engagement, 1, "Closed while pinned"));
        await RunAsync(host.Provider, host.Tenant, engagement, command.AcknowledgementId);
        var released = await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagement,
            1, IndependenceHost.Draft(lead, "Amended after settlement")));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, pinned.Error?.Kind);
        Assert.True(pinned.Error!.IsTransient);
        Assert.Equal(RequestErrorKind.Conflict, closePinned.Error?.Kind);
        Assert.True(released.IsSuccess, released.Error?.Message);
        Assert.Equal(2, released.Value!.Revision);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("staff")]
    [InlineData("duty")]
    [InlineData("rules")]
    [InlineData("ratification")]
    public async Task ShouldRejectAndFenceEveryUseGivenPinnedSourceChangedBeforeItsGrant(string changed)
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        await ChangeSourceAsync(host, partner, changed);

        // Act
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);

        // Assert
        Assert.Equal(ProcessState.Aborted, record.State);
        var stream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        Assert.Single(await host.EventsAsync<EngagementPersonalDecisionCanceled>(stream));
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAuthorized>(stream));
        Assert.Empty(await host.EventsAsync<PartnerIndependenceEvaluationRecorded>(
            new PartnerIndependenceDecision(host.Tenant, command.EvaluationId).Stream));
        var entries = await UseEntriesAsync(host, partner, engagement, command.EvaluationId);
        Assert.All(entries, entry => Assert.True(entry.Entry is { IsSettled: true }, entry.Area));
        var refusing = entries.Single(entry => entry.Area == AreaOf(changed));
        Assert.True(refusing.Entry is { IsFence: true }, $"{changed} must be fenced, never granted");
    }

    [Fact]
    public async Task ShouldReplayEveryLaterReceiverAndSupersedeAcquireGivenRedeliveryAfterCompletion()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);
        var definition = EngagementPersonalDecisionProcess.Definition();
        var receivers = definition.Stages.SelectMany(stage => stage.Receivers)
            .Where(receiver => receiver != nameof(MaterializeEngagementManagementAcknowledgement)).ToArray();

        // Act
        var outcomes = new List<(string Receiver, Result<ReceiverOutcome> Outcome)>();
        foreach (var receiver in receivers)
            outcomes.Add((receiver, await SendReceiverAsync(host.Provider, (await BuildAsync(host, record, receiver))!)));

        // Assert
        Assert.Equal(ProcessState.Completed, record.State);
        Assert.Equal(14 - 1, receivers.Length);
        foreach (var (receiver, outcome) in outcomes)
        {
            Assert.True(outcome.IsSuccess, $"{receiver}: {outcome.Error?.Message}");
            if (definition.Stages[0].Receivers.Contains(receiver))
            {
                // A grant redelivered after authorization is a proven superseded no-op, never a second grant.
                Assert.IsType<ReceiverOutcome.Superseded>(outcome.Value);
                continue;
            }

            var replayed = Assert.IsType<ReceiverOutcome.Replayed>(outcome.Value);
            Assert.Equal(record.Facts[receiver], replayed.Fact);
        }
    }

    [Fact]
    public async Task ShouldConflictGivenSettleDispositionDifferentFromTerminalSourceEvent()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await AcknowledgementAsync(host, engagement);
        Assert.True((await host.HttpAsync(command)).IsSuccess);
        var record = await RunAsync(host.Provider, host.Tenant, engagement, command.AcknowledgementId);
        var settle = (SettleClientServicePersonalDecisionUse)(await BuildAsync(host, record,
            nameof(SettleClientServicePersonalDecisionUse)))!;

        // Act
        var wrong = await SendReceiverAsync(host.Provider, settle with
        {
            CommandId = Uuid.CreateVersion4(),
            Disposition = SourceUseSettlementDisposition.Canceled,
        });

        // Assert
        Assert.False(wrong.IsSuccess);
        Assert.False(wrong.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldRefuseSourceWritersGivenUsesHeldUntilSettlement()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        Assert.True((await host.HttpAsync(command, partner.Actor)).IsSuccess);
        var record = await StartAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);
        var acquire = new[]
        {
            nameof(AuthorizeClientServicePersonalDecisionUse),
            nameof(AuthorizeFirmStaffRecordEngagementUse),
            nameof(AuthorizeFirmProfessionalDutyDesignationEngagementUse),
            nameof(AuthorizeIndependenceRuleCatalogEngagementUse),
            nameof(AuthorizeIndependenceRuleRatificationCatalogEngagementUse),
        };
        var grants = new Dictionary<string, NativeFactReference>();
        foreach (var receiver in acquire)
            grants[receiver] = Assert.IsType<ReceiverOutcome.Applied>((await SendReceiverAsync(host.Provider,
                (await BuildAsync(host, record, receiver))!)).Value).Fact;
        var retried = new List<(string Receiver, Result<ReceiverOutcome> Outcome)>();
        foreach (var receiver in acquire)
            retried.Add((receiver, await SendReceiverAsync(host.Provider, (await BuildAsync(host, record, receiver))!)));

        // Act
        var staffRecord = await host.HydrateAsync(new FirmStaffRecord(partner.Staff.StaffMemberId));
        var designation = await host.HydrateAsync(new FirmProfessionalDutyDesignation(partner.Staff.UserId,
            FirmProfessionalDuty.EngagementPartner, host.Tenant));
        var catalog = await host.HydrateAsync(new IndependenceRuleCatalog());
        var ratifications = await host.HydrateAsync(new IndependenceRuleRatificationCatalog());
        var clientService = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));
        RequestError?[] held =
        [
            staffRecord.PrepareStatus(staffRecord.Revision).Error,
            designation.PrepareChange((long)designation.Revision, Uuid.CreateVersion4()).Error,
            catalog.PrepareRevision(catalog.Sequence).Error,
            ratifications.PrepareRatification(ratifications.Sequence, 2).Error,
            clientService.PrepareService(Uuid.CreateVersion4(), (long)clientService.Epoch).Error,
        ];
        var finished = await RunAsync(host.Provider, host.Tenant, engagement, command.EvaluationId);
        var releasedStaff = await host.HydrateAsync(new FirmStaffRecord(partner.Staff.StaffMemberId));
        var releasedService = await host.HydrateAsync(new ClientServiceIndependence(host.Tenant));

        // Assert
        Assert.All(retried, item => Assert.Equal(grants[item.Receiver],
            Assert.IsType<ReceiverOutcome.Replayed>(item.Outcome.Value).Fact));
        Assert.All(held, error =>
        {
            Assert.NotNull(error);
            Assert.Equal(RequestErrorKind.Conflict, error.Kind);
            Assert.True(error.IsTransient);
        });
        Assert.Equal(ProcessState.Completed, finished.State);
        Assert.True(releasedStaff.PrepareStatus(releasedStaff.Revision).IsSuccess);
        Assert.True(releasedService.PrepareService(Uuid.CreateVersion4(), (long)releasedService.Epoch).IsSuccess);
    }

    [Fact]
    public async Task ShouldDenyPartnerEvaluationGivenActorWithoutCurrentPartnerDesignationForThisClient()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var partner = await SeedPartnerAsync(host);
        var (engagement, _) = await SeedEngagementAsync(host);
        var command = await EvaluationAsync(host, engagement, partner);
        var other = IndependenceHost.ActorFor(Uuid.CreateVersion4());

        // Act
        var denied = await host.HttpAsync(command, other);
        var direct = await host.SendAsync(command, partner.Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, direct.Error?.Kind);
        Assert.Empty(await host.EventsAsync<EngagementPersonalDecisionAdmitted>(
            new ServiceEngagementIndependence(host.Tenant, engagement).Stream));
    }

    static string AreaOf(string changed) => changed switch
    {
        "service" => ClientServiceIndependence.Area,
        "staff" => FirmStaffRecord.Area,
        "duty" => FirmProfessionalDutyDesignation.Area,
        "rules" => IndependenceRuleCatalog.Area,
        _ => IndependenceRuleRatificationCatalog.Area,
    };

    static async Task ChangeSourceAsync(IndependenceHost host, PartnerSetup partner, string changed)
    {
        switch (changed)
        {
            case "service":
                Assert.True((await host.SendAsync(new RecordNonattestService(host.Tenant, Uuid.CreateVersion4(), 1,
                    IndependenceHost.Service()))).IsSuccess);
                break;
            case "staff":
                await FirmStaffFacts.SetStatusAsync(host.Provider, partner.Staff.StaffMemberId, false);
                break;
            case "duty":
                var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                var revocation = ProfessionalDutyAssertionTestIssuer.Issue(partner.Duty.DesignationId,
                    partner.Staff.UserId, FirmProfessionalDuty.EngagementPartner, host.Tenant, 2, "revoked",
                    partner.Assertion.Evidence.AssertionId, at.AddMinutes(-1), at.AddMinutes(30));
                var revoked = await ProfessionalDutyFacts.RevokeAsync(host.Provider, revocation, partner.OperatorActor, at);
                Assert.True(revoked.IsSuccess, revoked.Error?.Message);
                break;
            case "rules":
                await host.SeedDraftRulesAsync(PersonalDecisionFacts.ConditionalRules);
                break;
            default:
                var ratificationId = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(host.Provider, new IndependenceRuleRatificationRules(), catalog =>
                    catalog.Ratify(Uuid.CreateVersion4(), ratificationId, 2, 1, catalog.Sequence, new string('c', 64),
                        "Synthetic later ratification", partner.Ratifier, partner.RatifierDuty,
                        ActorReference.ForFirmStaff(partner.Ratifier.UserId, "Synthetic ratifier"), partner.At));
                var ratified = await host.Scope.ServiceProvider.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync();
                Assert.Equal(ratificationId, ratified.Active!.RatificationId);
                break;
        }
    }

    static async Task<(EngagementPersonalDecisionIntent Intent, IReadOnlyList<SourceUseRequirement> Uses)> IntentOfAsync(
        IndependenceHost host, Uuid engagement, Uuid decision)
    {
        var owner = await host.HydrateAsync(new ServiceEngagementIndependence(host.Tenant, engagement));
        var admitted = (await ServiceEngagementHistory.ReadAsync(host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>(), owner)).DecisionOf(decision)!.Admitted;
        var intent = await host.Content.ReadJsonAsync<EngagementPersonalDecisionIntent>(host.Tenant.ToString(),
            admitted.Intent);
        return (intent, await host.Content.ReadSelectionAsync<SourceUseRequirement>(host.Tenant.ToString(),
            intent.RequiredSourceUses));
    }

    static async Task<PartnerIndependenceEvaluationRecorded?> HydrateRecordedAsync(IndependenceHost host, Uuid evaluation) =>
        (await host.HydrateAsync(new PartnerIndependenceDecision(host.Tenant, evaluation))).Recorded;

    static async Task<IRequest<ReceiverOutcome>?> BuildAsync(IndependenceHost host, ProcessRecord record, string receiver)
    {
        var definition = EngagementPersonalDecisionProcess.Definition();
        var stage = definition.Stages.Single(item => item.Receivers.Contains(receiver));
        var step = new ProcessStep(record, stage.Stage, receiver, null, EffectIdentity.CommandId(record.Source.EventId,
            $"{record.Key.ProcessKind}:{receiver}", "-", record.Episode));
        return await definition.CreateReceiver(step, host.Scope.ServiceProvider, CancellationToken.None);
    }

    static async Task<IReadOnlyList<(string Area, SourceUseEntry? Entry)>> UseEntriesAsync(IndependenceHost host,
        PartnerSetup partner, Uuid engagement, Uuid decision)
    {
        var engagementStream = new ServiceEngagementIndependence(host.Tenant, engagement).Stream;
        ISourceUseOwner[] owners =
        [
            await host.HydrateAsync(new ClientServiceIndependence(host.Tenant).ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, candidate.Stream))),
            await host.HydrateAsync(new FirmStaffRecord(partner.Staff.StaffMemberId).ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, candidate.Stream))),
            await host.HydrateAsync(new FirmProfessionalDutyDesignation(partner.Staff.UserId,
                FirmProfessionalDuty.EngagementPartner, host.Tenant).ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, candidate.Stream))),
            await host.HydrateAsync(new IndependenceRuleCatalog().ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, candidate.Stream))),
            await host.HydrateAsync(new IndependenceRuleRatificationCatalog().ForSourceUse(candidate => EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, candidate.Stream))),
        ];
        return owners.Select(owner => (owner.Stream.Area, owner.Uses.Find(
            EngagementPersonalDecisionEffects.UseKey(decision, engagementStream, owner.Stream)))).ToArray();
    }
}
