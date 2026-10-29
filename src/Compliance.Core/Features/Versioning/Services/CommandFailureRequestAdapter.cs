using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Services;
namespace Bdgrz.Compliance.Features.Versioning.Services;

static class CommandFailureRequestAdapter
{
    public static AggregateOutcome ToOutcome(CommandFailure? failure) =>
        failure is null
            ? AggregateOutcome.Commit(Result.Success)
            : AggregateOutcome.Discard(Result.Failure(ToRequestError(failure)));

    public static AggregateOutcome<T> ToOutcome<T>(CommandFailure? failure, T value) =>
        failure is null
            ? AggregateOutcome.Commit(Result<T>.Success(value))
            : AggregateOutcome.Discard(Result<T>.Failure(ToRequestError(failure)));

    internal static RequestError ToRequestError(this CommandFailure failure) => failure.Code switch
    {
        CommandFailureCode.MissingRecord => new(RequestErrorKind.NotFound, failure.Message!),
        CommandFailureCode.StateConflict => new(RequestErrorKind.Conflict, failure.Message!),
        CommandFailureCode.InvalidContent => new(RequestErrorKind.Validation, failure.Message!),
        CommandFailureCode.ActorProhibited => new(RequestErrorKind.Forbidden, failure.Message!),
        CommandFailureCode.VersionConflict => failure.Version!.ToRequestError(),
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}
