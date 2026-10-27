using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class CampaignLaunchPreparationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayEqualLaunchOrRequestedCancellationGivenWinnerBeforeAudienceWriteFailure(bool cancel)
    {
        // Arrange
        await using var host = new CampaignHost();
        var services = host.Scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var executor = services.GetRequiredService<IAggregateExecutor>();
        var campaignId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var request = new LaunchPolicyCampaign(campaignId, host.Tenant, host.Program, CampaignHost.PolicySubject.RecordId,
            1, snapshotId, host.Today.AddDays(30), "Review exact policy");
        var context = new Cntryl.Portia.RequestContext<LaunchPolicyCampaign>(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(host.Manager), new HttpInvocation("POST",
                "/api/v1/programs/test/policy-campaigns", "/api/v1/programs/test/policy-campaigns", "campaign-race")));
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [host.OfflinePerson] = new(host.OfflinePerson, "Included person", "employee", null, new DateOnly(2025, 1, 1)),
        }, new HashSet<Uuid> { host.OfflinePerson });
        var terms = new CampaignLaunchTerms(CampaignHost.PolicySubject, "core_security", [], snapshotId,
            new string('a', 64), audience);
        var healthy = new CampaignLaunch(new NativeOperationExecutor(reader, executor, host.Content),
            host.Content, host.Clock);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<CampaignRegistration>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var trace = new Trace();
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await LaunchAsync(healthy, context, terms, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var losing = new CampaignLaunch(new NativeOperationExecutor(reader, executor, fault, trace),
            fault, host.Clock);
        var actual = Result<CampaignRegistration>.Failure(new RequestError(RequestErrorKind.Conflict, "No losing result yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () =>
            actual = await LaunchAsync(losing, context, terms, cancellation.Token));
        var launched = Assert.Single(await host.EventsAsync<PolicyCampaignLaunched>(host.CampaignStream(campaignId)));

        // Assert
        Assert.True(winner.IsSuccess);
        if (cancel)
        {
            var canceled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(cancellation.Token, canceled.CancellationToken);
            Assert.Null(trace.Observed);
        }
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            Assert.Equal(winner.Value, actual.Value);
            var replay = Assert.IsType<AcceptedAppendResult.Replayed>(trace.Observed);
            Assert.Equal(launched.FirstResponse, replay.FirstResponse);
            Assert.Equal(NativeFactReference.Of(host.CampaignStream(campaignId), launched), replay.FirstFact);
            Assert.Equal(200, replay.Response.HttpStatus);
            Assert.Equal(PolicyCampaignContent.RegistrationSchema, replay.Response.ResultSchema);
        }
        Assert.Equal(0, trace.Classifications);
        Assert.Single(await host.EventsAsync<PolicyCampaignAudienceFrozen>(host.CampaignStream(campaignId)));
        Assert.Equal(1, (await host.CampaignAsync(campaignId)).Revision);
    }

    static ValueTask<Result<CampaignRegistration>> LaunchAsync(CampaignLaunch launch,
        Cntryl.Portia.RequestContext<LaunchPolicyCampaign> context, CampaignLaunchTerms terms, CancellationToken ct) =>
        launch.LaunchAsync(context, context.Request.TenantId, context.Request.ProgramId, context.Request.CampaignId,
            nameof(LaunchPolicyCampaign), context.Request, context.Request.DueOn, context.Request.Instructions,
            _ => ValueTask.FromResult(Result<CampaignLaunchTerms>.Success(terms)), ct);

    sealed class FaultStore(IImmutableContentStore inner, Func<Task> commitWinner) : IImmutableContentStore
    {
        bool _failed;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!_failed && schema == PolicyCampaignContent.TeamsSchema)
            {
                _failed = true;
                await commitWinner();
                throw new ImmutableContentUnavailableException("The losing actual campaign audience write failed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class Trace : INativeOperationTransportObserver
    {
        public AcceptedAppendResult? Observed { get; private set; }
        public int Classifications { get; private set; }
        public RequestError? ClassifyFailure(AcceptedOperationBinding binding, IExecutionContext context,
            ImmutableContentIntegrityException exception)
        {
            Classifications++;
            return new RequestError(RequestErrorKind.Conflict, exception.Message,
                exception is ImmutableContentUnavailableException);
        }
        public void Observe(AcceptedOperationBinding binding, IExecutionContext context, AcceptedAppendResult accepted) =>
            Observed = accepted;
    }
}
