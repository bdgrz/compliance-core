using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.SourceFacts.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Features.Examinations.Events;
using Bdgrz.Compliance.Features.Examinations.Readers;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class ExaminationEngagementNativeBoundaryTests
{
    [Fact]
    public async Task ShouldFreezeCompleteSupportedSourceSetGivenImmutableExternalBaselineContent()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var examination = Uuid.CreateVersion4();
        var boundary = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var at = DateTimeOffset.UtcNow;
        var events = new InMemoryEventStore();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(events);
        services.AddPortia();
        ExaminationSeeds.Configure(services, events);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var owner = new ExaminationEngagementRules(tenant, program, examination);
        Assert.Null(owner.Create(new ExaminationEngagementContent("type_i", "Independent Audit LLP",
            new DateOnly(2026, 9, 30), null, ["security"], boundary, []), actor, "Engagement lead", at));
        var sources = new List<TypeIBaselineSourceReference>
        {
            new("approved_boundary", boundary, Uuid.CreateVersion4(), 1, new string('a', 64))
        };
        sources.AddRange(Enumerable.Range(1, 255).Select(_ => new TypeIBaselineSourceReference("control",
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, new string('b', 64))));
        var preview = TypeIBaselinePreviewTestFactory.Create(tenant, program, examination, sources);

        // Act
        var failure = owner.FreezeBaseline(1, Uuid.CreateVersion4(), preview, "Confirmed every supported source",
            actor, "Engagement lead", at.AddMinutes(1));

        Assert.Null(failure);
        await ExaminationSeeds.WriteAsync(reader, writer, events, content, owner);
        var snapshot = await new ExaminationEngagementReader(reader, events, content).ReadAsync(tenant, program, examination);
        var frozen = Assert.IsType<TypeIBaselineFrozen>(snapshot.History.Facts[^1]);
        var frame = JsonSerializer.SerializeToUtf8Bytes(frozen, ComplianceCoreJsonContext.Default.TypeIBaselineFrozen);

        // Assert
        Assert.True(frozen.Change.Bytes > 48 * 1024);
        Assert.True(frame.Length < 48 * 1024);
        Assert.Equal("bdgrz.examinations.type-i-baseline-frozen@2", EventKind.Of(frozen));
        Assert.Equal(2, snapshot.Owner.Revision);
        Assert.Equal(256, snapshot.Baseline!.Sources.Count);
        Assert.Equal(preview.ContentSha256, snapshot.Baseline.ContentSha256);
    }
}
