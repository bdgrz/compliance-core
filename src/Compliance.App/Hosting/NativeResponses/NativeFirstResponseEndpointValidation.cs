using Cntryl.Portia;
using Microsoft.AspNetCore.Routing;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Rejects a generated mutation endpoint that omits the native transport contract marker.</summary>
public sealed class NativeFirstResponseEndpointValidation(IEnumerable<EndpointDataSource> sources) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Validate(sources.SelectMany(static source => source.Endpoints));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public static void Validate(IEnumerable<Endpoint> endpoints)
    {
        foreach (var endpoint in endpoints)
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (endpoint.Metadata.GetMetadata<PortiaOpenApiOperation>() is null || methods is null ||
                !methods.Any(static method => method is "POST" or "PUT" or "PATCH" or "DELETE"))
                continue;
            if (endpoint.Metadata.GetMetadata<NativeFirstResponseEndpoint>() is null)
                throw new InvalidOperationException("Every generated mutation endpoint must install its native first-response result contract: " + endpoint.DisplayName);
        }
    }
}
