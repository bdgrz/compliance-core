using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public static class PersonContent
{
    public const string ViewSchema = "person-values.v1";
    public const string ChangeSchema = "person-native-change.v1";
    public const string RegistrationSchema = "person-registration.v1";
    public static PersonHeader Header(PersonView value) => new(value.TenantId, value.PersonId, value.Revision,
        value.CorrelatedUserId, ValuesSha256(value.DisplayName, value.WorkEmail, value.PersonalContact), value.LastChangedBy, value.LastChangedAt);
    public static string ValuesSha256(string displayName, string? workEmail, PersonalContactDetails? contact) =>
        ContentDigest.OfJson(new PersonValues(displayName, workEmail, contact));
    public static async Task<PersonPrepared> FreezeAsync(IImmutableContentStore store, PersonReadSnapshot before,
        PersonRules after, OperationProvenance operation, CancellationToken ct = default)
    {
        var view = after.Current ?? throw new ImmutableContentIntegrityException("The person mutation has no resulting values.");
        var changed = after.PendingEvents.Count != 0;
        var body = await NativeContent.WriteAsync(store, before.Owner.TenantId, ViewSchema, view, ct).ConfigureAwait(false);
        var mutation = new PersonChangeContent(before.Owner.Header, before.Owner.CurrentContent, Header(view), body, operation.CommandKind, changed);
        var change = await NativeContent.WriteAsync(store, before.Owner.TenantId, ChangeSchema, mutation, ct).ConfigureAwait(false);
        var frozen = operation.CommandKind == nameof(RecordPerson)
            ? await NativeContent.FreezeAsync(store, before.Owner.TenantId, RegistrationSchema, new PersonRegistration(before.Owner.Id), ct).ConfigureAwait(false)
            : await NativeContent.FreezeUnitAsync(store, before.Owner.TenantId, ct).ConfigureAwait(false);
        var prepared = new PersonPrepared(before.Owner.Version, before.Owner.CommittedStreamPosition, before.Owner.ReplayCut.Digest, before.Owner.ReplayCut.MetadataDigest,
            before.Owner.Header, before.Owner.CurrentContent, before.View, view, body, mutation, change, changed, frozen.Response, frozen.Result);
        if (!Verify(prepared, operation))
            throw new ImmutableContentIntegrityException("The person mutation does not match its exact prepared source values.");
        return prepared;
    }
    public static bool Verify(PersonPrepared value, OperationProvenance operation) =>
        Semantic(value, operation) &&
        value.Mutation.Before == value.Before && value.Mutation.BeforeContent == value.BeforeContent &&
        value.Mutation.After == Header(value.View) && value.Mutation.Content == value.Content && value.Mutation.CommandKind == operation.CommandKind &&
        value.Mutation.Changed == value.Changed && Matches(value.Content, value.View, ViewSchema) && Matches(value.Change, value.Mutation, ChangeSchema) &&
        value.View.SourceKind == "manual" && !value.View.RestrictedFieldsRedacted && value.Response.HttpStatus == 200 &&
        value.Response.ResultSchema == (operation.CommandKind == nameof(RecordPerson) ? RegistrationSchema : UnitResult.Schema) &&
        Matches(value.Result, new MutationResultContent(value.Response.ResultSchema, value.Response.Body), "mutation-result-content.v1");
    static bool Semantic(PersonPrepared value, OperationProvenance operation)
    {
        if ((value.BeforeView is null) != (value.Before is null) ||
            value.BeforeView is { } prior && (Header(prior) != value.Before ||
                value.BeforeContent is { } reference && !Matches(reference, prior, ViewSchema)))
            return false;
        var rules = new PersonRules(value.View.TenantId, value.View.PersonId, value.BeforeView);
        switch (operation.CommandKind)
        {
            case nameof(RecordPerson):
                if (value.Before is not null || !rules.Record(value.View.DisplayName, value.View.WorkEmail,
                    value.View.PersonalContact, operation.Actor, operation.AcceptedAt).IsSuccess)
                    return false;
                break;
            case nameof(RevisePerson):
                if (rules.Revise(value.Before?.Revision ?? 0, value.View.DisplayName, value.View.WorkEmail,
                    value.View.PersonalContact, operation.Actor, operation.AcceptedAt) is not null)
                    return false;
                break;
            case nameof(CorrelatePersonMembership):
                if (rules.CorrelateMembership(value.Before?.Revision ?? 0, value.View.CorrelatedUserId,
                    operation.Actor, operation.AcceptedAt) is not null)
                    return false;
                break;
            default:
                return false;
        }
        return rules.Current == value.View && (rules.PendingEvents.Count != 0) == value.Changed;
    }
    public static DomainEvent Fact(PersonPrepared value, OperationProvenance operation, ImmutableContentRef frozen) => operation.CommandKind switch
    {
        nameof(RecordPerson) => new PersonRegistrationRecorded(Header(value.View), value.Content, value.Change, value.Changed, operation, frozen, value.Result),
        nameof(RevisePerson) => new PersonDetailsRevised(Header(value.View), value.Content, value.Change, value.Changed, operation, frozen, value.Result),
        nameof(CorrelatePersonMembership) => new PersonMembershipCorrelationRecorded(Header(value.View), value.Content, value.Change, value.Changed, operation, frozen, value.Result),
        _ => throw new ImmutableContentIntegrityException("Another person native command kind."),
    };
    public static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty &&
        value.Schema == schema && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && value.Bytes >= 0 &&
        value.Sha256 is { Length: 64 } && value.Sha256.All(Uri.IsHexDigit);
    public static bool Matches<T>(ImmutableContentRef reference, T value, string schema) where T : notnull
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeof(T), ComplianceCoreJsonContext.Default);
        return Reference(reference, schema) && reference.Bytes == bytes.LongLength && reference.Sha256 == ContentDigest.Sha256(bytes);
    }
}
