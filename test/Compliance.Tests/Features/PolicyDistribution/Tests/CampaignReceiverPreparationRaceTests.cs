using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Handlers;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class CampaignReceiverPreparationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayReceiverReceiptOrRequestedCancellationGivenEqualWinnerBeforeIntentReadFailure(bool cancel)
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaignId = await host.LaunchAsync();
        var root = Uuid.CreateVersion4();
        var admitted = await host.SendAsync(host.Acknowledge(campaignId, host.LearnerPerson, 1, root), host.Learner);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var campaign = await host.CampaignAsync(campaignId);
        var fact = campaign.AdmittedFor(root)!;
        var command = new ApplyPolicyParticipantResult(Uuid.CreateVersion5(root, "apply"),
            NativeFactReference.Of(campaign.Stream, fact), PolicyCampaignParticipant.StreamOf(host.Tenant, campaignId,
                host.LearnerPerson), root, fact.Generation, null, fact.Intent, campaignId, host.LearnerPerson,
            CampaignParticipantResultKind.PolicyAcknowledgement);
        var context = new Cntryl.Portia.RequestContext<ApplyPolicyParticipantResult>(command,
            new RequestDispatchContext(RequestActor.System));
        var services = host.Scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var healthy = new ApplyPolicyParticipantResultHandler(reader, executor, host.Content, host.Clock, host.Events);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var losing = new ApplyPolicyParticipantResultHandler(reader, executor, fault, host.Clock, host.Events);
        var actual = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No losing result yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await losing.HandleAsync(context, cancellation.Token));
        var written = Assert.Single(await host.EventsAsync<PolicyAcknowledgementRecorded>(command.Target));

        // Assert
        Assert.True(winner.IsSuccess);
        if (cancel)
        {
            var canceled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
        }
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value);
            var replay = Assert.IsType<ReceiverOutcome.Replayed>(actual.Value);
            Assert.Equal(original.Fact, replay.Fact);
            Assert.Equal(NativeFactReference.Of(command.Target, written), replay.Fact);
        }
        Assert.Equal(root, written.Operation.RootOperationId);
        Assert.Equal(command.CommandId, written.Operation.LogicalId);
        Assert.Equal(1, (await host.ParticipantAsync(campaignId, host.LearnerPerson)).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveMissingWinnerClassificationAndProgrammerFaultGivenReceiverPreparationFailure(bool programmer)
    {
        // Arrange
        await using var host = new CampaignHost();
        var context = await SeedApplyAsync(host);
        var services = host.Scope.ServiceProvider;
        var fault = new ReadFailure(host.Content, programmer);
        var handler = new ApplyPolicyParticipantResultHandler(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), fault, host.Clock, host.Events);
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await handler.HandleAsync(context, CancellationToken.None));

        // Assert
        if (programmer)
            Assert.IsType<InvalidOperationException>(failure);
        else
        {
            Assert.Null(failure);
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.False(result.Error!.IsTransient);
        }
        Assert.Empty(await host.EventsAsync<PolicyAcknowledgementRecorded>(context.Request.Target));
    }

    [Fact]
    public async Task ShouldReplayOriginalReceiverWithoutIntentReadGivenAlreadyAcceptedNativeReceipt()
    {
        // Arrange
        await using var host = new CampaignHost();
        var context = await SeedApplyAsync(host);
        var services = host.Scope.ServiceProvider;
        var healthy = new ApplyPolicyParticipantResultHandler(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), host.Content, host.Clock, host.Events);
        var winner = await healthy.HandleAsync(context, CancellationToken.None);
        Assert.True(winner.IsSuccess, winner.Error?.Message);
        var fault = new ReadFailure(host.Content, programmer: false);
        var retry = new ApplyPolicyParticipantResultHandler(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<IAggregateExecutor>(), fault, host.Clock, host.Events);

        // Act
        var result = await retry.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        Assert.Equal(0, fault.Reads);
        Assert.Single(await host.EventsAsync<PolicyAcknowledgementRecorded>(context.Request.Target));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRecheckActualNativeReceiptGivenThrownExpectedPreparationFailure(bool commitWinner, bool cancel)
    {
        // Arrange
        await using var host = new CampaignHost();
        var context = await SeedApplyAsync(host);
        var command = context.Request;
        var services = host.Scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var healthy = new ApplyPolicyParticipantResultHandler(reader,
            services.GetRequiredService<IAggregateExecutor>(), host.Content, host.Clock, host.Events);
        using var cancellation = new CancellationTokenSource();
        var expected = new ImmutableContentUnavailableException("Expected preparation failure after possible equal winner.");
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var actual = Result<NativeReceiverPreparation.Prepared<object>>.Failure(new RequestError(RequestErrorKind.Conflict,
            "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => actual =
            await NativeReceiverPreparation.PrepareAsync<PolicyCampaignParticipant, object>(reader,
                () => new PolicyCampaignParticipant(host.Tenant, command.CampaignId, command.PersonId,
                    command.CommandId, command.OperationId),
                owner => owner.SelectedResult is { } fact
                    ? SourceUseReceiverSupport.Replayed(NativeFactReference.Of(owner.Stream, fact)) : null,
                async (_, _) =>
                {
                    if (commitWinner)
                    {
                        winner = await healthy.HandleAsync(context, CancellationToken.None);
                        Assert.True(winner.IsSuccess, winner.Error?.Message);
                    }
                    if (cancel)
                        cancellation.Cancel();
                    throw expected;
                }, cancellation.Token));

        // Assert
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else if (!commitWinner)
            Assert.Same(expected, failure);
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            Assert.Null(actual.Value!.Value);
            var outcome = actual.Value.Outcome!.Value;
            Assert.True(outcome.IsSuccess, outcome.Error?.Message);
            Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact,
                Assert.IsType<ReceiverOutcome.Replayed>(outcome.Value).Fact);
        }
        Assert.Equal(commitWinner ? 1 : 0,
            (await host.EventsAsync<PolicyAcknowledgementRecorded>(command.Target)).Count);
    }

    static async Task<Cntryl.Portia.RequestContext<ApplyPolicyParticipantResult>> SeedApplyAsync(CampaignHost host)
    {
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var request = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var response = await host.SendAsync(request, host.Learner);
        Assert.True(response.IsSuccess, response.Error?.Message);
        var parent = await host.CampaignAsync(campaign);
        var admitted = parent.AdmittedFor(request.CommandId)!;
        return new Cntryl.Portia.RequestContext<ApplyPolicyParticipantResult>(new ApplyPolicyParticipantResult(
            Uuid.CreateVersion4(), NativeFactReference.Of(parent.Stream, admitted),
            PolicyCampaignParticipant.StreamOf(host.Tenant, campaign, host.LearnerPerson), request.CommandId,
            admitted.Generation, null, admitted.Intent, campaign, host.LearnerPerson,
            CampaignParticipantResultKind.PolicyAcknowledgement), new RequestDispatchContext(RequestActor.System));
    }

    sealed class ReadFailure(IImmutableContentStore inner, bool programmer) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (reference.Schema == ChildAdmissionIntent.Schema)
            {
                Reads++;
                if (programmer)
                    throw new InvalidOperationException("Unexpected receiver implementation fault.");
                throw new ImmutableContentUnavailableException("The actual receiver intent is unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class FaultStore(IImmutableContentStore inner, Func<Task> commitWinner) : IImmutableContentStore
    {
        bool _failed;
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (!_failed && reference.Schema == ChildAdmissionIntent.Schema)
            {
                _failed = true;
                await commitWinner();
                throw new ImmutableContentUnavailableException("The losing actual receiver intent read failed.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
