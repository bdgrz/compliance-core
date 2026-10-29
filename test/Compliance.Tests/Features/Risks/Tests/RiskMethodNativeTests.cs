using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
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
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Handlers;
using Bdgrz.Compliance.Features.Risks.Queries;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskMethodNativeTests
{
    [Fact]
    public async Task ShouldReadEveryImmutablePublicationGivenManyLaterNativeVersions()
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        RiskMethodVersionView? original = null;
        for (var version = 0; version < 128; version++)
        {
            var published = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(version, 12)), CancellationToken.None);
            Assert.True(published.IsSuccess, published.Error?.Message);
            original ??= published.Value;
            fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        }
        var reader = new RiskMethodReader(fixture.Reader, fixture.Events, fixture.Content);

        // Act
        var owner = await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program));
        var history = await reader.ReadAsync(fixture.Tenant, fixture.Program);
        var replay = await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(0, 12)), CancellationToken.None);

        // Assert
        Assert.Equal(128, owner.PublishedVersion);
        Assert.Equal(128, history.Versions.Count);
        Assert.Equal(original!.MethodVersionId, history.GetVersion(1)!.MethodVersionId);
        Assert.Equal(original.MethodVersionId, replay.Value!.MethodVersionId);
        Assert.True(DomainEventPayload.Size(owner.Latest!) < 16 * 1024);
        Assert.Equal(history.Facts[^1].Content, owner.Latest!.Content);
        Assert.Equal(history.Versions[^1].MethodVersionId, (await reader.CurrentAsync(fixture.Tenant, fixture.Program))!.MethodVersionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldClassifyCurrentReadFailureGivenMissingOrCorruptImmutableMethod(bool unavailable)
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(0, 12)), CancellationToken.None)).IsSuccess);
        var reader = new RiskMethodReader(fixture.Reader, fixture.Events, new FailedContent(fixture.Content, unavailable));
        var handler = new GetRiskMethodHandler(reader);

        // Act
        var result = await handler.HandleAsync(new RequestContext<GetRiskMethod>(new GetRiskMethod(fixture.Tenant, fixture.Program),
            new System.Security.Claims.ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRejectForgedImmutablePublicationGivenDifferentOriginalIntentOrScope(bool wrongIntent)
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        Assert.True((await fixture.Handler.HandleAsync(fixture.Context(fixture.Request(0, 12)), CancellationToken.None)).IsSuccess);
        var owner = await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program));
        var fact = owner.Latest!;
        var body = await NativeContent.ReadAsync<RiskMethodPublicationContent>(fixture.Content, fixture.Tenant, fact.Content);
        var forged = wrongIntent ? body with { Request = body.Request with { AppetiteThreshold = 13 } }
            : body with { Version = body.Version with { ProgramId = Uuid.CreateVersion4() } };
        var reference = await NativeContent.WriteAsync(fixture.Content, fixture.Tenant, RiskMethodContent.Schema, forged);

        // Act
        var failure = await Assert.ThrowsAsync<ImmutableContentIntegrityException>(() =>
            RiskMethodContent.ReadAsync(fixture.Content, fact with { Content = reference }));

        // Assert
        Assert.NotEmpty(failure.Message);
        Assert.Equal(1, (await fixture.Reader.HydrateAsync(new RiskMethod(fixture.Tenant, fixture.Program))).PublishedVersion);
    }

    [Fact]
    public async Task ShouldReadPublishedMethodGivenOriginalWhitespaceAndLaterClock()
    {
        // Arrange
        await using var fixture = await RiskMethodPublicationTests.Fixture.CreateAsync();
        var request = fixture.Request(0, 12) with { LikelihoodScale = [" Rare ", " Unlikely ", " Possible ", " Likely ", " Certain "] };
        var published = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        Assert.True(published.IsSuccess, published.Error?.Message);
        fixture.Clock.Now = fixture.Clock.Now.AddYears(1);

        // Act
        var read = await new RiskMethodReader(fixture.Reader, fixture.Events, fixture.Content).ReadAsync(fixture.Tenant, fixture.Program);
        var replay = await fixture.Handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal("Rare", read.Current!.LikelihoodScale[0]);
        Assert.Equal(published.Value!.PublishedAt, replay.Value!.PublishedAt);
        Assert.Equal(AuthoredIntentDigest.Compute(request), read.Facts[0].Operation.AuthoredIntentSha256);
        Assert.Equal(read.Facts[0].Operation.Owner, read.Facts[0].Operation.FirstOwner);
    }

    sealed class FailedContent(IImmutableContentStore inner, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) => inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference, CancellationToken ct = default) =>
            throw (unavailable ? new ImmutableContentUnavailableException("The method is unavailable.")
                : new ImmutableContentIntegrityException("The method is corrupt."));
    }
}
