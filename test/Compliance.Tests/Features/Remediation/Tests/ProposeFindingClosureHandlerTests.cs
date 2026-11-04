using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class ProposeFindingClosureHandlerTests
{
    const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldProposeExactEvidenceWithNoActionsGivenLeadOrFindingOwner(bool lead)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var request = Command(fixture, finding, artifact);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(Context(request, lead ? fixture.LeadUserId : fixture.OwnerUserId), CancellationToken.None);
        var owner = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("closing", result.Value.Status);
        Assert.Equal(request.ClosureId, result.Value.ClosureProposal!.ClosureId);
        Assert.Equal(1, result.Value.ClosureProposal.ProposalRevision);
        Assert.Equal(request.Terms.ResolutionEvidence, result.Value.ClosureProposal.Terms.ResolutionEvidence);
        Assert.Equal(request.Terms.VerificationRationale.Trim(), result.Value.ClosureProposal.Terms.VerificationRationale);
        Assert.Null(result.Value.Closure);
        Assert.Equal(finding.Source, result.Value.Source);
        var actions = await ImmutableSelectionManifests.ReadAsync<AdmittedChild>(fixture.Content, fixture.TenantId.ToString(),
            owner.ClosureProposal!.ActionsReliedOn, CancellationToken.None);
        Assert.Empty(actions);
        Assert.Equal(0, owner.UnresolvedActionCount);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenNeitherLeadNorFindingOwner()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var before = await fixture.FindingStreamAsync(finding.FindingId);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(Context(Command(fixture, finding, artifact), fixture.BackupUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.ClosureProposal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("version")]
    [InlineData("quarantined")]
    public async Task ShouldRefuseWithoutFactGivenEvidenceVersionCannotBeVerified(string problem)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = problem == "missing" ? Uuid.CreateVersion4() : await ArtifactAsync(fixture,
            problem == "quarantined" ? EvidenceInspectionOutcome.Malware : EvidenceInspectionOutcome.Clean);
        var request = Command(fixture, finding, artifact);
        if (problem == "version")
            request = request with { Terms = request.Terms with
            { ResolutionEvidence = [new FindingClosureArtifactVersion(artifact, new string('b', 64))] } };
        var before = await fixture.FindingStreamAsync(finding.FindingId);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(Context(request, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.ClosureProposal);
    }

    [Fact]
    public async Task ShouldRefuseWithoutFactGivenPlannedActionOutcomeNotRecorded()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await CorrectiveActionChangeWorkflowTests.OpenAsync(fixture);
        var artifact = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var before = await fixture.FindingStreamAsync(finding.FindingId);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);

        // Act
        var result = await handler.HandleAsync(Context(Command(fixture, finding, artifact), fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
        Assert.Null(after.ClosureProposal);
    }

    [Fact]
    public async Task ShouldReplayFrozenProposalResponseGivenExactCommandRetry()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var request = Command(fixture, finding, artifact);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);
        var first = await handler.HandleAsync(Context(request, fixture.OwnerUserId), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        var retry = await handler.HandleAsync(Context(request, fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(WireJson.Of(first.Value), WireJson.Of(retry.Value));
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task ShouldRefuseChangedIntentWithoutFactGivenProposalCommandRetried()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var provider = fixture.Provider;
        var finding = await fixture.RaiseFindingAsync(fixture.OwnerMemberId);
        var artifact = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);
        var request = Command(fixture, finding, artifact);
        await using var scope = provider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<ProposeFindingClosureHandler>(scope.ServiceProvider);
        Assert.True((await handler.HandleAsync(Context(request, fixture.OwnerUserId), CancellationToken.None)).IsSuccess);
        var before = await fixture.FindingStreamAsync(finding.FindingId);

        // Act
        var retry = await handler.HandleAsync(Context(request with { Terms = request.Terms with { Rationale = "Changed intent." } },
            fixture.OwnerUserId), CancellationToken.None);
        var after = await fixture.FindingStreamAsync(finding.FindingId);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, retry.Error?.Kind);
        Assert.Equal(before.Version, after.Version);
    }

    internal static ProposeFindingClosure Command(OperationsFixture fixture, FindingView finding, Uuid artifact) =>
        new(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, finding.FindingId, finding.Revision,
            Uuid.CreateVersion4(), 0, new FindingClosureTerms([new FindingClosureArtifactVersion(artifact, Hash)],
                " Verified access removal. ", " Resolution evidence is complete. "));

    internal static async Task<Uuid> ArtifactAsync(OperationsFixture fixture, EvidenceInspectionOutcome outcome)
    {
        var id = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync<EvidenceArtifact, EvidenceArtifactRegistration>(fixture.Provider,
            new EvidenceArtifact(fixture.TenantId, id), artifact => artifact.Register(
                new EvidenceArtifactContent("Resolution evidence", null, "document", "Access review", at,
                    fixture.Today, fixture.Today, "internal"), Hash, 4, ActorReference.ForMember(fixture.OwnerMemberId, "Owner"), at));
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, id),
            artifact => artifact.RecordInspection(outcome, at) is null ? Result.Success :
                Result.Failure(new RequestError(RequestErrorKind.Conflict, "Inspection failed.")));
        return id;
    }

    internal static Cntryl.Portia.RequestContext<T> Context<T>(T request, Uuid user) where T : IRequestBase, IIdentifiedRequest =>
        new(request, new RequestDispatchContext(ProgramManagementServices.Actor(user),
            new HttpInvocation("POST", "/findings/closure", "/findings/closure", "finding-closure"),
            new RequestMetadata(request.RequestId, Uuid.CreateVersion4(), null)));
}
