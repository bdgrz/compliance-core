using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Independence.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
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
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentActionCompactionTests
{
    [Fact]
    public async Task ShouldKeepActionCommandAuthorityBoundedGivenManyRealRevisions()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var request = fixture.AddAction(worker, [], 0);
        await fixture.Scenario(fixture.AssessorUserId).When(request).ExpectSuccess();
        for (var revision = 1; revision <= 32; revision++)
            await fixture.Scenario(fixture.AssessorUserId).When(new ReviseRiskTreatmentAction(Uuid.CreateVersion4(),
                fixture.TenantId, fixture.ProgramId, fixture.RiskId, request.ActionId, revision,
                $"Treatment revision {revision}", "Hardware keys enforced", "Identity provider export",
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), worker.MemberId, [])).ExpectSuccess();

        // Act
        var owner = await fixture.ActionAsync(request.ActionId);
        var view = Assert.Single((await fixture.GovernanceAsync()).TreatmentActions);

        // Assert
        Assert.Equal(33, owner.Revision);
        Assert.Equal("Treatment revision 32", view.Title);
        Assert.Equal(33, view.Revision);
        Assert.All(typeof(RiskTreatmentAction).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(owner) is ICollection { Count: > 2 },
                $"The action command owner retains lifetime history in {field.Name}."));
    }

    [Fact]
    public async Task ShouldReplayActionCreationGivenRiskSourceUnavailableAfterAcceptance()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var request = fixture.AddAction(worker, [], 0);
        var first = await fixture.Scenario(fixture.AssessorUserId).When(request).ExpectSuccess();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var source = new UnavailableRiskReader(reader);
        var handler = new AddRiskTreatmentActionHandler(new NativeOperationExecutor(reader,
            scope.ServiceProvider.GetRequiredService<IAggregateExecutor>(), content), source, content, TimeProvider.System);
        var before = await fixture.ActionAsync(request.ActionId);

        // Act
        var replay = await handler.HandleAsync(new RequestContext<AddRiskTreatmentAction>(request,
            ProgramManagementServices.Actor(fixture.AssessorUserId)), CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(0, source.SourceReads);
        Assert.Equal(before.CommittedStreamPosition, (await fixture.ActionAsync(request.ActionId)).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRetryReviewAdmissionGivenEarlierUnacceptedCandidateWroteTimedContent()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        var submitted = await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        await fixture.AssignReviewAsync(submitted.Value.SubmissionId);
        var request = fixture.ReviewAction(added.Value.ActionId, 2, "accept");
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var content = new UnavailableAcknowledgmentOnce(services.GetRequiredService<IImmutableContentStore>());
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var handler = new ReviewRiskTreatmentActionCompletionHandler(new NativeOperationExecutor(reader,
            services.GetRequiredService<IAggregateExecutor>(), content), reader,
            services.GetRequiredService<RiskGovernanceReader>(),
            services.GetRequiredService<ISeparationOfDutiesWaiverProofResolver>(), content, clock);
        var context = new PersonalContext<ReviewRiskTreatmentActionCompletion>(request,
            ProgramManagementServices.Actor(fixture.ApproverUserId));
        Result<OperationAcceptedResponse>? first = null;
        try
        {
            first = await handler.HandleAsync(context, CancellationToken.None);
        }
        catch (ImmutableContentUnavailableException)
        {
            // The old API surfaced this expected storage failure; the retry must still remain valid.
        }
        Assert.True(first is null || !first.Value.IsSuccess);
        if (first is { } failure)
            Assert.True(failure.Error!.IsTransient);
        Assert.Null((await fixture.ActionAsync(request.ActionId)).PendingReview);
        clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        var retried = await handler.HandleAsync(context, CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(1));
        var replayed = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal(request.CommandId, retried.Value!.OperationId);
        Assert.Equal(retried.Value, replayed.Value);
        Assert.Equal(3, (await fixture.ActionAsync(request.ActionId)).Revision);
        Assert.Equal(1, content.Failures);
    }

    [Fact]
    public async Task ShouldRejectForgedSubmitterGivenNativeSubmissionAttributedToAnotherMember()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var worker = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [evidence], 0)).ExpectSuccess();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(added.Value.ActionId, 1, [evidence]));
        var original = await fixture.ActionAsync(added.Value.ActionId);
        var target = new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId, fixture.RiskId, added.Value.ActionId);
        var replay = new AggregateScenario<RiskTreatmentAction>(target).Given(original.Added!);
        var forged = original.PendingSubmission! with { SubmitterMemberId = fixture.ApproverUserId };

        // Act
        var failure = Record.Exception(() => replay.Given(forged));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    sealed class UnavailableRiskReader(IAggregateReader inner) : IAggregateReader
    {
        public int SourceReads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            if (aggregate is RiskDraft)
            {
                SourceReads++;
                throw new ImmutableContentUnavailableException("The fresh risk source is not available.");
            }
            return inner.HydrateAsync(aggregate, ct);
        }
    }

    sealed class UnavailableAcknowledgmentOnce(IImmutableContentStore inner) : IImmutableContentStore
    {
        public int Failures { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema == FindingContent.AcknowledgmentSchema && Failures == 0)
            {
                Failures++;
                throw new ImmutableContentUnavailableException("The first candidate cannot publish its acknowledgment.");
            }
            return inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    sealed class PersonalContext<T>(T request, ClaimsPrincipal actor) : IRequestContext<T> where T : IRequestBase
    {
        readonly RequestContext<T> _inner = new(request, actor);
        public T Request => _inner.Request;
        public ClaimsPrincipal Actor => _inner.Actor;
        public Uuid ExecutionId => _inner.ExecutionId;
        public Uuid RequestId => _inner.RequestId;
        public Uuid CorrelationId => _inner.CorrelationId;
        public Uuid? CausationId => _inner.CausationId;
        public Uuid CauseId => _inner.CauseId;
        public DateTimeOffset StartedAt => _inner.StartedAt;
        public RequestInvocation Invocation => new HttpInvocation("PUT", "/synthetic/review", "/synthetic/review", "synthetic");
    }
}
