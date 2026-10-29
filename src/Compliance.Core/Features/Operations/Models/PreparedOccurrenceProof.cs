using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Projections;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>The actor, performer, snapshot, current occurrence view and plan an occurrence proof is decided on.</summary>
sealed record PreparedOccurrenceProof(OperationsActor Actor, OperatingHolder Performer,
    IReadOnlyDictionary<Uuid, DateOnly?> Windows, ControlOperationsSnapshot Snapshot,
    ControlOccurrenceView Current, ControlOperatingPlanView Plan);
