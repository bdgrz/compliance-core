using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Commands;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderGapAcceptedRetryTests
{
    [Fact]
    public async Task ShouldReplayOriginalAcceptanceAndRequireCurrentAuthorityGivenUnavailablePreparationBodies()
    {
        // Arrange
        var content = new PreparationFailure(new FitzKvImmutableContentStore(new InMemoryKvClient()));
        await using var host = await ProviderAssuranceHost.CreateAsync(content);
        var gap = await host.RecordGapAsync();
        var risk = await host.AcceptRiskAsync();
        var request = new LinkProviderCoverageGapRiskAcceptance(Uuid.CreateVersion4(), host.Tenant,
            ProviderAssuranceHost.ProgramId, host.ProviderId, gap.GapId, 1, risk.RiskId, risk.AcceptanceId);
        var first = await host.SendAsync(request);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId));
        content.Block = true;

        // Act
        var retry = await host.SendAsync(request);
        host.Permissions.Allowed = false;
        var denied = await host.SendAsync(request);
        var after = await host.FactsAsync(ProviderAssuranceStreams.Gap(host.Tenant, host.ProviderId, gap.GapId));

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Forbidden, denied.Error?.Kind);
        Assert.Equal(0, content.BlockedReads);
        Assert.Equal(before.Count, after.Count);
        Assert.Single(after.OfType<ProviderCoverageGapRiskAcceptanceAdmitted>());
    }

    sealed class PreparationFailure(IImmutableContentStore inner) : IImmutableContentStore
    {
        public bool Block { get; set; }
        public int BlockedReads { get; private set; }

        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);

        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default)
        {
            if (Block && reference.Schema is ProviderAssuranceContent.GapRecordedSchema or ProviderRiskLinkIntent.Schema)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("The original gap preparation bodies are unavailable.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }
}
