using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Explicit HTTP intent for existing personal import handler fixtures only.</summary>
sealed class PersonalApplicationImportContext<T>(T request, ClaimsPrincipal actor) : IRequestContext<T>
    where T : IRequestBase
{
    readonly RequestContext<T> _context = new(request, actor);
    public T Request => _context.Request;
    public ClaimsPrincipal Actor => _context.Actor;
    public Uuid ExecutionId => _context.ExecutionId;
    public Uuid RequestId => _context.RequestId;
    public Uuid CorrelationId => _context.CorrelationId;
    public Uuid? CausationId => _context.CausationId;
    public Uuid CauseId => _context.CauseId;
    public DateTimeOffset StartedAt => _context.StartedAt;
    public RequestInvocation Invocation => new HttpInvocation("POST", "/synthetic/import", "/synthetic/import", "synthetic");
}
