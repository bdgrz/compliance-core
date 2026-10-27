using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Applications.Processes;
using Bdgrz.Compliance.Features.Applications.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationImportTargetPreparationTests
{
    [Theory]
    [InlineData("reserve", false)]
    [InlineData("reserve", true)]
    [InlineData("apply", false)]
    [InlineData("apply", true)]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    [InlineData("fence", false)]
    [InlineData("fence", true)]
    [InlineData("complete-cancel", false)]
    [InlineData("complete-cancel", true)]
    public async Task ShouldReplayNativeReceiptOrRequestedCancellationGivenWinnerBeforeFrozenPlanReadFailure(
        string receiver, bool cancel)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await SeedAsync(host, receiver);
        var before = (await host.EventsAsync<DomainEvent>(ApplicationImportPreparationSafetyTests.TargetOf(command))).Count;
        var healthy = ApplicationImportReceiverPreparationTests.Receivers(host, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new ApplicationImportReceiverPreparationTests.FaultStore(host.Content, async () =>
        {
            winner = await HandleAsync(healthy, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await HandleAsync(
            ApplicationImportReceiverPreparationTests.Receivers(host, fault), command, cancellation.Token));

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
        Assert.Equal(before + 1, (await host.EventsAsync<DomainEvent>(ApplicationImportPreparationSafetyTests.TargetOf(command))).Count);
    }

    [Theory]
    [InlineData("reserve")]
    [InlineData("apply")]
    [InlineData("commit")]
    [InlineData("fence")]
    [InlineData("complete-cancel")]
    public async Task ShouldReplayAcceptedNativeReceiptWithoutFrozenPlanGivenRequiredExtraTermsRemainReadable(string receiver)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await SeedAsync(host, receiver);
        var first = await HandleAsync(ApplicationImportReceiverPreparationTests.Receivers(host, host.Content), command,
            CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var fault = new ApplicationImportReceiverPreparationTests.FaultStore(host.Content, null);

        // Act
        var replay = await HandleAsync(ApplicationImportReceiverPreparationTests.Receivers(host, fault), command,
            CancellationToken.None);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
        Assert.Equal(0, fault.Reads);
    }

    internal static ValueTask<Result<ReceiverOutcome>> HandleAsync(ApplicationImportReceivers receivers,
        IRequest<ReceiverOutcome> command, CancellationToken ct) => command switch
        {
            ReserveApplicationImportTarget value => receivers.ReserveAsync(ApplicationImportReceiverPreparationTests.Context(value), value, ct),
            ApplyApplicationImportEffect value => receivers.ApplyAsync(ApplicationImportReceiverPreparationTests.Context(value), value, ct),
            CommitApplicationImportBatch value => receivers.CommitAsync(ApplicationImportReceiverPreparationTests.Context(value), value, ct),
            FenceApplicationImportTarget value => receivers.FenceAsync(ApplicationImportReceiverPreparationTests.Context(value), value, ct),
            CompleteApplicationImportCancellation value => receivers.CompleteAsync(ApplicationImportReceiverPreparationTests.Context(value), value, ct),
            _ => throw new ArgumentException("The target receiver is not part of this fixture.", nameof(command)),
        };

    internal static async Task<IRequest<ReceiverOutcome>> SeedAsync(ApplicationImportHost host, string receiver)
    {
        var (batch, process, applicationId) = await ApplicationImportReceiverPreparationTests.SealedAsync(host);
        var claimed = await host.SendAsync((await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource),
            process.Targets[0]))!);
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        var target = ApplicationImportEffects.TargetKey(applicationId);
        if (receiver is "fence" or "complete-cancel")
        {
            var canceled = await host.CancelAsync(batch.Id, (await host.BatchAsync(batch.Id)).Revision);
            Assert.True(canceled.IsSuccess, canceled.Error?.Message);
            var pending = await host.BatchAsync(batch.Id);
            var accepted = pending.CancellationAccepted!;
            var source = NativeFactReference.Of(pending.Stream, accepted);
            var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(host.Content, host.TenantId,
                batch.Plan!.FrozenPlan);
            var groups = await ApplicationImportContent.ReadSelectionAsync<ImportTargetGroupIntent>(host.Content,
                host.TenantId, plan.TargetGroups);
            var group = Assert.Single(groups);
            var groupRef = await ApplicationImportPlanWriter.GroupRefAsync(host.Content, host.TenantId,
                batch.Plan.FrozenPlan, group, CancellationToken.None);
            var fence = new FenceApplicationImportTarget(ApplicationImportEffects.ReceiverCommandId(source.EventId,
                nameof(FenceApplicationImportTarget), target, accepted.Operation.Episode), source,
                host.NewApplication(applicationId).Stream, accepted.Operation.RootOperationId, ApplicationImportEffects.Generation,
                null, groupRef, batch.Id, applicationId);
            if (receiver == "fence")
                return fence;
            var fenced = await host.SendAsync(fence);
            Assert.True(fenced.IsSuccess, fenced.Error?.Message);
            var fact = Assert.IsType<ReceiverOutcome.Applied>(fenced.Value).Fact;
            var manifest = await host.Content.WriteJsonAsync<IReadOnlyList<ApplicationImportFenceProof>>(host.TenantId.ToString(),
                Uuid.CreateVersion5(accepted.Intent.ContentId, "import-fence-manifest"), "import-fence-manifest.v1",
                [new ApplicationImportFenceProof(applicationId, fact.EventId, fact.AggregateVersion)]);
            return new CompleteApplicationImportCancellation(ApplicationImportEffects.ReceiverCommandId(source.EventId,
                nameof(CompleteApplicationImportCancellation), "-", accepted.Operation.Episode), source, pending.Stream,
                accepted.Operation.RootOperationId, ApplicationImportEffects.Generation, null, accepted.Intent, batch.Id, manifest);
        }
        var reserve = (await host.StepAsync(process, "reserve", nameof(ReserveApplicationImportTarget), target))!;
        if (receiver == "reserve")
            return reserve;
        var reserved = await host.SendAsync(reserve);
        Assert.True(reserved.IsSuccess, reserved.Error?.Message);
        var apply = (await host.StepAsync(process, "apply", nameof(ApplyApplicationImportEffect), target))!;
        if (receiver == "apply")
            return apply;
        var applied = await host.SendAsync(apply);
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        return (await host.StepAsync(process, "publish", nameof(CommitApplicationImportBatch)))!;
    }
}
