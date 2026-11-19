using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Handlers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class RetiredPersonCorrelationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRefuseRetiredCorrelationGivenLinkOrClearIntent(bool clear)
    {
        // Arrange
        var request = new CorrelatePersonMembership(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            1, clear ? null : Uuid.CreateVersion4());
        var context = new RequestContext<CorrelatePersonMembership>(request,
            new RequestDispatchContext(ProgramManagementServices.Actor(Uuid.CreateVersion4()),
                new HttpInvocation("POST", "/retired", "/retired", "retired")));

        // Act
        var result = await new CorrelatePersonMembershipHandler().HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("LinkPersonToMember", result.Error!.Message, StringComparison.Ordinal);
    }
}
