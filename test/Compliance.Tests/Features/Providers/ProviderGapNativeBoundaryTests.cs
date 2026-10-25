using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderGapNativeBoundaryTests
{
    [Fact]
    public async Task ShouldRetainOnlyCurrentGapAuthorityGivenManyRealAcceptedRiskAdmissions()
    {
        // Arrange
        await using var host = await ProviderAssuranceHost.CreateAsync();
        var registration = await host.RecordGapAsync();
        var first = Uuid.Empty;

        // Act
        for (var index = 0; index < 105; index++)
        {
            var acceptance = await host.AcceptRiskAsync();
            var link = Uuid.CreateVersion4();
            if (index == 0)
                first = link;
            var admitted = await host.LinkRequestAsync(registration.GapId, index + 1, acceptance.RiskId,
                acceptance.AcceptanceId, link);
            Assert.True(admitted.IsSuccess, admitted.Error?.Message);
            Assert.Equal(link, admitted.Value.OperationId);
        }
        await using var scope = host.Services.CreateAsyncScope();
        var gap = await scope.ServiceProvider.GetRequiredService<ProviderGapReader>()
            .ReadAsync(host.Tenant, host.ProviderId, registration.GapId);
        var retained = gap.Owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.GetValue(gap.Owner) is ICollection { Count: > 2 }).ToArray();

        // Assert
        Assert.Equal(106, gap.Revision);
        Assert.True(gap.IsOpen);
        Assert.NotNull(gap.AdmissionOf(first));
        Assert.Equal(106, (await host.FactsAsync(gap.Stream)).Count);
        Assert.Empty(retained);
    }
}
