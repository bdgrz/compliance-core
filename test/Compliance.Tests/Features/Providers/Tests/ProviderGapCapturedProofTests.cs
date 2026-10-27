using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderGapCapturedProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectCapturedWrongRiskOrPhysicalTupleGivenCoherentNativeGapAdmission(bool anotherRisk)
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var registration = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync();
        var other = anotherRisk ? await host.AcceptRiskAsync() : risk;
        var gap = await host.GapAsync(registration.GapId);
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var source = await services.GetRequiredService<RiskEvaluationReader>().ReadAsync(host.Tenant,
            ProviderAssuranceHost.ProgramId, other.RiskId);
        var (proof, _, _) = await SourceFactProofs.BuildAsync<RiskAccepted>(services.GetRequiredService<ISourceFactResolver>(),
            host.Content, host.Tenant.ToString(), NativeFactReference.Of(source.Stream, source.AcceptanceFact(other.AcceptanceId)!));
        if (!anotherRisk)
            proof = proof with { GlobalPosition = proof.GlobalPosition + 1 };
        var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
            ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, gap.Revision, risk.RiskId, risk.AcceptanceId);
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(RbacIds.Member(host.Tenant, host.User), "Gap operator");
        var pair = ProviderAssuranceGap.PairEpisode(request.RiskId, request.AcceptanceId);
        var operation = gap.Provenance(request.CommandId, nameof(LinkProviderCoverageGapRiskAcceptance), actor,
            pair, (ulong)gap.Revision, AuthoredIntentDigest.Compute(request), now);
        var header = new DurableIntentHeader(request.CommandId, gap.Stream, gap.Stream, gap.Stream, actor, pair,
            (ulong)gap.Revision, operation.AuthoredIntentSha256, now);
        var recorded = await host.Content.ReadJsonAsync<ProviderCoverageGapContent>(host.Tenant.ToString(), gap.Recorded!.ContentContent);
        var reference = await ProviderAssuranceContent.WriteAsync(host.Content, host.Tenant, ProviderRiskLinkIntent.Schema,
            new ProviderRiskLinkIntent(header, request.CommandId, host.ProviderId, gap.GapId, (ulong)gap.Revision + 1,
                request.ProgramId, request.RiskId, request.AcceptanceId, proof, risk.ExpiresAt,
                (ulong)gap.Recorded.ProviderRevision, recorded.ServiceId, now), CancellationToken.None);
        var acknowledgment = await ProviderAssuranceContent.AcceptedAsync(host.Content, host.Tenant, request.CommandId,
            now, CancellationToken.None);
        var frozen = await FirstResponses.FreezeAsync(host.Content, host.Tenant.ToString(), acknowledgment);
        var admitted = await services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(new ProviderAssuranceGap(
                host.Tenant, host.ProviderId, gap.GapId, request.CommandId, selectedPair: pair), owner =>
                AggregateOutcome.Commit(owner.AdmitLink(operation, gap.Revision, request.RiskId, request.AcceptanceId, reference, frozen)),
            new RequestDispatchContext(RequestActor.System));
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var root = await host.RootAsync(gap.Stream, request.CommandId);
        var frame = (ProviderCoverageGapRiskAcceptanceAdmitted)root.Event;
        var command = new MaterializeProviderRiskAcceptanceLink(Uuid.CreateVersion4(), NativeFactReference.Of(gap.Stream, frame),
            ProviderAssuranceStreams.RiskAcceptance(host.Tenant, request.CommandId), request.CommandId, frame.Generation,
            null, reference, gap.GapId, request.CommandId);

        // Act
        var result = await host.ReceiveAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
        Assert.Empty(await host.FactsAsync(command.Target));
        Assert.Equal(2, (await host.FactsAsync(gap.Stream)).Count);
    }
}
