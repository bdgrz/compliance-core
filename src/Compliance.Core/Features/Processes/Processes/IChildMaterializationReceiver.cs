using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
namespace Bdgrz.Compliance.Features.Processes.Processes;

/// <summary>
///     Builds the receiver command of one admitted child kind of <see cref="IndependentChildMaterializationProcess" />.
///     Each owner that admits an independent child registers its own implementation, so the shared process definition
///     never changes when another owner joins it.
/// </summary>
public interface IChildMaterializationReceiver
{
    /// <summary>The receiver name this implementation serves, as listed by the process stage.</summary>
    string Receiver { get; }

    /// <summary>Builds the typed receiver command of the step, or null when the record is not of this child kind.</summary>
    ValueTask<IRequest<ReceiverOutcome>?> CreateAsync(ProcessStep processStep, IServiceProvider services,
        CancellationToken ct);
}
