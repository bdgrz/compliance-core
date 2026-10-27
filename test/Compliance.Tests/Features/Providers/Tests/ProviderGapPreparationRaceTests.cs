using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderGapPreparationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReplayOriginalAcceptanceOrRequestedCancellationGivenWinnerBeforeLinkIntentWriteFailure(bool cancel)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var source = await host.AcceptRiskAsync();
        var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
            ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, 1, source.RiskId, source.AcceptanceId);
        var context = new Cntryl.Portia.RequestContext<LinkProviderCoverageGapRiskAcceptance>(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(host.User),
                new HttpInvocation("PUT", "/synthetic/provider-gap", "/synthetic/provider-gap", "synthetic")));
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var healthy = Handler(services, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new FaultStore(host.Content, async () =>
        {
            winner = await healthy.HandleAsync(context, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var losing = Handler(services, fault);
        var actual = Result<OperationAcceptedResponse>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => actual = await losing.HandleAsync(context, cancellation.Token));
        var facts = await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId));

        // Assert
        Assert.True(winner.IsSuccess);
        var admitted = Assert.Single(facts.OfType<ProviderCoverageGapRiskAcceptanceAdmitted>());
        Assert.Equal(request.CommandId, admitted.Operation.LogicalId);
        Assert.Equal(2, admitted.Revision);
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(actual.IsSuccess, actual.Error?.Message);
            Assert.Equal(winner.Value, actual.Value);
        }
        Assert.Equal(2, facts.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldClassifyRequiredRecordedBodyGivenMissingOrCorruptGapContent(bool link, bool unavailable)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = new RecordedBodyStore(host.Content, unavailable);
        RequestError? error;
        bool success;

        // Act
        if (link)
        {
            var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
                ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, 1, risk.RiskId, risk.AcceptanceId);
            var result = await Handler(services, store).HandleAsync(new Cntryl.Portia.RequestContext<LinkProviderCoverageGapRiskAcceptance>(
                request, new RequestDispatchContext(ProgramManagementServices.Actor(host.User))), CancellationToken.None);
            (success, error) = (result.IsSuccess, result.Error);
        }
        else
        {
            var request = new CloseProviderCoverageGap(Uuid.CreateVersion4(), host.Tenant, host.ProviderId, gap.GapId, 1,
                new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report", Uuid.CreateVersion4(), 2,
                    "Verified closure evidence."));
            var handler = ActivatorUtilities.CreateInstance<CloseProviderCoverageGapHandler>(services,
                new NativeOperationExecutor(services.GetRequiredService<IAggregateReader>(),
                    services.GetRequiredService<IAggregateExecutor>(), store), store);
            var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<CloseProviderCoverageGap>(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(host.User))), CancellationToken.None);
            (success, error) = (result.IsSuccess, result.Error);
        }

        // Assert
        Assert.False(success);
        Assert.Equal(RequestErrorKind.Conflict, error?.Kind);
        Assert.Equal(unavailable, error!.IsTransient);
        Assert.Equal(1, store.Reads);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldClassifyCapturedProofFailureGivenMissingOrCorruptRiskSource(bool unavailable)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var gap = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = new FailingResolver(services.GetRequiredService<ISourceFactResolver>(), unavailable);
        var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
            ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, 1, risk.RiskId, risk.AcceptanceId);
        var handler = Handler(services, host.Content, resolver);

        // Act
        var result = await handler.HandleAsync(new Cntryl.Portia.RequestContext<LinkProviderCoverageGapRiskAcceptance>(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(host.User))), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(1, resolver.Reads);
        Assert.Single(await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId)));
    }

    sealed class RecordedBodyStore(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != ProviderAssuranceContent.GapRecordedSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Reads++;
            throw unavailable
                ? new ImmutableContentUnavailableException("The actual recorded gap body is unavailable.")
                : new ImmutableContentIntegrityException("The actual recorded gap body is corrupt.");
        }
    }

    sealed class FailingResolver(ISourceFactResolver inner, bool unavailable) : ISourceFactResolver
    {
        public int Reads { get; private set; }
        public Task<ResolvedSourceFact> ResolveAsync(EventStreamAddress stream, ulong resourcePosition,
            CancellationToken ct = default)
        {
            Reads++;
            throw unavailable ? new SourceFactUnavailableException("The captured source is unavailable.")
                : new SourceFactVerificationException("The captured source is corrupt.");
        }
        public Task<VerifiedSourceProof<TEvent>> VerifyAsync<TEvent>(SourceFactProof<TEvent> proof,
            CancellationToken ct = default) where TEvent : DomainEvent => inner.VerifyAsync(proof, ct);
    }

    static LinkProviderCoverageGapRiskAcceptanceHandler Handler(IServiceProvider services, IImmutableContentStore content,
        ISourceFactResolver? resolver = null) =>
        new(new NativeOperationExecutor(services.GetRequiredService<IAggregateReader>(),
                services.GetRequiredService<IAggregateExecutor>(), content), services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<AssuranceReferences>(), services.GetRequiredService<ProviderReadConsistency>(),
            resolver ?? services.GetRequiredService<ISourceFactResolver>(), content, services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<RiskEvaluationReader>());

    sealed class FaultStore(IImmutableContentStore inner, Func<Task> commitWinner) : IImmutableContentStore
    {
        bool _failed;
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!_failed && schema == ProviderRiskLinkIntent.Schema)
            {
                _failed = true;
                await commitWinner();
                throw new ImmutableContentUnavailableException("The losing actual risk-link intent write failed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
