using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
namespace Bdgrz.Compliance.Features.Work.Handlers;

/// <summary>Records and replays the acting member's own authored weekly digest preference.</summary>
public sealed class SetWorkDigestPreferenceHandler(NativeOperationExecutor operations, IAggregateReader reader,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<SetWorkDigestPreference, WorkDigestPreferenceView>
{
    const string Schema = "work-digest-preference-view.v1";
    public async ValueTask<Result<WorkDigestPreferenceView>> HandleAsync(IRequestContext<SetWorkDigestPreference> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || !WorkDigestSchedule.IsValidTimeZone(request.TimeZoneId))
            return Result<WorkDigestPreferenceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "A typed command, tenant and valid optional time_zone_id are required."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var user))
            return Result<WorkDigestPreferenceView>.Failure(new RequestError(RequestErrorKind.Unauthorized, "A personal member identity is required."));
        var member = await reader.HydrateAsync(new Member(request.TenantId, user), ct).ConfigureAwait(false);
        if (!member.IsAccessEligibleOn(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) || member.UserId != user || member.Affiliation == "firm_staff")
            return Result<WorkDigestPreferenceView>.Failure(new RequestError(RequestErrorKind.Forbidden, "Current active client membership is required."));
        var actor = ActorReference.ForMember(member.Id, UserIdentityClaims.BdgrzDisplay(context.Actor, user));
        var stream = new WorkDigestPreference(request.TenantId, member.Id).Stream;
        var operation = new OperationProvenance(request.CommandId, request.CommandId, null, nameof(SetWorkDigestPreference),
            actor, stream, RootOperation.InitialEpisode, null, AuthoredIntentDigest.Compute(request), clock.GetUtcNow(),
            stream, stream, FirstOwner: stream);
        WorkDigestPreferenceRecorded? prepared = null;
        ulong version = 0;
        ulong position = 0;
        try
        {
            var result = await operations.ExecuteAsync(new NativeOperation<WorkDigestPreference>(AcceptedOperationBinding.Of(operation),
                () => new WorkDigestPreference(request.TenantId, member.Id, request.CommandId), owner => owner.Acceptance,
                async (owner, token) =>
                {
                    var at = clock.GetUtcNow();
                    var view = WorkDigestPreference.Decide(owner.Read(), request.EmailDigestEnabled, request.TimeZoneId, at);
                    var body = await content.WriteJsonAsync(stream.Realm, Uuid.CreateVersion4(), Schema, view, token).ConfigureAwait(false);
                    var response = new FirstResponseDescriptor(200, ImmutableContentStoreExtensions.JsonMediaType, Schema, new StandardResponseBody(body));
                    var mutation = await content.WriteJsonAsync(stream.Realm, Uuid.CreateVersion4(), "mutation-result-content.v1",
                        new MutationResultContent(Schema, new StandardResponseBody(body)), token).ConfigureAwait(false);
                    (version, position) = (owner.Version, owner.CommittedStreamPosition);
                    prepared = new WorkDigestPreferenceRecorded(request.TenantId, member.Id, request.EmailDigestEnabled,
                        request.TimeZoneId, view == owner.Read(), view, operation with { AcceptedAt = at }, null, mutation);
                    return Result<FirstResponseDescriptor>.Success(response);
                }, (owner, first) => owner.Record((prepared ?? throw new InvalidOperationException("Missing prepared preference.")) with
                { FirstResponse = first }, version, position)), context, ct).ConfigureAwait(false);
            return await IndependenceContent.ReadOutcomeAsync<WorkDigestPreferenceView>(content, stream.Realm, result, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException error)
        {
            ct.ThrowIfCancellationRequested();
            return Result<WorkDigestPreferenceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The original preference receipt cannot be verified.", error is ImmutableContentUnavailableException));
        }
    }
}
