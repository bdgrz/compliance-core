using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Guards and result shaping shared by the auditor request, response, thread and selection command handlers.</summary>
static class AuditorCommands
{
    public static string Digest<T>(T request) where T : notnull => AuthoredIntentDigest.Compute(request);

    public static Result Forbidden(string message) => Result.Failure(new RequestError(RequestErrorKind.Forbidden, message));

    public static Result<T> Forbidden<T>(string message) =>
        Result<T>.Failure(new RequestError(RequestErrorKind.Forbidden, message));

    public static Result Validation(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Validation, message));

    public static Result<T> Validation<T>(string message) =>
        Result<T>.Failure(new RequestError(RequestErrorKind.Validation, message));

    /// <summary>The current terms of a hydrated request, read from verified content.</summary>
    public static async ValueTask<Result<AuditorRequestTermsContent>> TermsAsync(IImmutableContentStore content,
        Uuid tenantId, AuditorRequest request, CancellationToken ct)
    {
        if (request.Terms is not { } terms)
            return Result<AuditorRequestTermsContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The auditor request was not found."));
        try
        {
            var authored = await AuditorContent.ReadAsync<AuditorRequestTermsContent>(content, tenantId, terms, ct)
                .ConfigureAwait(false);
            return authored.ProgramId == request.ProgramId
                ? Result<AuditorRequestTermsContent>.Success(authored)
                : Result<AuditorRequestTermsContent>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The auditor request was not found in this program."));
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<AuditorRequestTermsContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The request terms cannot be verified yet: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<AuditorRequestTermsContent>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The request terms cannot be verified yet: {exception.Message}", false));
        }
    }

    /// <summary>
    ///     Whether the response was admitted under a request that required an independent internal review, read from the
    ///     frozen admission content of its materialization.
    /// </summary>
    public static async ValueTask<Result<bool>> RequiresReviewAsync(IImmutableContentStore content, Uuid tenantId,
        AuditorResponse response, CancellationToken ct)
    {
        if (response.Created is not { } created)
            return Result<bool>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The auditor response was not found."));
        try
        {
            var intent = await AuditorContent.ReadAsync<ChildAdmissionIntent>(content, tenantId, created.Change, ct)
                .ConfigureAwait(false);
            var draft = await AuditorContent.ReadAsync<AuditorResponseDraftContent>(content, tenantId,
                intent.CanonicalAuthoredContent, ct).ConfigureAwait(false);
            return Result<bool>.Success(draft.RequiresInternalReview);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result<bool>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen response admission cannot be verified yet: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<bool>.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The frozen response admission cannot be verified yet: {exception.Message}", false));
        }
    }

    public static ActorReference Actor(Uuid memberId, string display) => ActorReference.ForMember(memberId, display);
}
