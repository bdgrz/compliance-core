using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Changes;
using Bdgrz.Compliance.Features.Incidents;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Changes;

public sealed class RestrictedComplianceRecordMutationAuthorizerTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 19, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldLimitRestrictedChangeMutationsToCurrentAssessorOrPrivilegedRoleGivenAnAssessedRestrictedChange()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var assessorUserId = Uuid.CreateVersion4();
        var assessorMemberId = RbacIds.Member(tenantId, assessorUserId);
        await scenario.SaveAsync(AssessedChange(tenantId, programId, changeId, assessorMemberId));
        var request = new AssessSignificantChange(tenantId, programId, changeId, 2,
            new SignificantChangeImpactContent("significant", "The change affects the boundary.",
                true, false, false, false, ["scope", "system_description"],
                [new("system_description_section", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)],
                [], [], [], [], true));

        var deniedPermissions = new RecordingPermissionAuthorizer(false);

        // Act
        var denied = await Authorizer(scenario.Reader, deniedPermissions).AuthorizeAsync(
            Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var assessor = await Authorizer(scenario.Reader, deniedPermissions).AuthorizeAsync(
            Context(request, assessorUserId), CancellationToken.None);
        var privilegedPermissions = new RecordingPermissionAuthorizer(true);
        var privileged = await Authorizer(scenario.Reader, privilegedPermissions).AuthorizeAsync(
            Context(request, Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, denied.Error!.Kind);
        Assert.Equal(RbacPermissions.ComplianceRecordRestrictedWrite,
            Assert.Single(deniedPermissions.Permissions));
        Assert.True(assessor.IsSuccess);
        Assert.True(privileged.IsSuccess);
        Assert.Equal(RbacPermissions.ComplianceRecordRestrictedWrite,
            Assert.Single(privilegedPermissions.Permissions));
    }

    [Fact]
    public async Task ShouldApplyRestrictedEditingRuleToIncidentResolutionGivenAnAssessedRestrictedIncident()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var incidentId = Uuid.CreateVersion4();
        var assessorUserId = Uuid.CreateVersion4();
        var assessorMemberId = RbacIds.Member(tenantId, assessorUserId);
        await scenario.SaveAsync(AssessedIncident(tenantId, programId, incidentId, assessorMemberId));
        var request = new RecordComplianceIncidentResolution(tenantId, programId, incidentId, 2,
            new ComplianceIncidentResolutionContent("The issue was contained.", Now, [], []));
        var permissions = new RecordingPermissionAuthorizer(false);

        // Act
        var denied = await Authorizer(scenario.Reader, permissions).AuthorizeAsync(
            Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var assessor = await Authorizer(scenario.Reader, permissions).AuthorizeAsync(
            Context(request, assessorUserId), CancellationToken.None);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, denied.Error!.Kind);
        Assert.True(assessor.IsSuccess);
    }

    [Fact]
    public async Task ShouldRequirePrivilegedPermissionToCreateRestrictedRecordGivenNoRecordedAssessor()
    {
        // Arrange
        await using var scenario = new Scenario();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var changeId = Uuid.CreateVersion4();
        var content = ChangeContent(tenantId, "restricted");
        var request = new RecordSignificantChange(tenantId, programId, changeId, content);

        // Act
        var denied = await Authorizer(scenario.Reader, new RecordingPermissionAuthorizer(false))
            .AuthorizeAsync(Context(request, Uuid.CreateVersion4()), CancellationToken.None);
        var allowed = await Authorizer(scenario.Reader, new RecordingPermissionAuthorizer(true))
            .AuthorizeAsync(Context(request, Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, denied.Error!.Kind);
        Assert.True(allowed.IsSuccess);
    }

    static RestrictedComplianceRecordMutationAuthorizer Authorizer(IAggregateReader reader,
        IPermissionAuthorizer permissions) => new(reader,
        new RestrictedComplianceRecordVisibility(permissions));

    static RequestContext<IRestrictedComplianceRecordMutationRequest> Context(
        IRestrictedComplianceRecordMutationRequest request, Uuid userId) => new(request,
        new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "bdgrz"),
            new Claim("sub", userId.ToString()),
            new Claim("name", "Test user"),
        ], "UnitTest")));

    static SignificantChange AssessedChange(Uuid tenantId, Uuid programId, Uuid changeId,
        Uuid assessorMemberId)
    {
        var change = new SignificantChange(tenantId, programId, changeId);
        Assert.Null(change.Record(ChangeContent(tenantId, "restricted"),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()), "Reporter", Now.AddMinutes(-2)));
        Assert.Null(change.Assess(change.Revision, new SignificantChangeImpactContent(
            "significant", "The change affects the approved system description.", true,
            false, false, false, ["scope", "system_description"],
            [new SignificantChangeAffectedRecordReference("system_description_section",
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)], [], [], [], [], true),
            assessorMemberId, "Assessor", Now.AddMinutes(-1)));
        return change;
    }

    static ComplianceIncident AssessedIncident(Uuid tenantId, Uuid programId, Uuid incidentId,
        Uuid assessorMemberId)
    {
        var incident = new ComplianceIncident(tenantId, programId, incidentId);
        Assert.Null(incident.Record(new ComplianceIncidentContent(null, null, null, null,
            "A restricted customer data alert", "Customer data was briefly exposed.", "security",
            "high", Now.AddMinutes(-3), Now.AddMinutes(-2),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()), true, false,
            [Uuid.CreateVersion4()], "restricted", []),
            RbacIds.Member(tenantId, Uuid.CreateVersion4()), "Reporter", Now.AddMinutes(-2)));
        Assert.Null(incident.Assess(incident.Revision,
            new ComplianceIncidentImpactAssessmentContent("significant",
                "Customer data was affected by the incident.", true, true, true, true,
                ["scope", "control", "risk", "vendor", "evidence", "system_description"],
                [new ComplianceIncidentAffectedRecordReference("system_description_section",
                    Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)], [], [], true, true, false),
            assessorMemberId, "Assessor", Now.AddMinutes(-1)));
        return incident;
    }

    static SignificantChangeContent ChangeContent(Uuid tenantId, string handlingClass) => new(
        null, null, null, "Restricted identity change", "Update production identity controls.",
        new DateOnly(2026, 10, 18), null, RbacIds.Member(tenantId, Uuid.CreateVersion4()),
        [Uuid.CreateVersion4()], "high", handlingClass, []);

    sealed class Scenario : IAsyncDisposable
    {
        readonly ServiceProvider provider;
        readonly AsyncServiceScope scope;

        public Scenario()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddPortia();
            provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            scope = provider.CreateAsyncScope();
            Reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
            writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        }

        readonly IAggregateWriter writer;
        public IAggregateReader Reader { get; }

        public ValueTask SaveAsync(Aggregate aggregate) => writer.SaveAsync(aggregate,
            new RequestDispatchContext(RequestActor.System));

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }
}
