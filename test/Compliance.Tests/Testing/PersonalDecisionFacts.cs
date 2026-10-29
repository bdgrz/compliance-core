using System.Security.Claims;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Queries;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds the sources a partner evaluation relies on through the real owners (ratifier duty, draft rules,
///     ratification, partner staff and engagement-partner designation) and drives admitted personal decisions through
///     their process, as the reactor and sweep do.
/// </summary>
static class PersonalDecisionFacts
{
    /// <summary>Rules that classify a readiness service as conditionally compatible, so a partner must evaluate.</summary>
    public static IndependenceRuleContent ConditionalRules { get; } = new(12,
        [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
        "Synthetic conditional policy source");

    /// <summary>A readiness service shortly before the draft period, so the history needs a partner evaluation.</summary>
    public static NonattestServiceContent ReadinessService() => IndependenceHost.Service("readiness",
        new DateOnly(2025, 12, 1));

    /// <summary>Seeds ratified rules and a current engagement partner for the host's client.</summary>
    public static Task<PartnerSetup> SeedPartnerAsync(IndependenceHost host,
        IndependenceRuleContent? rules = null, string partnerPractice = "attest") =>
        SeedPartnerAsync(host.Provider, host.Tenant, rules, partnerPractice);

    /// <summary>
    ///     Seeds ratified rules and a current engagement partner of <paramref name="tenant" /> in any composition;
    ///     <paramref name="partnerUserId" /> names the canonical user who is the partner, for a signed-in caller.
    /// </summary>
    public static async Task<PartnerSetup> SeedPartnerAsync(IServiceProvider provider, Uuid tenant,
        IndependenceRuleContent? rules = null, string partnerPractice = "attest", Uuid? partnerUserId = null)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
        var operatorActor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var content = rules ?? ConditionalRules;

        var ratifierUser = Uuid.CreateVersion4();
        var ratifier = await FirmStaffFacts.RegisterAsync(provider, Uuid.CreateVersion4(), ratifierUser, "attest",
            "Synthetic ratifier source", operatorActor, IndependenceHost.At);
        var ratifierAssertion = ProfessionalDutyAssertionTestIssuer.Issue(Uuid.CreateVersion4(), ratifierUser,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, at.AddMinutes(-1), at.AddMinutes(57));
        var ratifierDuty = await ProfessionalDutyFacts.RecordAsync(provider, ratifierAssertion, ratifier,
            operatorActor, at);
        Assert.True(ratifierDuty.IsSuccess, ratifierDuty.Error?.Message);
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleCatalogRules(), catalog => catalog.ReviseRules(
            Uuid.CreateVersion4(), catalog.Sequence, content, operatorActor, IndependenceHost.At));
        await using var scope = provider.CreateAsyncScope();
        var ratificationId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new IndependenceRuleRatificationRules(), catalog =>
            catalog.Ratify(Uuid.CreateVersion4(), ratificationId, 1, 1, catalog.Sequence,
                IndependenceSourceDigest.RuleContent(content), "Synthetic ratification evidence", ratifier,
                ratifierDuty.Value!, ActorReference.ForFirmStaff(ratifierUser, "Synthetic ratifier"), at));
        var ratified = await scope.ServiceProvider.GetRequiredService<IndependenceRuleRatificationReader>().ReadAsync();
        Assert.Equal(ratificationId, ratified.Active!.RatificationId);

        var partnerUser = partnerUserId ?? Uuid.CreateVersion4();
        var staff = await FirmStaffFacts.RegisterAsync(provider, Uuid.CreateVersion4(), partnerUser, partnerPractice,
            "Synthetic partner source", operatorActor, IndependenceHost.At);
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(Uuid.CreateVersion4(), partnerUser,
            FirmProfessionalDuty.EngagementPartner, tenant, 1, "active", null, at.AddMinutes(-1),
            at.AddMinutes(57));
        var duty = await ProfessionalDutyFacts.RecordAsync(provider, assertion, staff, operatorActor, at);
        Assert.True(duty.IsSuccess, duty.Error?.Message);
        return new PartnerSetup(staff, IndependenceHost.ActorFor(partnerUser), duty.Value!, assertion, content,
            operatorActor, at, ratifier, ratifierDuty.Value!);
    }

    /// <summary>Creates the engagement and records one readiness service; returns the engagement and service ids.</summary>
    public static async Task<(Uuid Engagement, Uuid Service)> SeedEngagementAsync(IndependenceHost host,
        string practice = "attest")
    {
        var lead = await host.RegisterStaffAsync(practice);
        var engagement = Uuid.CreateVersion4();
        var created = await host.SendAsync(new CreateServiceEngagement(host.Tenant, engagement, 0,
            IndependenceHost.Draft(lead)));
        Assert.True(created.IsSuccess, created.Error?.Message);
        var service = Uuid.CreateVersion4();
        var recorded = await host.SendAsync(new RecordNonattestService(host.Tenant, service, 0, ReadinessService()));
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        return (engagement, service);
    }

    /// <summary>The partner evaluation command for the exact current sources of the host's client.</summary>
    public static async Task<RecordPartnerIndependenceEvaluation> EvaluationAsync(IndependenceHost host, Uuid engagement,
        PartnerSetup partner, long engagementRevision = 1, string rationale = "Synthetic partner rationale")
    {
        var history = await host.SendAsync(new GetClientIndependenceHistory(host.Tenant));
        Assert.True(history.IsSuccess, history.Error?.Message);
        return new RecordPartnerIndependenceEvaluation(host.Tenant, engagement, Uuid.CreateVersion4(),
            engagementRevision, 1, IndependenceSourceDigest.RuleContent(partner.Rules),
            IndependenceSourceDigest.Services(history.Value!.Services), rationale, history.Value.Sequence);
    }

    /// <summary>The management acknowledgement command for the exact current history of the host's client.</summary>
    public static async Task<AcknowledgeEngagementManagement> AcknowledgementAsync(IndependenceHost host,
        Uuid engagement, long engagementRevision = 1, string statement = "I retain management responsibility.")
    {
        var history = await host.SendAsync(new GetClientIndependenceHistory(host.Tenant));
        Assert.True(history.IsSuccess, history.Error?.Message);
        return new AcknowledgeEngagementManagement(host.Tenant, engagement, Uuid.CreateVersion4(), engagementRevision,
            history.Value!.Services.Select(service => service.ServiceRecordId).ToArray(), statement,
            history.Value.Sequence);
    }

    public static ProcessKey KeyOf(Uuid decision, NativeFactReference source) =>
        ProcessKey.ForRoot(EngagementPersonalDecisionEffects.ProcessName, decision, source);

    /// <summary>Creates (if absent) the process record of an admitted decision without dispatching any receiver.</summary>
    public static async Task<ProcessRecord> StartAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid decision)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ServiceEngagementIndependence(tenant, engagement, selectedRootId: decision));
        var admitted = owner.DecisionOf(decision)?.Admitted ?? throw new InvalidOperationException("Admit first.");
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(
            KeyOf(decision, NativeFactReference.Of(owner.Stream, admitted)), NativeFactReference.Of(owner.Stream, admitted), admitted.Operation.Episode,
            admitted.Intent, [], DateTimeOffset.UtcNow));
    }

    /// <summary>Starts (if absent) and drives the process of an admitted decision until it ends or waits.</summary>
    public static async Task<ProcessRecord> RunAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid decision)
    {
        var started = await StartAsync(provider, tenant, engagement, decision);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Requests an abort of the process of an admitted decision, as an operator or the sweep would.</summary>
    public static async Task<ProcessRecord> AbortAsync(IServiceProvider provider, Uuid tenant, Uuid engagement,
        Uuid decision)
    {
        var started = await StartAsync(provider, tenant, engagement, decision);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().AbortAsync(started.Key);
    }

    /// <summary>The wire form of a request as a trusted system caller, for receiver replays.</summary>
    public static async Task<Result<ReceiverOutcome>> SendReceiverAsync(IServiceProvider provider,
        IRequest<ReceiverOutcome> command)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(command, RequestActor.System);
    }

    internal sealed record PartnerSetup(FirmStaffMemberView Staff, ClaimsPrincipal Actor,
        FirmProfessionalDutyDesignationView Duty, ProfessionalDutyAssertionTestIssuer.SignedAssertion Assertion,
        IndependenceRuleContent Rules, ActorReference OperatorActor, DateTimeOffset At, FirmStaffMemberView Ratifier,
        FirmProfessionalDutyDesignationView RatifierDuty);
}
