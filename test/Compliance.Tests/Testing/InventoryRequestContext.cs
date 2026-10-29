using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>A direct request context whose logical id is the request's own identity when it has one.</summary>
sealed class InventoryRequestContext<TRequest>(TRequest request, ClaimsPrincipal actor)
    : IRequestContext<TRequest> where TRequest : notnull
{
    readonly Uuid _requestId = request is IIdentifiedRequest identified ? identified.RequestId : Uuid.CreateVersion4();

    public TRequest Request => request;
    public ClaimsPrincipal Actor => actor;
    public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
    public Uuid RequestId => _requestId;
    public Uuid CorrelationId => _requestId;
    public Uuid? CausationId => null;
    public Uuid CauseId => _requestId;
    public DateTimeOffset StartedAt { get; } = ApplicationInventoryHost.Now;
    public RequestInvocation Invocation { get; } = new DirectInvocation();
}
