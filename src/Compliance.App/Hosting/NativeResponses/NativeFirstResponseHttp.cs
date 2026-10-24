using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;
using Microsoft.Net.Http.Headers;

namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Reads the exact immutable standard response only after a matching successful native root admission.</summary>
public static class NativeFirstResponseHttp
{
    public static async ValueTask<IResult?> ReadAsync(HttpContext http, CancellationToken ct)
    {
        if (http.Features.Get<NativeFirstResponseCapture>() is not { } capture)
            return null;
        try
        {
            if (capture.Ambiguous || capture.FirstResponse.Schema != FirstResponses.Schema)
                throw new ImmutableContentIntegrityException("The HTTP request has no single exact native first response.");
            var content = http.RequestServices.GetRequiredService<IImmutableContentStore>();
            // Re-read the accepted descriptor before any headers/body are emitted, including first writes.
            var descriptor = await FirstResponses.ReplayAsync(content, capture.Binding.Realm, capture.FirstResponse, ct).ConfigureAwait(false);
            if (descriptor != capture.Response || descriptor.HttpStatus is < 200 or >= 300 ||
                !MediaTypeHeaderValue.TryParse(descriptor.ContentType, out _) || descriptor.Body is not StandardResponseBody body ||
                body.Content is null || body.Content.Schema != descriptor.ResultSchema || body.Content.MediaType != descriptor.ContentType ||
                descriptor.HttpStatus == StatusCodes.Status204NoContent && body.Content.Bytes != 0)
                throw new ImmutableContentIntegrityException("The native original response has no qualified standard HTTP representation.");
            var bytes = await content.ReadVerifiedAsync(capture.Binding.Realm, body.Content, ct).ConfigureAwait(false);
            return new NativeFirstResponseBytes(descriptor.HttpStatus, descriptor.ContentType, bytes);
        }
        catch (ImmutableContentIntegrityException error)
        {
            http.Response.Headers.Remove(HeaderNames.Location);
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The original native response cannot be verified.", error is ImmutableContentUnavailableException)).ToHttpResult();
        }
    }
}
