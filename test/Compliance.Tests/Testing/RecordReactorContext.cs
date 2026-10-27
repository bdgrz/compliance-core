using System.Security.Claims;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>A reaction context for an event read back from a store, as the system principal.</summary>
class RecordReactorContext(DomainEventRecord source) : IReactorContext
{
    public DomainEventRecord Source => source;

    public ClaimsPrincipal Actor => RequestActor.System;

    public Uuid ExecutionId { get; } = Uuid.CreateVersion4();

    public Uuid CorrelationId { get; } = Uuid.CreateVersion4();

    public Uuid CauseId => source.Event.Metadata.EventId;

    public DateTimeOffset StartedAt => new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
}
