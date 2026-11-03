using System.Reflection;
using System.Text.RegularExpressions;
using Cntryl.Portia;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bdgrz.Compliance.Tests;

public sealed partial class AggregateCommandCoverageTests
{
    [Fact]
    public void ShouldKeepTrustedReceiversPrivateGivenPublicTransportMappings()
    {
        // Arrange
        var exposed = PublicTransportRequests();
        var receivers = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && IsReceiver(type));

        // Act
        var leaks = receivers.Select(static type => type.Name).Where(exposed.Contains).ToArray();

        // Assert
        Assert.Empty(leaks);
    }

    [Fact]
    public void ShouldUseSignedExpectedPositionsGivenPublicRequestContracts()
    {
        // Arrange
        string[] shippedExceptions = ["AssignRolePermission", "RemoveRolePermission", "AssignTeamRole",
            "RemoveTeamRole", "GrantPlatformOperator", "RevokePlatformOperator"];
        var requests = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && IsRequest(type)).ToArray();
        var exposed = PublicTransportRequests();

        // Act
        var failures = requests.Where(type => !shippedExceptions.Contains(type.Name, StringComparer.Ordinal) &&
                !(IsReceiver(type) && !exposed.Contains(type.Name)))
            .SelectMany(type => type.GetProperties().Where(static property =>
                    (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == typeof(ulong) &&
                    Regex.IsMatch(property.Name, "^Expected\\w*(Revision|Generation|Epoch|Version)$"))
                .Select(property => $"{type.Name}.{property.Name}")).ToArray();

        // Assert
        Assert.All(shippedExceptions, name => Assert.Contains(requests, type => type.Name == name));
        Assert.Empty(failures);
    }

    [Fact]
    public void ShouldReserveBareOperationRoutesGivenPublicTransportMappings()
    {
        // Arrange
        var invocations = ReadSourceRoots().Values.SelectMany(static root => root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>());

        // Act
        var reserved = invocations.Where(static call => TransportMethod(call)?.StartsWith("MapPortia", StringComparison.Ordinal) == true)
            .SelectMany(static call => call.ArgumentList.Arguments)
            .SelectMany(static argument => argument.Expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
            .Select(static literal => literal.Token.ValueText)
            .Where(static route => route.Contains("/operations/{operation_id}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        Assert.Empty(reserved); // The explicit source-scoped contract remains required (#1042, #1055).
    }

    [Fact]
    public void ShouldPreserveUniqueWireIdentitiesGivenDomainEventInventory()
    {
        // Arrange
        using var inventory = ReadInventory();
        var expected = inventory.RootElement.GetProperty("facts").EnumerateArray().ToDictionary(
            static row => row.GetProperty("fact").GetString()!,
            static row => $"{row.GetProperty("discriminator").GetString()}@{row.GetProperty("schemaVersion").GetInt32()}",
            StringComparer.Ordinal);
        var facts = ContractAssemblies().SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsClass && !type.IsAbstract && typeof(DomainEvent).IsAssignableFrom(type));

        // Act
        var actual = facts.ToDictionary(static type => type.Name, static type =>
        {
            var discriminator = type.GetCustomAttribute<DiscriminatorAttribute>()
                ?? throw new InvalidOperationException($"{type.Name} has no wire discriminator.");
            return $"{discriminator.Name}@{discriminator.Version}";
        }, StringComparer.Ordinal);

        // Assert
        Assert.Equal(actual.Count, actual.Values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected.OrderBy(static pair => pair.Key, StringComparer.Ordinal),
            actual.OrderBy(static pair => pair.Key, StringComparer.Ordinal));
    }

    static HashSet<string> PublicTransportRequests() => ReadSourceRoots().Values
        .SelectMany(static root => root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        .Where(static call => TransportMethod(call) is { } method &&
            (method.StartsWith("MapPortia", StringComparison.Ordinal) || method == "AddMcpTool"))
        .Select(static call => call.Expression is MemberAccessExpressionSyntax member ? member.Name : call.Expression)
        .OfType<GenericNameSyntax>().Select(static method => SimpleName(method.TypeArgumentList.Arguments[0]))
        .ToHashSet(StringComparer.Ordinal);

    static string? TransportMethod(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => null,
    };
}
