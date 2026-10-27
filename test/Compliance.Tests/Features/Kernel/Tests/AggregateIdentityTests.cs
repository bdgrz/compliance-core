using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.Providers.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Kernel.Tests;

public sealed class AggregateIdentityTests
{
    static readonly Uuid One = Uuid.Parse("00000000-0000-0000-0000-000000000001", CultureInfo.InvariantCulture);
    static readonly Uuid Two = Uuid.Parse("00000000-0000-0000-0000-000000000002", CultureInfo.InvariantCulture);

    public static TheoryData<string, object[], string, string> Vectors => new()
    {
        {
            "ProviderNameClaim", [One, "EXAMPLE"],
            "0000001150726f76696465724e616d65436c61696d00000000000000000000000000000001000000074558414d504c45",
            "99758556-85f4-5674-870a-c23c7df99bfd"
        },
        {
            "PracticeIndependenceCommitment", [One, "canonical_user", Two],
            "0000001e5072616374696365496e646570656e64656e6365436f6d6d69746d656e74000000000000000000000000000000010000000e63616e6f6e6963616c5f7573657200000000000000000000000000000002",
            "cb195905-614d-5ad7-af2e-91aa8a512a46"
        },
        {
            "TenantInvitationMailboxClaim", [One, "josé@example.test"],
            "0000001c54656e616e74496e7669746174696f6e4d61696c626f78436c61696d00000000000000000000000000000001000000126a6f73c3a9406578616d706c652e74657374",
            "1954d2b3-b016-51a0-b1c2-bc5fe18485a3"
        },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ShouldCreateVectorGivenOwnerTypeAndParts(string owner, object[] parts, string encodedHex, string uuid)
    {
        // Arrange
        var expected = Uuid.Parse(uuid, CultureInfo.InvariantCulture);

        // Act
        var encoded = Convert.ToHexStringLower(AggregateIdentity.Encode(owner, parts));
        var actual = AggregateIdentity.Create(owner, parts);

        // Assert
        Assert.Equal(encodedHex, encoded);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldRejectPartGivenUnsupportedType()
    {
        // Arrange
        object[] parts = [42];

        // Act
        var error = Assert.Throws<ArgumentException>(() => AggregateIdentity.Create("Owner", parts));

        // Assert
        Assert.Contains("Unsupported", error.Message, StringComparison.Ordinal);
    }
}
