using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Writes verified frozen bytes without reserializing a current resource or replacing the response feature.</summary>
public sealed class NativeFirstResponseBytes(int status, string contentType, ReadOnlyMemory<byte> bytes) : IResult, IStatusCodeHttpResult
{
    public int? StatusCode => status;
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = bytes.Length;
        if (bytes.Length != 0)
            await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted).ConfigureAwait(false);
    }
}
