using Cntryl.Portia;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Handlers;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class RetiredFindingWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutExecutionGivenRetiredGenericRevision(bool system)
    {
        // Arrange
        var request = new ReviseFinding(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, "low", Uuid.CreateVersion4(), new DateOnly(2026, 12, 1), "Scope", null, "Reason");
        var context = new RequestContext<ReviseFinding>(request, system ? RequestActor.System : RequestActor.Anonymous);
        var handler = new ReviseFindingHandler();

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains(nameof(ReviseFindingAnalysis), result.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseWithoutExecutionGivenRetiredOneStepClosure(bool system)
    {
        // Arrange
        var request = new CloseFinding(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, "Verified.", [], "Resolved.");
        var context = new RequestContext<CloseFinding>(request, system ? RequestActor.System : RequestActor.Anonymous);
        var handler = new CloseFindingHandler();

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains(nameof(ProposeFindingClosure), result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ReviewFindingClosure), result.Error.Message, StringComparison.Ordinal);
    }
}
