using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorSelectionIdentityTests
{
    [Fact]
    public async Task ShouldAdmitDistinctFrozenSourcesGivenSameSelectionIdOnDifferentRequests()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var firstRequest = await fixture.SeedRequestAsync();
        var secondRecord = fixture.NewRecordCommand();
        Assert.True((await fixture.Handler<RecordAuditorRequestHandler>().HandleAsync(fixture.Context(secondRecord),
            CancellationToken.None)).IsSuccess);
        var firstPopulation = await fixture.SeedFrozenPopulationAsync();
        var secondPopulation = await fixture.SeedFrozenPopulationAsync();
        var selectionId = Uuid.CreateVersion4();
        var first = SampleSelectionCommandHandlerTests.RecordCommand(fixture, firstRequest,
            firstPopulation.PopulationId, firstPopulation.IncludedRowId) with
        { SelectionId = selectionId };
        var second = SampleSelectionCommandHandlerTests.RecordCommand(fixture, secondRecord.RequestId,
            secondPopulation.PopulationId, secondPopulation.IncludedRowId) with
        { SelectionId = selectionId };
        var handler = fixture.Handler<RecordSampleSelectionHandler>();
        var admitted = await handler.HandleAsync(fixture.Context(first), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        Result<OperationAcceptedResponse> secondAdmission = default;

        // Act
        var error = await Record.ExceptionAsync(async () => secondAdmission = await handler.HandleAsync(
            fixture.Context(second), CancellationToken.None));
        var firstOwner = await fixture.RequestAsync(firstRequest);
        var secondOwner = await fixture.RequestAsync(secondRecord.RequestId);

        // Assert
        Assert.Null(error);
        Assert.True(secondAdmission.IsSuccess, secondAdmission.Error?.Message);
        Assert.Equal(2, firstOwner.Revision);
        Assert.Equal(2, secondOwner.Revision);
        Assert.NotEqual(AuditorSampleSelection.IdentityOf(fixture.TenantId, fixture.ProgramId, firstRequest, selectionId),
            AuditorSampleSelection.IdentityOf(fixture.TenantId, fixture.ProgramId, secondRecord.RequestId, selectionId));
    }
}
