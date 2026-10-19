using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.Artifacts;
using Cntryl.Portia;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Bdgrz.Compliance.Features.Evidence;

/// <summary>Maps the bounded multipart transport for governed evidence capture.</summary>
public static class EvidenceArtifactUploadEndpoint
{
    const int MaximumMetadataLength = 64 * 1024;
    const int MaximumBoundaryLength = 128;
    const int MaximumSectionHeadersLength = 16 * 1024;
    const string UploadRequestHeader = "X-Bdgrz-Upload";
    const long MaximumRequestLength = ArtifactContentLimits.MaxContentLength + MaximumMetadataLength +
        (2L * MaximumSectionHeadersLength) + (4L * MaximumBoundaryLength) + 1024;

    public static RouteHandlerBuilder MapEvidenceArtifactUpload(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/api/v1/tenants/{tenant_id}/evidence-artifacts", UploadAsync)
            .RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser)
            .Accepts<EvidenceArtifactUploadForm>("multipart/form-data")
            .Produces<EvidenceCapture>(StatusCodes.Status200OK)
            .Produces<EvidenceCapture>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status411LengthRequired)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .WithTags("Evidence")
            .WithSummary("Upload a governed evidence artifact")
            .WithDescription("Send `X-Bdgrz-Upload: true` and multipart/form-data with a bounded JSON `metadata` part followed by one `content` file part. " +
                "The content limit is 256 MiB. Uploads are authorized as personal tenant-member actions and enter durable inspection; " +
                "binary upload is not exposed as an MCP tool.");

    static async Task<IResult> UploadAsync(HttpContext context,
        [FromHeader(Name = UploadRequestHeader)] string uploadRequestHeader,
        CaptureEvidenceArtifactHandler handler, TimeProvider time, CancellationToken ct)
    {
        var request = context.Request;
        if (!string.Equals(uploadRequestHeader, "true", StringComparison.Ordinal))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: $"The {UploadRequestHeader} header is required.");

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !string.Equals(contentType.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Evidence upload requires multipart/form-data.");

        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaximumBoundaryLength)
            return BadRequest("The multipart boundary is missing or too long.");
        if (request.ContentLength > MaximumRequestLength)
            return PayloadTooLarge();

        var requestSize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSize is { IsReadOnly: false })
            requestSize.MaxRequestBodySize = MaximumRequestLength;
        else if (request.ContentLength is null)
            return Results.Problem(statusCode: StatusCodes.Status411LengthRequired,
                title: "The server cannot apply a bounded upload limit to this request.");

        if (!Uuid.TryParse(Convert.ToString(request.RouteValues["tenant_id"], CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, out var tenantId) || tenantId == Uuid.Empty)
            return BadRequest("The tenant identifier is invalid.");

        try
        {
            var reader = new MultipartReader(boundary, request.Body)
            {
                HeadersCountLimit = 16,
                HeadersLengthLimit = MaximumSectionHeadersLength,
                BodyLengthLimit = ArtifactContentLimits.MaxContentLength + 1,
            };
            var metadataSection = await reader.ReadNextSectionAsync(ct).ConfigureAwait(false);
            if (metadataSection is null || !IsNamedField(metadataSection, "metadata", requireFileName: false))
                return BadRequest("The first multipart section must be the JSON metadata field.");
            if (!HasJsonContentType(metadataSection))
                return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType,
                    title: "The metadata part must use application/json.");

            var metadataBytes = await ReadMetadataAsync(metadataSection.Body, ct).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize(metadataBytes, ComplianceJsonContext.Default.EvidenceArtifactContent);
            if (metadata is null)
                return BadRequest("The evidence metadata must be a JSON object.");

            var contentSection = await reader.ReadNextSectionAsync(ct).ConfigureAwait(false);
            if (contentSection is null || !IsNamedField(contentSection, "content", requireFileName: true))
                return BadRequest("The second multipart section must be one content file.");

            var invocation = new HttpInvocation(request.Method, request.Path,
                (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText, context.TraceIdentifier);
            var dispatch = new RequestDispatchContext(context.User, invocation, RequestMetadata.Create(), time);
            var command = new CaptureEvidenceArtifact(tenantId, [], metadata);
            var boundedContent = new BoundedMultipartContentStream(contentSection.Body, reader,
                ArtifactContentLimits.MaxContentLength);
            var captured = await handler.HandleUploadAsync(command, boundedContent, dispatch, ct)
                .ConfigureAwait(false);
            if (!captured.IsSuccess)
                return PortiaProblem(captured.Error);
            return captured.Value.State == EvidenceArtifactStates.PendingInspection
                ? Results.Accepted(value: captured.Value)
                : Results.Ok(captured.Value);
        }
        catch (UploadTooLargeException)
        {
            return PayloadTooLarge();
        }
        catch (InvalidDataException)
        {
            return BadRequest("The multipart upload is malformed or contains unexpected fields.");
        }
        catch (JsonException)
        {
            return BadRequest("The evidence metadata is not valid JSON.");
        }
    }

    static async ValueTask<byte[]> ReadMetadataAsync(Stream body, CancellationToken ct)
    {
        using var metadata = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await body.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (metadata.Length + read > MaximumMetadataLength)
                throw new UploadTooLargeException();
            metadata.Write(buffer, 0, read);
        }

        return metadata.ToArray();
    }

    static bool IsNamedField(MultipartSection section, string expectedName, bool requireFileName)
    {
        if (string.IsNullOrEmpty(section.ContentDisposition))
            return false;
        ContentDispositionHeaderValue disposition;
        try
        {
            disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!string.Equals(disposition.DispositionType.Value, "form-data", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(HeaderUtilities.RemoveQuotes(disposition.Name).Value, expectedName,
                StringComparison.Ordinal))
            return false;

        var hasFileName = !string.IsNullOrWhiteSpace(HeaderUtilities.RemoveQuotes(disposition.FileName).Value) ||
            !string.IsNullOrWhiteSpace(HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value);
        return requireFileName == hasFileName;
    }

    static bool HasJsonContentType(MultipartSection section)
    {
        if (section.Headers is not { } headers || !headers.TryGetValue(HeaderNames.ContentType, out var value) ||
            !MediaTypeHeaderValue.TryParse(value.ToString(), out var mediaType) || mediaType is null)
            return false;
        return string.Equals(mediaType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    static IResult PortiaProblem(RequestError error)
    {
        var statusCode = error.Kind switch
        {
            RequestErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            RequestErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            RequestErrorKind.NotFound => StatusCodes.Status404NotFound,
            RequestErrorKind.Conflict => StatusCodes.Status409Conflict,
            RequestErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Problem(statusCode: statusCode, title: "Evidence upload was rejected", detail: error.Message);
    }

    static IResult BadRequest(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid evidence upload", detail: detail);

    static IResult PayloadTooLarge() =>
        Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Evidence content may not exceed 256 MiB.");

    public sealed record EvidenceArtifactUploadForm(string Metadata, IFormFile Content);

    sealed class UploadTooLargeException : Exception;

    sealed class BoundedMultipartContentStream(Stream content, MultipartReader reader, long maximumLength) : Stream
    {
        readonly byte[] _probe = new byte[1];
        long _read;
        bool _completed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_completed || buffer.IsEmpty)
                return 0;

            if (_read == maximumLength)
            {
                if (await content.ReadAsync(_probe, cancellationToken).ConfigureAwait(false) != 0)
                    throw new UploadTooLargeException();
                await EnsureFinalSectionAsync(cancellationToken).ConfigureAwait(false);
                _completed = true;
                return 0;
            }

            var allowed = (int)Math.Min(buffer.Length, maximumLength - _read);
            var read = await content.ReadAsync(buffer[..allowed], cancellationToken).ConfigureAwait(false);
            _read += read;
            if (read == 0)
            {
                await EnsureFinalSectionAsync(cancellationToken).ConfigureAwait(false);
                _completed = true;
            }

            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        async ValueTask EnsureFinalSectionAsync(CancellationToken ct)
        {
            if (await reader.ReadNextSectionAsync(ct).ConfigureAwait(false) is not null)
                throw new InvalidDataException("Only metadata and one content section are accepted.");
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
