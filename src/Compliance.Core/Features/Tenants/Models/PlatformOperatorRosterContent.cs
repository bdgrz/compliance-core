using System.Text.Json;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Tenants.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Commands;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Content.Persistence;
namespace Bdgrz.Compliance.Features.Tenants.Models;

/// <summary>Verified immutable roster authority, full change history and exact receipt reconstruction.</summary>
public static class PlatformOperatorRosterContent
{
    public const string Realm = "bdgrz";
    public const string AuthoritySchema = "platform-operator-current-authority.v1";
    public const string ChangeSchema = "platform-operator-mutation.v1";

    public static PlatformOperatorAuthorityContent Empty => new(PlatformOperatorRoster.CreationId, 0, []);

    public static async Task<PlatformOperatorAuthorityContent> ReadAsync(IImmutableContentStore content,
        PlatformOperatorRoster owner, CancellationToken ct = default)
    {
        if (owner.CurrentAuthority is null)
            return Empty;
        if (!Reference(owner.CurrentAuthority, AuthoritySchema))
            throw new ImmutableContentIntegrityException("The roster authority has another schema.");
        var state = await content.ReadJsonAsync<PlatformOperatorAuthorityContent>(Realm, owner.CurrentAuthority, ct)
            .ConfigureAwait(false);
        RequireAuthority(state, owner.Revision, owner.ActiveCount);
        return state;
    }

    public static async Task<PlatformOperatorMutationContent> ReadChangeAsync(IImmutableContentStore content,
        DomainEvent value, CancellationToken ct = default)
    {
        if (value is not IPlatformOperatorFact fact || fact.Operation is null ||
            !Reference(fact.Change, ChangeSchema) || !Reference(fact.Authority, AuthoritySchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, UnitResult.Schema))
            throw new ImmutableContentIntegrityException("The roster fact has no exact native change and response.");
        var mutation = await content.ReadJsonAsync<PlatformOperatorMutationContent>(Realm, fact.Change, ct).ConfigureAwait(false);
        var state = await content.ReadJsonAsync<PlatformOperatorAuthorityContent>(Realm, fact.Authority, ct).ConfigureAwait(false);
        RequireAuthority(state, fact.Revision, fact.ActiveCount);
        var expected = Fact(mutation, fact.Operation, fact.Change, fact.Authority, fact.FirstResponse!, fact.OperationResult!, state);
        if (ContentDigest.Sha256(expected, expected.GetType()) != ContentDigest.Sha256(value, value.GetType()) ||
            fact.Operation.AuthoredIntentSha256 != IntentDigest(mutation))
            throw new ImmutableContentIntegrityException("The roster native receipt differs from its immutable original change.");
        var response = await FirstResponses.ReplayAsync(content, Realm, fact.FirstResponse!, ct).ConfigureAwait(false);
        if (response.HttpStatus != 200 || response.ContentType != "application/json" || response.ResultSchema != UnitResult.Schema ||
            response.Body is not StandardResponseBody body || body.Content != fact.OperationResult ||
            !Matches(fact.OperationResult!, new UnitResult(), UnitResult.Schema))
            throw new ImmutableContentIntegrityException("The roster receipt has another original Unit result or descriptor.");
        await content.ReadJsonAsync<UnitResult>(Realm, body.Content, ct).ConfigureAwait(false);
        return mutation;
    }

