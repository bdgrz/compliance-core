using System.Security.Claims;
using Bdgrz.Compliance.Features.AuditPopulations;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Operations;

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
