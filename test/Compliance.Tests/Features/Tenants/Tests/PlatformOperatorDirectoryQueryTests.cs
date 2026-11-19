using Bdgrz.Compliance.Features.Tenants.Handlers;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Queries;
using Bdgrz.Compliance.Features.Tenants.Readers;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants.Tests;

public sealed class PlatformOperatorDirectoryQueryTests
{
    [Fact]
    public async Task ShouldReturnRequestedPageGivenOperatorDirectoryQuery()
    {
        // Arrange
        var entry = new PlatformOperatorDirectoryEntry(Uuid.CreateVersion4(), Uuid.CreateVersion4(), true);
        var directory = new Directory(new Page<PlatformOperatorDirectoryEntry>([entry], "next"));
        var query = new ListPlatformOperators(17, "cursor");

        // Act
        var result = await new ListPlatformOperatorsHandler(directory).HandleAsync(
            new RequestContext<ListPlatformOperators>(query), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(entry, Assert.Single(result.Value.Items));
        Assert.Equal("next", result.Value.NextCursor);
        Assert.Equal(17, directory.Limit);
        Assert.Equal("cursor", directory.Cursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task ShouldRejectLimitBeforeReadingGivenInvalidOperatorPageSize(int limit)
    {
        // Arrange
        var directory = new Directory(new Page<PlatformOperatorDirectoryEntry>([], null));

        // Act
        var result = await new ListPlatformOperatorsHandler(directory).HandleAsync(
            new RequestContext<ListPlatformOperators>(new ListPlatformOperators(limit)), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.Null(directory.Limit);
    }

    sealed class Directory(Page<PlatformOperatorDirectoryEntry> page) : IPlatformOperatorDirectoryReader
    {
        public int? Limit { get; private set; }
        public string? Cursor { get; private set; }

        public ValueTask<Page<PlatformOperatorDirectoryEntry>> ListAsync(int limit, string? cursor,
            CancellationToken ct = default)
        {
            Limit = limit;
            Cursor = cursor;
            return ValueTask.FromResult(page);
        }
    }
}