    /// <summary>Reads only the hydrated authority cut; later appends cannot change this historical composition.</summary>
    public static async Task<IReadOnlyList<PlatformOperatorMutationContent>> ReadHistoryAsync(IImmutableContentStore content,
        IEventStore events, PlatformOperatorRoster owner, CancellationToken ct = default)
    {
        var changes = new List<PlatformOperatorMutationContent>();
        if (owner.Revision == 0)
            return changes;
        var state = Empty;
        await foreach (var record in events.ReadAsync(owner.Stream, 0, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (state.Revision == owner.Revision)
                break;
            var mutation = await ReadChangeAsync(content, record.Event, ct).ConfigureAwait(false);
            var evaluated = PlatformOperatorRosterRules.Apply(state, mutation);
            if (!evaluated.IsSuccess || record.Event is not IPlatformOperatorFact fact ||
                !Matches(fact.Authority, evaluated.Value!, AuthoritySchema))
                throw new ImmutableContentIntegrityException("The roster history has an invalid original authority transition.");
            state = evaluated.Value!;
            changes.Add(mutation);
        }
        if (state.Revision != owner.Revision || owner.CurrentAuthority is { } authority && !Matches(authority, state, AuthoritySchema))
            throw new ImmutableContentIntegrityException("The roster history does not reconstruct the selected native authority cut.");
        return changes;
    }

    public static string IntentDigest(PlatformOperatorMutationContent mutation) => AuthoredIntentDigest.Compute(
        mutation with { Actor = mutation.Actor with { Display = string.Empty }, AcceptedAt = default });

    public static DomainEvent Fact(PlatformOperatorMutationContent mutation, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef authority, ImmutableContentRef firstResponse,
        ImmutableContentRef result, PlatformOperatorAuthorityContent after)
    {
        if (operation.Actor != mutation.Actor || operation.AcceptedAt != mutation.AcceptedAt ||
            operation.LogicalId != mutation.CommandId || operation.CommandKind != mutation.CommandKind ||
            operation.OriginalExpectedRevision != mutation.ExpectedRevision)
            throw new InvalidOperationException("The roster change differs from its original provenance.");
        return mutation.CommandKind switch
        {
            nameof(SeedPlatformOperatorRoster) => new PlatformOperatorRosterSeeded(mutation.CommandId, [],
                mutation.OriginalActor, mutation.AcceptedAt)
            {
                Operation = operation,
                Authority = authority,
                Change = change,
                FirstResponse = firstResponse,
                OperationResult = result,
                Revision = after.Revision,
                ActiveCount = after.Operators.Length
            },
            nameof(GrantPlatformOperator) => new PlatformOperatorGranted(mutation.CommandId, mutation.ExpectedRevision!.Value,
                mutation.ActorUserId, mutation.SubjectUserId, mutation.OriginalActor, mutation.AcceptedAt,
                mutation.Reason, mutation.Actor.Display)
            {
                Operation = operation,
                Authority = authority,
                Change = change,
                FirstResponse = firstResponse,
                OperationResult = result,
                Revision = after.Revision,
                ActiveCount = after.Operators.Length
            },
            nameof(RevokePlatformOperator) => new PlatformOperatorRevoked(mutation.CommandId, mutation.ExpectedRevision!.Value,
                mutation.ActorUserId, mutation.SubjectUserId, mutation.AssignmentId, mutation.OriginalActor,
                mutation.AcceptedAt, mutation.Reason, mutation.Actor.Display)
            {
                Operation = operation,
                Authority = authority,
                Change = change,
                FirstResponse = firstResponse,
                OperationResult = result,
                Revision = after.Revision,
                ActiveCount = after.Operators.Length
            },
            _ => throw new InvalidOperationException("Unknown roster operation.")
        };
    }

    public static void RequireAuthority(PlatformOperatorAuthorityContent state, ulong revision, int count)
    {
        if (state is null || state.RosterId != PlatformOperatorRoster.CreationId || state.Revision != revision ||
            state.Operators is null || state.Operators.Length != count || (revision == 0 ? count != 0 : count <= 0) ||
            state.Operators.Any(static item => item is null || item.UserId == Uuid.Empty || item.AssignmentId == Uuid.Empty) ||
            state.Operators.Select(static item => item.UserId).Distinct().Count() != count ||
            !state.Operators.Select(static item => item.UserId).SequenceEqual(
                PlatformOperatorRosterRules.CanonicalUsers(state.Operators.Select(static item => item.UserId))))
            throw new ImmutableContentIntegrityException("The roster authority is not the exact canonical nonempty source state.");
    }

    public static bool Reference(ImmutableContentRef? reference, string schema) => reference is not null &&
        reference.ContentId != Uuid.Empty && reference.Schema == schema && reference.MediaType == "application/json" &&
        reference.Bytes > 0 && reference.Sha256 is { Length: 64 } &&
        reference.Sha256.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull =>
        Reference(reference, schema) && reference.Sha256 == ContentDigest.OfJson(value) &&
        reference.Bytes == JsonSerializer.SerializeToUtf8Bytes(value,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!).Length;
}
