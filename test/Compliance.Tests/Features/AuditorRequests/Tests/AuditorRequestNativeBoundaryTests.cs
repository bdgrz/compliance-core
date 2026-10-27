using System.Collections;
using System.Reflection;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;

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
