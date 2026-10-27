using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Processes;
using Bdgrz.Compliance.Features.AccessReviews.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
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
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Commands;
using Bdgrz.Compliance.Features.AccessReviews.Handlers;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;

public sealed class AccessSourceUsePreparationTests
{
    [Theory]
    [InlineData("bulk-authorize", false)]
    [InlineData("bulk-authorize", true)]
    [InlineData("bulk-settle", false)]
    [InlineData("bulk-settle", true)]
    [InlineData("completion-authorize", false)]
    [InlineData("completion-authorize", true)]
    [InlineData("completion-settle", false)]
    [InlineData("completion-settle", true)]
    public async Task ShouldRecoverNativeUseReceiptOrRequestedCancellationGivenWinnerBeforeParentIntentReadFailure(
        string pair, bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, pair);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, "parent", async () =>
        {
            winner = await HandleAsync(services, content, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(services, fault,
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        Assert.NotEqual(Uuid.Empty, original.EventId);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    [Theory]
    [InlineData("bulk-authorize")]
    [InlineData("bulk-settle")]
    [InlineData("completion-authorize")]
    [InlineData("completion-settle")]
    public async Task ShouldReplayNativeUseReceiptWithoutParentIntentGivenRequiredSelectedUseIntentAvailable(string pair)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, pair);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var first = await HandleAsync(services, content, command, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var fault = new FaultStore(content, "parent", null);

        // Act
        var replay = await HandleAsync(services, fault, command, CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    [Theory]
    [InlineData("bulk-authorize", false)]
    [InlineData("bulk-authorize", true)]
    [InlineData("bulk-settle", false)]
    [InlineData("bulk-settle", true)]
    [InlineData("completion-authorize", false)]
    [InlineData("completion-authorize", true)]
    [InlineData("completion-settle", false)]
    [InlineData("completion-settle", true)]
    public async Task ShouldRecoverUseReceiptOrRequestedCancellationGivenWinnerBeforeDerivedIntentWriteFailure(
        string pair, bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, pair);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, "write", async () =>
        {
            winner = await HandleAsync(services, content, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(services, fault,
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Writes);
    }

    [Theory]
    [InlineData("bulk-authorize")]
    [InlineData("completion-authorize")]
    public async Task ShouldReplayGrantWithoutRewritingDerivedIntentGivenExactAcceptedUse(string pair)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, pair);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var first = await HandleAsync(services, content, command, CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var fault = new FaultStore(content, "write", null);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            var replay = await HandleAsync(services, fault, command, CancellationToken.None);
            Assert.True(replay.IsSuccess, replay.Error?.Message);
            Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
                Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        });

        // Assert
        Assert.Null(failure);
        Assert.Equal(0, fault.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRecoverGrantOrRequestedCancellationGivenWinnerBeforePinnedItemBodyReadFailure(bool cancel)
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var command = await SeedAsync(fixture, "completion-authorize");
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(content, "pin", async () =>
        {
            winner = await HandleAsync(services, content, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(services, fault,
            command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Reads);
    }

    internal static ValueTask<Result<ReceiverOutcome>> HandleAsync(IServiceProvider services, IImmutableContentStore content,
        IRequest<ReceiverOutcome> command, CancellationToken ct)
    {
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var clock = services.GetRequiredService<TimeProvider>();
        var events = services.GetRequiredService<IDomainEventReader>();
        var manifests = new AccessReviewManifests(content);
        var bulk = new AccessReviewBulkVerifier(reader, content, manifests);
        var completion = new AccessReviewCompletionVerifier(reader, content, manifests, events);
        return command switch
        {
            ReserveAccessReviewItemDecision value => new ReserveAccessReviewItemDecisionHandler(reader, executor, content,
                clock, bulk, services.GetRequiredService<AccessReviewBulkFences>()).HandleAsync(Context(value), ct),
            SettleAccessReviewItemDecision value => new SettleAccessReviewItemDecisionHandler(reader, executor, content,
                clock, bulk).HandleAsync(Context(value), ct),
            AuthorizeAccessReviewItemCampaignCompletionUse value => new AuthorizeAccessReviewItemCampaignCompletionUseHandler(
                reader, executor, content, clock, clock, completion, services.GetRequiredService<AccessReviewCompletionFences>(),
                manifests, events).HandleAsync(Context(value), ct),
            SettleAccessReviewItemCampaignCompletionUse value => new SettleAccessReviewItemCampaignCompletionUseHandler(
                reader, executor, content, clock, completion).HandleAsync(Context(value), ct),
            _ => throw new ArgumentException("The request is not a generic Access SourceUse pair.", nameof(command)),
        };
    }

    internal static Cntryl.Portia.RequestContext<T> Context<T>(T command) where T : IRequestBase =>
        new(command, new RequestDispatchContext(RequestActor.System));

    internal static async Task<IRequest<ReceiverOutcome>> SeedAsync(AccessReviewFixture fixture, string pair)
    {
        if (pair.StartsWith("bulk", StringComparison.Ordinal))
        {
            if (pair == "bulk-settle")
            {
                var publication = Assert.IsType<PublishAccessReviewDecisionBatch>(
                    await AccessBulkReceiverPreparationTests.SeedAsync(fixture, "publish"));
                var published = await fixture.ReceiveAsync(publication);
                Assert.True(published.IsSuccess, published.Error?.Message);
                var publishedCampaignId = Uuid.Parse(publication.Source.Stream.Resource, System.Globalization.CultureInfo.InvariantCulture);
                var process = await fixture.StartBulkAsync(publishedCampaignId, publication.OperationId);
                return await fixture.BulkStepAsync(process, "settle", nameof(SettleAccessReviewItemDecision), process.Targets[0]);
            }
            var (campaignId, selected) = await AccessReviewBulkDecisionTests.LaunchedAsync(fixture);
            var (request, _) = await fixture.AdmitBulkAsync(fixture.ReviewerUserId, campaignId, selected);
            var record = await fixture.StartBulkAsync(campaignId, request.DecisionBatchId);
            var materialized = await fixture.ReceiveAsync(await fixture.BulkStepAsync(record, "materialize",
                nameof(MaterializeAccessReviewDecisionBatch)));
            Assert.True(materialized.IsSuccess, materialized.Error?.Message);
            return await fixture.BulkStepAsync(record, "reserve", nameof(ReserveAccessReviewItemDecision), record.Targets[0]);
        }
        var pending = await AccessCompletionReceiverPreparationTests.PendingAsync(fixture);
        if (pair == "completion-authorize")
            return await fixture.CompletionStepAsync(pending, "acquire", nameof(AuthorizeAccessReviewItemCampaignCompletionUse), pending.Targets[0]);
        foreach (var target in pending.Targets)
        {
            var held = await fixture.ReceiveAsync(await fixture.CompletionStepAsync(pending, "acquire",
                nameof(AuthorizeAccessReviewItemCampaignCompletionUse), target));
            Assert.True(held.IsSuccess, held.Error?.Message);
        }
        var completed = await fixture.ReceiveAsync(await fixture.CompletionStepAsync(pending, "complete",
            nameof(CompleteAccessReviewCampaignClosure)));
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        return await fixture.CompletionStepAsync(pending, "settle", nameof(SettleAccessReviewItemCampaignCompletionUse), pending.Targets[0]);
    }

    sealed class FaultStore(IImmutableContentStore inner, string stage, Func<Task>? beforeFailure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (stage == "write" && schema == SourceUseAuthorizationIntent.Schema)
            {
                Writes++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual derived source-use intent cannot be written.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (stage == "parent" && reference.Schema is BulkAccessDecisionIntent.Schema or ClosureIntent.Schema ||
                stage == "pin" && reference.Schema == AccessReviewItemCreationIntent.Schema)
            {
                Reads++;
                if (beforeFailure is not null)
                    await beforeFailure();
                throw new ImmutableContentUnavailableException("The required source-use preparation bytes are unavailable.");
            }
            return await inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
