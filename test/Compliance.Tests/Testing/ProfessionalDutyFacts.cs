using System.Security.Claims;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Projectors;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds well-formed designation facts straight onto the designation owner, exactly as the change process leaves
///     them (accepted, then recorded or revoked), and drives accepted changes through their process like the reactor
///     and sweep do.
/// </summary>
static class ProfessionalDutyFacts
{
    const string Realm = "bdgrz";

    static readonly ImmutableContentRef FirstResponse = new(Uuid.Parse("00000000-0000-4000-8000-0000000000f2",
        System.Globalization.CultureInfo.InvariantCulture), new string('d', 64), 2, "application/json",
        "first-response-descriptor.v1");

    /// <summary>Records the designation of a signed active assertion for an active staff member.</summary>
    public static async Task<Result<FirmProfessionalDutyDesignationView>> RecordAsync(IServiceProvider provider,
        ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion, FirmStaffMemberView staff, ActorReference actor,
        DateTimeOffset at) => await ApplyAsync(provider, ProfessionalDutyChangeKind.Record, assertion, staff, actor, at)
        .ConfigureAwait(false);

    /// <summary>Revokes the designation with a signed revocation assertion.</summary>
    public static async Task<Result<FirmProfessionalDutyDesignationView>> RevokeAsync(IServiceProvider provider,
        ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion, ActorReference actor, DateTimeOffset at) =>
        await ApplyAsync(provider, ProfessionalDutyChangeKind.Revoke, assertion, null, actor, at)
            .ConfigureAwait(false);

