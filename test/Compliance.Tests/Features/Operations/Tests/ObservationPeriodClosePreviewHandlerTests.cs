using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Contracts;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AuditPopulations.Queries;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Handlers;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Operations.Tests;

public sealed class ObservationPeriodClosePreviewHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReportSourceReadinessWithoutAdmittingCloseGivenExactCurrentPeriod(bool intervalEnded)
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var id = await fixture.StartPeriodAsync();
        var at = intervalEnded ? TypeIIRangeFixture.StartedAt.AddYears(1) : TypeIIRangeFixture.StartedAt;
        var sources = new Sources();
        var request = new PreviewObservationPeriodClose(fixture.TenantId, fixture.ProgramId, id);
        var handler = new PreviewObservationPeriodCloseHandler(fixture.Host.Reader, fixture.Host.Periods, sources, new FixedClock(at));

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);
        var owner = await fixture.Host.Reader.HydrateAsync(new TypeIIPeriod(fixture.TenantId, fixture.ProgramId, id));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(intervalEnded, result.Value!.Ready);
        Assert.Equal(4, result.Value.ExpectedOccurrenceCount);
        Assert.Equal(4, result.Value.ActualOccurrenceCount);
        Assert.Equal(at, result.Value.CalculatedAt);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(2, owner.NativeSequence);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldReturnTypedFailureWithoutOwnerAppendGivenPreviewSourceCannotBeVerified(bool unavailable)
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var id = await fixture.StartPeriodAsync();
        var sources = new Sources { Failure = unavailable ? new ImmutableContentUnavailableException("Missing source.") : new ImmutableContentIntegrityException("Corrupt source.") };
        var request = new PreviewObservationPeriodClose(fixture.TenantId, fixture.ProgramId, id);
        var handler = new PreviewObservationPeriodCloseHandler(fixture.Host.Reader, fixture.Host.Periods, sources,
            new FixedClock(TypeIIRangeFixture.StartedAt.AddYears(1)));

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.Equal(1, (await fixture.PeriodAsync(id)).Revision);
    }

    [Fact]
    public async Task ShouldReturnPendingConflictGivenPeriodChangesDuringPreview()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var id = await fixture.StartPeriodAsync();
        var source = new Sources
        {
            BeforeRead = async () =>
            {
                var generated = await fixture.Host.GenerateHandler(fixture.Clock).HandleAsync(fixture.Context(
                    new GenerateDueControlOccurrences(fixture.TenantId, fixture.ProgramId, id, fixture.FirstStart, "preview-race")), CancellationToken.None);
                Assert.True(generated.IsSuccess, generated.Error?.Message);
            },
        };
        var request = new PreviewObservationPeriodClose(fixture.TenantId, fixture.ProgramId, id);
        var handler = new PreviewObservationPeriodCloseHandler(fixture.Host.Reader, fixture.Host.Periods, source,
            new FixedClock(TypeIIRangeFixture.StartedAt.AddYears(1)));

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.True(result.Error!.IsTransient);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldHidePeriodWithoutReadingSourcesGivenDifferentProgram()
    {
        // Arrange
        await using var fixture = new TypeIIRangeFixture();
        var id = await fixture.StartPeriodAsync();
        var sources = new Sources();
        var request = new PreviewObservationPeriodClose(fixture.TenantId, Uuid.CreateVersion4(), id);
        var handler = new PreviewObservationPeriodCloseHandler(fixture.Host.Reader, fixture.Host.Periods, sources,
            new FixedClock(TypeIIRangeFixture.StartedAt.AddYears(1)));

        // Act
        var result = await handler.HandleAsync(fixture.Context(request), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(0, sources.Calls);
    }

    sealed class FixedClock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }
    sealed class Sources : ITypeIIPeriodCloseReconciliation
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; init; }
        public Func<Task>? BeforeRead { get; init; }
        public async ValueTask<Result<TypeIIPeriodCloseValidationContent>> ReadAsync(TypeIIPeriodReadSnapshot period, ClaimsPrincipal actor,
            Uuid requesterUserId, OperationProvenance operation, CancellationToken ct)
        {
            Calls++;
            if (BeforeRead is { } read)
                await read();
            if (Failure is { } error)
                throw error;
            var expected = period.View!.ExpectedOccurrences.Select(item => item.OccurrenceId).ToArray();
            return Result<TypeIIPeriodCloseValidationContent>.Success(new TypeIIPeriodCloseValidationContent(period.View.OpeningSourceReferences,
                expected, expected, [], [], [])
            { RecordedThrough = operation.AcceptedAt });
        }
    }
}
