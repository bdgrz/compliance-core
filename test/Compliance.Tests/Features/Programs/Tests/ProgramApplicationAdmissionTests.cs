using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Programs.Services;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramApplicationAdmissionTests
{
    [Fact]
    public async Task ShouldAdmitGivenExactCreatedSourceAndActiveTenant()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = await CreatedAsync(tenant);
        var admission = new ProgramApplicationAdmission(new SourceReader(program), new TenantActivity(tenant));

        // Act
        var result = await admission.CheckAsync(tenant, program.Id);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Fact]
    public async Task ShouldRefuseGivenProgramAbsentFromAuthoritativeSource()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var admission = new ProgramApplicationAdmission(new SourceReader(), new TenantActivity(tenant));

        // Act
        var result = await admission.CheckAsync(tenant, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRefuseGivenSameProgramIdentifierBelongsToAnotherTenant()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = await CreatedAsync(Uuid.CreateVersion4());
        var admission = new ProgramApplicationAdmission(new SourceReader(program), new TenantActivity(tenant));

        // Act
        var result = await admission.CheckAsync(tenant, program.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public async Task ShouldRecheckCurrentTenantGivenPreviouslyAdmittedProgram()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = await CreatedAsync(tenant);
        var activity = new TenantActivity(tenant);
        var admission = new ProgramApplicationAdmission(new SourceReader(program), activity);
        Assert.True((await admission.CheckAsync(tenant, program.Id)).IsSuccess);
        activity.Active = false;

        // Act
        var result = await admission.CheckAsync(tenant, program.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldRefuseGivenMissingTenantOrProgramIdentity(bool missingTenant)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var admission = new ProgramApplicationAdmission(new SourceReader(), new TenantActivity(tenant));

        // Act
        var result = await admission.CheckAsync(missingTenant ? Uuid.Empty : tenant,
            missingTenant ? Uuid.CreateVersion4() : Uuid.Empty);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    static async Task<ComplianceProgram> CreatedAsync(Uuid tenant)
    {
        var rules = new ProgramRules(tenant, Uuid.CreateVersion4());
        Assert.Null(rules.Create("Program", new ProgramPlan(null, null, null, null, null, null),
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        return await ProgramSeeds.OwnerAsync(new FitzKvImmutableContentStore(new InMemoryKvClient()), rules);
    }

    sealed class SourceReader(params ComplianceProgram[] programs) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            var source = programs.FirstOrDefault(program => program.Stream == aggregate.Stream);
            return ValueTask.FromResult(source is null ? aggregate : (TAggregate)(Aggregate)source);
        }
    }

    sealed class TenantActivity(Uuid tenant) : ITenantActivity
    {
        public bool Active { get; set; } = true;
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(Active && tenantId == tenant);
    }
}
