using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ServiceEngagementListAuthorityTests
{
    [Fact]
    public async Task ShouldReadOwningEngagementAuthorityGivenDiscoveryScanHasDifferentRevision()
    {
        // Arrange
        await using var host = new IndependenceHost();
        var lead = await host.RegisterStaffAsync();
        var engagementId = Uuid.CreateVersion4();
        var draft = IndependenceHost.Draft(lead);
        Assert.True((await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagementId, 0, draft))).IsSuccess);
        Assert.True((await host.SendAsync(new AmendServiceEngagement(Uuid.CreateVersion4(), host.Tenant, engagementId, 1,
            draft with { Scope = "Current authoritative scope" }))).IsSuccess);
        var reader = host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var events = host.Scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
        var views = new ServiceEngagementViews(reader, new DiscoveryScan(events), host.Content);

        // Act
        var listed = await views.ListAsync(host.Tenant);
        var current = await views.GetAsync(host.Tenant, engagementId);

        // Assert
        var item = Assert.Single(listed);
        Assert.NotNull(current);
        Assert.Equal(2, current.Revision);
        Assert.Equal(current.Revision, item.Revision);
        Assert.Equal("Current authoritative scope", item.Content.Scope);
        Assert.Equal(current.Status, item.Status);
    }

    sealed class DiscoveryScan(IDomainEventReader inner) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress stream, ulong offset, CancellationToken ct = default) =>
            inner.ReadAsync(stream, offset, ct);
        public async IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var record in inner.ReadAsync(pattern, cursor, ct))
            {
                if (record.Event is ServiceEngagementMutationRecorded { Revision: 2 } original)
                {
                    var changed = original with { Revision = 999 };
                    changed.AttachMetadata(original.Metadata);
                    yield return record with { Event = changed };
                }
                else
                    yield return record;
            }
        }
    }
}
