using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Kernel;

public sealed class KernelContractSerializationTests
{
    static readonly Uuid Id = Uuid.Parse("11111111-2222-3333-4444-555555555555", CultureInfo.InvariantCulture);
    static readonly Uuid Id2 = Uuid.Parse("66666666-7777-8888-9999-000000000000", CultureInfo.InvariantCulture);
    static readonly EventStreamAddress Address = new("tenant-a", "area", "record-b");
    static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = new("member", "m1", "Member One");
    static readonly ImmutableContentRef Content = new(Id, new string('a', 64), 12, "application/json", "x.v1");
    static readonly ProtectedResponseContentRef Protected = new(Id2, new string('b', 64), 40, "x.v1", "k1");

    static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, typeof(T), ComplianceCoreJsonContext.Default);
        return (T)JsonSerializer.Deserialize(json, typeof(T), ComplianceCoreJsonContext.Default)!;
    }

    [Fact]
    public void ShouldRoundTripGivenProofContracts()
    {
        // Arrange
        var manifest = new ImmutableSelectionManifest(Content, "item.v1", 2, SelectionOrder.CanonicalKey,
            new string('c', 64));
        var locator = new SourceFactLocator(Address, Id, 1, 2, 3, 4, new string('d', 64));
        var cut = new AuthoritativeReadCut("stream://**", 9, At);

        // Act
        // Assert
        Assert.Equal(Content, RoundTrip(Content));
        Assert.Equal(manifest, RoundTrip(manifest));
        Assert.Equal(locator, RoundTrip(locator));
        Assert.Equal(cut, RoundTrip(cut));
        Assert.Equal(Protected, RoundTrip(Protected));
        Assert.Equal(ReadCutScope.Realm, RoundTrip(ReadCutScope.Realm));
    }

    [Fact]
    public void ShouldRoundTripGivenNativeOperationContracts()
    {
        // Arrange
        var provenance = new OperationProvenance(Id, Id2, null, "Cmd", Actor, Address, "initial", 5,
            new string('f', 64), At, Address, Address);
        var header = new DurableIntentHeader(Id, Address, Address, Address, Actor, "initial", null,
            new string('f', 64), At);
        var concurrency = new EntityConcurrency("thing", Id, 3, "initial");
        var first = new FirstResponseDescriptor(200, "application/json", "x.v1", new StandardResponseBody(Content));
        var mutation = new MutationResultContent("x.v1", new ProtectedResponseBody(Protected));
        var accepted = new OperationAcceptedResponse(Id, "/status", At);
        var status = new OperationStatusResponse(Id, OperationStatus.Completed,
            new OperationResultDescriptor("x.v1", "/result"), null);
        var subject = new OperationSubject(Address, Address);
        var scope = new OperationAccessScope("realm", "policy", Actor, Id, null, Address, Content);

        // Act
        // Assert
        Assert.Equal(provenance, RoundTrip(provenance));
        Assert.Equal(header, RoundTrip(header));
        Assert.Equal(concurrency, RoundTrip(concurrency));
        Assert.Equal(first, RoundTrip(first));
        Assert.Equal(mutation, RoundTrip(mutation));
        Assert.Equal(accepted, RoundTrip(accepted));
        Assert.Equal(status, RoundTrip(status));
        Assert.Equal(subject, RoundTrip(subject));
        Assert.Equal(scope, RoundTrip(scope));
        Assert.Equal(OperationStatus.Unknown, RoundTrip(OperationStatus.Unknown));
    }

    [Fact]
    public void ShouldEmitDiscriminatorGivenResponseBodyReference()
    {
        // Arrange
        ResponseBodyReference standard = new StandardResponseBody(Content);
        ResponseBodyReference protectedBody = new ProtectedResponseBody(Protected);

        // Act
        var standardJson = JsonSerializer.Serialize(standard, ComplianceCoreJsonContext.Default.ResponseBodyReference);
        var protectedJson = JsonSerializer.Serialize(protectedBody,
            ComplianceCoreJsonContext.Default.ResponseBodyReference);

        // Assert
        Assert.Contains("\"kind\":\"standard\"", standardJson, StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"protected\"", protectedJson, StringComparison.Ordinal);
        Assert.IsType<ProtectedResponseBody>(JsonSerializer.Deserialize(protectedJson,
            ComplianceCoreJsonContext.Default.ResponseBodyReference));
    }
}
