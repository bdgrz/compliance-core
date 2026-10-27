using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class PersonalOccurrenceProofTransportTests
{
    [Theory]
    [InlineData("attest", "direct")]
    [InlineData("attest", "mcp")]
    [InlineData("correct", "direct")]
    [InlineData("correct", "mcp")]
    [InlineData("review", "direct")]
    [InlineData("review", "mcp")]
    [InlineData("attest", "http")]
    [InlineData("correct", "http")]
    [InlineData("review", "http")]
    public async Task ShouldEnforcePersonalTransportGivenValidOccurrenceProof(string operation, string transport)
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync();
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        if (operation != "attest")
            occurrence = await source.AsAsync(source.OwnerUserId, source.Attest(occurrence));
        if (operation == "correct")
            occurrence = await source.AsAsync(source.ReviewerUserId, source.Review(occurrence, "returned"));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var actor = operation == "review" ? source.ReviewerUserId : source.OwnerUserId;
        var context = PersonalReadinessClosureTransportTests.Context(actor, transport);

        // Act
        var result = operation switch
        {
            "attest" => await bus.DispatchAsync(source.Attest(occurrence), context, CancellationToken.None),
            "review" => await bus.DispatchAsync(source.Review(occurrence, "approved"), context, CancellationToken.None),
            _ => await bus.DispatchAsync(new CorrectControlAttestation(Uuid.CreateVersion4(), source.TenantId,
                source.ProgramId, source.ControlId, occurrence.OccurrenceId, occurrence.Revision, "complete",
                DateTimeOffset.UtcNow.AddMinutes(-1), null, null, "Added missing support.", null,
                OperationsFixture.FullSupport, "Reviewer requested support."), context, CancellationToken.None)
        };
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(operation == "attest" ? 2 : occurrence.Revision + 1, result.Value.Revision);
            Assert.True(after.CommittedStreamPosition > before.CommittedStreamPosition);
            Assert.Equal(source.Member(actor), operation == "review" ? result.Value.Reviews[^1].ReviewerMemberId :
                result.Value.Attestations[^1].RecorderMemberId);
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        }
    }
    [Fact]
    public async Task ShouldPreserveRecorderSeparationGivenNativeHttpReview()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync(owner: new OperatingHolder("person", source.PersonId));
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        var submitted = await source.AsAsync(source.LeadUserId, source.Attest(occurrence, personId: source.PersonId));
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));

        // Act
        var result = await HttpAsync(provider, source.LeadUserId, source.Review(submitted, "approved"), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));

        // Assert
        Assert.Equal("The member who performed or recorded an attestation cannot review it.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldPreserveOperatingResponsibilityGivenNativeHttpAttestation()
    {
        // Arrange
        var source = await OperationsFixture.CreateAsync();
        await using var sourceProvider = source.Provider;
        await source.PlanAsync();
        var occurrence = (await source.OccurrencesAsync("missed"))[0];
        await using var provider = await PersonalReadinessClosureTransportTests.ComposeAsync(source.Provider,
            source.TenantId, source.ProgramId, source.Permissions);
        var before = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));

        // Act
        var result = await HttpAsync(provider, source.OutsiderUserId, source.Attest(occurrence), RequestErrorKind.Forbidden);
        var after = await ProgramManagementServices.HydrateAsync(provider, new ControlOccurrence(source.TenantId, source.ProgramId, source.ControlId, occurrence.OccurrenceId));

        // Assert
        Assert.Equal("Only the control owner or backup owner may perform and attest to this occurrence.", result.Error!.Message);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    internal static async Task<Result<T>> HttpAsync<T>(IServiceProvider provider, Uuid actor,
        IRequest<T> request, RequestErrorKind? expectedFailure = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            PersonalReadinessClosureTransportTests.Context(actor, "http"), CancellationToken.None);
        if (expectedFailure is { } error)
            Assert.Equal(error, result.Error?.Kind);
        else
            Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

}
