using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Programs.Aggregates;
using Bdgrz.Compliance.Features.Programs.Commands;
using Bdgrz.Compliance.Features.Programs.Handlers;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProgramNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepCurrentCriteriaAuthorityGivenManyEditionSelections()
    {
        // Arrange
        await using var fixture = new ProgramNativeFixture();
        var rules = new ProgramRules(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var member = Uuid.CreateVersion4();
        var at = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(rules.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null), member, "Manager", at));
        var first = Uuid.CreateVersion4();
        var current = first;
        for (var index = 0; index < 32; index++)
        {
            current = index == 0 ? first : Uuid.CreateVersion4();
            Assert.Null(rules.SelectCriteriaEdition(index + 1, current, member, "Manager", at.AddMinutes(index + 1)));
        }

        await fixture.PublishAsync(rules);
        var history = await fixture.Programs.HistoryAsync(rules.TenantId, rules.Id);
        var owner = history.Owner;

        // Act
        var retained = typeof(ComplianceProgram).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is global::System.Collections.ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(33, owner.Revision);
        Assert.Equal(current, owner.CriteriaEditionId);
        Assert.Equal(first, history.CriteriaSelectionAt(at.AddMinutes(1))?.EditionId);
    }

    [Fact]
    public async Task ShouldReturnSelectedCreationIdentityGivenSameProgramCommandDispatchedTwice()
    {
        // Arrange
        await using var provider = ProgramManagementServices.Build(new RecordingPermissionAuthorizer(allowed: true),
            portia => portia.AddRequestHandler<CreateProgramHandler>());
        await using var scope = provider.CreateAsyncScope();
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        // The selected creation identity is required authored input; unrelated dispatch traces do not allocate another program.
        var json = """{"program_id":"__program__","tenant_id":"__tenant__","name":"SOC 2","plan":{"target_readiness_date":null,"target_type_i_as_of_date":null,"target_type_ii_start_date":null,"target_type_ii_end_date":null,"readiness_advisor":null,"audit_firm":null}}"""
            .Replace("__program__", program.ToString(), StringComparison.Ordinal)
            .Replace("__tenant__", tenant.ToString(), StringComparison.Ordinal);
        var request = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.CreateProgram)!;
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var first = await bus.SendAsync(request, actor);
        Assert.True(first.IsSuccess, first.Error?.Message);

        // Act
        var replay = await bus.SendAsync(request, actor);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(program, replay.Value.ProgramId);
    }
}
