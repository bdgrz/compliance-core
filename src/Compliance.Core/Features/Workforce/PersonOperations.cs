using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>All three identified producers share native acceptance before fresh values, canonical correlation or candidate publication.</summary>
public sealed class PersonOperations(NativeOperationExecutor operations, PersonReader people,
    IAggregateReader reader, IImmutableContentStore content, TimeProvider clock)
{
    public async ValueTask<Result<PersonRegistration>> RecordAsync(IRequestContext<RecordPerson> context, CancellationToken ct)
    {
        var request = context.Request;
        var accepted = await ExecuteAsync(context, request.PersonId, request.PersonId, null, nameof(RecordPerson),
            (rules, operation, _) =>
            {
                var result = rules.Record(request.DisplayName, request.WorkEmail, request.PersonalContact, operation.Actor, operation.AcceptedAt);
                return ValueTask.FromResult(result.IsSuccess ? Result.Success : Result.Failure(result.Error!));
            }, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<PersonRegistration>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
    }
    public async ValueTask<Result<UnitResult>> ReviseAsync(IRequestContext<RevisePerson> context, CancellationToken ct)
    {
        var request = context.Request;
        var accepted = await ExecuteAsync(context, request.CommandId, request.PersonId, request.ExpectedRevision, nameof(RevisePerson),
            (rules, operation, _) => ValueTask.FromResult(ToResult(rules.Revise(request.ExpectedRevision, request.DisplayName,
                request.WorkEmail, request.PersonalContact, operation.Actor, operation.AcceptedAt))), ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<UnitResult>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
    }
    public async ValueTask<Result<UnitResult>> CorrelateAsync(IRequestContext<CorrelatePersonMembership> context, CancellationToken ct)
    {
        var request = context.Request;
        var accepted = await ExecuteAsync(context, request.CommandId, request.PersonId, request.ExpectedRevision, nameof(CorrelatePersonMembership),
            async (rules, operation, token) =>
            {
                if (request.UserId is { } user)
                {
                    if (user == Uuid.Empty)
                        return Result.Failure(new RequestError(RequestErrorKind.Validation, "A correlated platform member ID cannot be empty."));
                    var member = await reader.HydrateAsync(new Member(request.TenantId, user), token).ConfigureAwait(false);
                    if (!member.IsRegistered || member.UserId != user)
                        return Result.Failure(new RequestError(RequestErrorKind.Validation, "The platform member was not found in the tenant."));
                    if (member.Affiliation == "firm_staff")
                        return Result.Failure(new RequestError(RequestErrorKind.Validation, "Firm staff are not tenant workforce and cannot be correlated to the roster."));
                }
                return ToResult(rules.CorrelateMembership(request.ExpectedRevision, request.UserId, operation.Actor, operation.AcceptedAt));
            }, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<UnitResult>(content, request.TenantId, accepted, ct).ConfigureAwait(false);
    }
    async ValueTask<Result<AcceptedAppendResult>> ExecuteAsync<T>(IRequestContext<T> context, Uuid logical, Uuid personId,
        long? expectedRevision, string command, Func<PersonRules, OperationProvenance, CancellationToken, ValueTask<Result>> mutate,
        CancellationToken ct) where T : IRequestBase, IWorkforceRequest
    {
        var request = context.Request;
        if (request.TenantId == Uuid.Empty || personId == Uuid.Empty || logical == Uuid.Empty || expectedRevision is < 0)
            return Result<AcceptedAppendResult>.Failure(new RequestError(RequestErrorKind.Validation,
                "A person mutation requires tenant, person, logical identity and a nonnegative expected revision."));
        var actor = WorkforceActor.From(context);
        var stream = new Person(request.TenantId, personId).Stream;
        var operation = RootOperation.Provenance(logical, command, stream, actor, expectedRevision,
            AuthoredIntentDigest.Compute(request), clock.GetUtcNow()) with
        { FirstOwner = stream };
        var binding = AcceptedOperationBinding.Of(operation);
        PersonPrepared? prepared = null;
        return await operations.ExecuteAsync(new NativeOperation<Person>(binding,
            () => new Person(request.TenantId, personId, logical), value => value.Acceptance,
            async (owner, token) =>
            {
                if (command == nameof(RecordPerson) && owner.IsCreated)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Conflict,
                        "The person already exists; a legacy write is not an identified creation receipt."));
                var snapshot = await people.ReadAsync(owner, token).ConfigureAwait(false);
                var rules = snapshot.Rules;
                var changed = await mutate(rules, operation, token).ConfigureAwait(false);
                if (!changed.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(changed.Error!);
                prepared = await PersonContent.FreezeAsync(content, snapshot, rules, operation, token).ConfigureAwait(false);
                return Result<FirstResponseDescriptor>.Success(prepared.Response);
            }, (Person owner, ImmutableContentRef frozen) => owner.Append(prepared!, operation, frozen)), context, ct).ConfigureAwait(false);
    }
    static Result ToResult(CommandFailure? failure) => failure is null ? Result.Success : Result.Failure(failure.ToRequestError());
}
