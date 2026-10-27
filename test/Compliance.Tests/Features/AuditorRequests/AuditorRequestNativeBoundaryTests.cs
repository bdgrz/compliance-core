using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.AuditorRequests;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditorRequests;

public sealed class AuditorRequestNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedRequestAuthorityGivenManyAdmittedComments()
    {
        // Arrange
        await using var fixture = new AuditorRequestFixture();
        var id = await fixture.SeedRequestAsync();
        for (var index = 0; index < 128; index++)
        {
            var owner = await fixture.RequestAsync(id);
            var command = new RecordAuditorRequestComment(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
                id, $"Attributable comment {index + 1}.", "internal", owner.Revision);
            var accepted = await fixture.Handler<RecordAuditorRequestCommentHandler>().HandleAsync(fixture.Context(command),
                CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        }

        // Act
        var saved = await fixture.RequestAsync(id);
        var retained = typeof(AuditorRequest).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(saved) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(129, saved.Revision);
        Assert.True(saved.IsOpen);
    }
}
