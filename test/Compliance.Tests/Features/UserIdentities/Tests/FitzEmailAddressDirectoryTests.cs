using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.UserIdentities.Readers;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;

public sealed class FitzEmailAddressDirectoryTests
{
    [Fact]
    public async Task ShouldListOwnAddressesGivenVerificationUpdates()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzEmailAddressDirectory(client);
        var owner = Uuid.CreateVersion4();
        var other = Uuid.CreateVersion4();
        var identity = new CheckpointIdentity("EmailAddressDirectory", EventStreamPattern.ForPattern("bdgrz"));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(EmailAddressFacts.ReservedFact(owner, "first@example.com"));
            await directory.ApplyAsync(EmailAddressFacts.ReservedFact(owner, "second@example.com"));
            await directory.ApplyAsync(EmailAddressFacts.ReservedFact(other, "other@example.com"));
            await directory.ApplyAsync(EmailAddressFacts.VerifiedFact(owner, "first@example.com", Uuid.CreateVersion4()));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        var first = await directory.GetAsync("first@example.com");

        // Act
        var page = await directory.ListAsync(owner, null, null);

        // Assert
        Assert.True(first?.Verified);
        Assert.Equal(2, page.Items.Count);
        Assert.DoesNotContain(page.Items, address => address.UserId == other);
        Assert.Contains(page.Items, address => address.EmailAddress == "second@example.com" && !address.Verified);
    }
}
