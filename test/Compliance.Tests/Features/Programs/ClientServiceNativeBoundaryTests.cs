using System.Text.Json;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Hosting.Persistence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ClientServiceNativeBoundaryTests
{
    [Theory]
    [InlineData("create")]
    [InlineData("revise")]
    [InlineData("retire")]
    public async Task ShouldKeepNativePayloadBoundedGivenCompleteUnicodeServiceValue(string mutation)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        await using var fixture = new ProductClientServiceFixture();
        var service = new ProductClientServiceRules(tenant, Uuid.CreateVersion4());
        var at = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var member = Uuid.CreateVersion4();
        var authored = string.Concat(Enumerable.Repeat("审计🦡", 8_000));
        var created = service.Create(Uuid.CreateVersion4(), mutation == "create" ? authored : "Payroll",
            mutation == "create" ? authored : "Run payroll", "Operations", member, "Owner", at);
        Assert.True(created.IsSuccess, created.Error?.Message);
        if (mutation == "revise")
            Assert.Null(service.Revise(1, authored, authored, authored, member, "Owner", at.AddMinutes(1)));
        if (mutation == "retire")
            Assert.Null(service.Retire(1, authored, member, "Owner", at.AddMinutes(1)));
        var facts = await ProductClientServiceSeeds.FactsAsync(fixture.Content, service);
        var fact = facts[^1];
        Assert.Equal(mutation == "create" ? 1 : 2, facts.Count);

        // Act
        var bytes = DomainEventPayload.Size(fact);
        var complete = mutation == "retire"
            ? (await ProductClientServiceReader.ReadRetirementAsync(fixture.Content, Assert.IsType<ClientServiceRetired>(fact))).Rationale
            : (await ProductClientServiceReader.ReadContentAsync(fixture.Content, (IProductClientServiceFact)fact)).Name;

        // Assert
        Assert.Equal(authored, complete);
        Assert.True(bytes <= DomainEventPayload.MaximumBytes,
            $"The real {fact.GetType().Name} payload retains {bytes} bytes of authored service content.");
    }

    [Fact]
    public async Task ShouldReturnSelectedServiceIdentityGivenIdenticalCreateDispatchedTwice()
    {
        // Arrange
        await using var provider = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateProgramHandler>().AddRequestHandler<CreateClientServiceHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var selectedService = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var programJson = $$"""{"program_id":"{{Uuid.CreateVersion4()}}","tenant_id":"{{tenant}}","name":"SOC 2","plan":{} }""";
        var programRequest = JsonSerializer.Deserialize(programJson, ComplianceCoreJsonContext.Default.CreateProgram)!;
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var program = await bus.SendAsync(programRequest, actor);
        Assert.True(program.IsSuccess, program.Error?.Message);
        // The selected ServiceId remains extra JSON against the old DTO, keeping this an existing-API regression.
        var json = $$"""{"service_id":"{{selectedService}}","tenant_id":"{{tenant}}","program_id":"{{program.Value.ProgramId}}","name":"Payroll","purpose":"Run payroll","owner_reference":"Operations"}""";
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.CreateClientService)!;
        var first = await bus.SendAsync(request, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var replay = await bus.SendAsync(request, actor);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(selectedService, replay.Value.ServiceId);
    }
}