    /// <summary>Projects every applied designation change into the catalog read model, as the hosted projector does.</summary>
    public static async Task ProjectDirectoryAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var projector = new FirmProfessionalDutyDirectoryProjector(
            scope.ServiceProvider.GetRequiredService<FitzFirmProfessionalDutyDirectory>(),
            scope.ServiceProvider.GetRequiredService<ProfessionalDutyReader>());
        await new ProjectorRunner(scope.ServiceProvider.GetRequiredService<IDomainEventReader>())
            .RunAsync(projector, ProjectionCheckpoint.Start);
    }

    /// <summary>Starts (if absent) and drives the process of an accepted change, as the reactor and sweep do.</summary>
    public static async Task<ProcessRecord> RunProcessAsync(IServiceProvider provider, Uuid userId, string duty,
        Uuid? tenantId, Uuid assertionId)
    {
        var started = await StartProcessAsync(provider, userId, duty, tenantId, assertionId);
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDriver>().RunAsync(started.Key, "test-worker",
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
    }

    /// <summary>Creates (if absent) the process record of an accepted change without dispatching any receiver.</summary>
    public static async Task<ProcessRecord> StartProcessAsync(IServiceProvider provider, Uuid userId, string duty,
        Uuid? tenantId, Uuid assertionId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new FirmProfessionalDutyDesignation(userId, duty, tenantId, assertionId, assertionId));
        var accepted = owner.AcceptedFor(assertionId) ?? throw new InvalidOperationException("The change was not accepted.");
        var store = services.GetRequiredService<IImmutableContentStore>();
        var intent = await store.ReadJsonAsync<ProfessionalDutyChangeIntent>(Realm, accepted.Intent);
        var evidence = await store.ReadJsonAsync<VerifiedFirmProfessionalDutyAssertion>(Realm, intent.VerifiedScope);
        var key = ProcessKey.ForRoot(ProfessionalDutyEffects.ProcessName, assertionId,
            NativeFactReference.Of(owner.Stream, accepted));
        return await services.GetRequiredService<IProcessRecordStore>().CreateIfAbsentAsync(ProcessRecord.Start(key,
            NativeFactReference.Of(owner.Stream, accepted), accepted.Operation.Episode, accepted.Intent,
            [ProfessionalDutyTarget.For(intent, evidence).ToString()], DateTimeOffset.UtcNow));
    }

    /// <summary>Admits a change through the bus and runs its process to the end; returns the original admission.</summary>
    public static async Task<Result<OperationAcceptedResponse>> AdmitThroughProcessAsync<TRequest>(
        IServiceProvider provider, ClaimsPrincipal actor, TRequest request,
        ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion)
        where TRequest : IRequest<OperationAcceptedResponse>, IProfessionalDutyAdministrationRequest
    {
        await using var scope = provider.CreateAsyncScope();
        var admitted = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            new RequestDispatchContext(actor, new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic")),
            CancellationToken.None);
        if (admitted.IsSuccess)
            await RunProcessAsync(provider, assertion.Evidence.UserId, assertion.Evidence.Duty,
                assertion.Evidence.TenantId, assertion.Evidence.AssertionId);
        return admitted;
    }

    static async Task<Result<FirmProfessionalDutyDesignationView>> ApplyAsync(IServiceProvider provider,
        ProfessionalDutyChangeKind kind, ProfessionalDutyAssertionTestIssuer.SignedAssertion assertion,
        FirmStaffMemberView? staff, ActorReference actor, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var evidence = assertion.Evidence;
        var assertionId = evidence.AssertionId;
        var owner = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty,
            evidence.TenantId, assertionId));
        var current = owner.LatestContent is { } latest
            ? await content.ReadJsonAsync<FirmProfessionalDutyDesignationView>(Realm, latest)
            : null;
        var chain = ProfessionalDutyRules.CheckChain(kind, evidence, current);
        if (!chain.IsSuccess)
            return Result<FirmProfessionalDutyDesignationView>.Failure(chain.Error!);

        var stream = owner.Stream;
        var revision = (long)owner.Revision + 1;
        var view = kind == ProfessionalDutyChangeKind.Record
            ? ProfessionalDutyRules.Recorded(current, evidence, staff!.StaffMemberId, staff.Revision, revision, actor, at)
            : ProfessionalDutyRules.Revoked(current!, evidence, revision, actor, at);
        ImmutableContentRef authored;
        string digest;
        if (kind == ProfessionalDutyChangeKind.Record)
        {
            var command = new RecordFirmProfessionalDutyDesignation(evidence.DesignationId, assertionId,
                assertion.Token, (long)owner.Revision);
            authored = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), ProfessionalDutyContent.RecordCommandSchema, command);
            digest = AuthoredIntentDigest.Compute(command);
        }
        else
        {
            var command = new RevokeFirmProfessionalDutyDesignation(evidence.DesignationId, assertionId,
                assertion.Token, (long)owner.Revision);
            authored = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), ProfessionalDutyContent.RevokeCommandSchema, command);
            digest = AuthoredIntentDigest.Compute(command);
        }
        var header = new DurableIntentHeader(assertionId, stream, stream, stream, actor, "initial", owner.Revision,
            digest, at);
        var scopeRef = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(),
            ProfessionalDutyContent.VerifiedScopeSchema, evidence);
        SourceUseRequirement? staffSource = null;
        if (staff is not null)
        {
            var source = await reader.HydrateAsync(new FirmStaffRecord(staff.StaffMemberId));
            staffSource = new SourceUseRequirement(source.Stream, NativeFactReference.Of(source.Stream, source.Latest!),
                source.Revision, SourceUsePurpose.ProfessionalDutyRecording, source.Latest!.StaffContent);
        }
        var intent = await content.WriteJsonAsync(Realm, Uuid.CreateVersion4(), ProfessionalDutyChangeIntent.Schema,
            new ProfessionalDutyChangeIntent(header, evidence.DesignationId, kind, authored, assertion.Token,
                assertionId, (ulong)evidence.AuthorityRevision, scopeRef,
                owner.Latest is DomainEvent previous ? NativeFactReference.Of(stream, previous) : null, staffSource));
        var accepted = await executor.ExecuteAsync(new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty,
            evidence.TenantId, assertionId), item => AggregateOutcome.Commit(item.AcceptChange(
                Provenance(assertionId, assertionId, kind == ProfessionalDutyChangeKind.Record ? nameof(RecordFirmProfessionalDutyDesignation) : nameof(RevokeFirmProfessionalDutyDesignation), actor, stream, header, at), intent,
                FirstResponse)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        var designation = await ProfessionalDutyContent.WriteDesignationAsync(content, Uuid.CreateVersion4(), kind,
            view, CancellationToken.None);
        var result = await ProfessionalDutyContent.WriteResultAsync(content, Uuid.CreateVersion4(), kind, designation,
            CancellationToken.None);
        var acceptedOwner = await reader.HydrateAsync(new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty,
            evidence.TenantId, assertionId, assertionId));
        var cause = acceptedOwner.AcceptedFor(assertionId)!.Metadata.EventId;
        var applyId = Uuid.CreateVersion4();
        var applied = await executor.ExecuteAsync(new FirmProfessionalDutyDesignation(evidence.UserId, evidence.Duty,
            evidence.TenantId, applyId, assertionId), item => AggregateOutcome.Commit(item.Apply(kind,
                Provenance(applyId, assertionId, "ApplyProfessionalDutyChange", actor, stream, header,
                    at, cause), designation, assertion.Token, result)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        return applied.IsSuccess
            ? Result<FirmProfessionalDutyDesignationView>.Success(view)
            : Result<FirmProfessionalDutyDesignationView>.Failure(applied.Error!);
    }

    static OperationProvenance Provenance(Uuid logicalId, Uuid rootId, string kind, ActorReference actor,
        EventStreamAddress owner, DurableIntentHeader header, DateTimeOffset acceptedAt, Uuid? cause = null) => new(logicalId, rootId,
        cause, kind, actor, owner, header.Episode, header.OriginalExpectedRevision,
        header.AuthoredIntentSha256, acceptedAt, owner, owner, header.FirstOwner);
}
