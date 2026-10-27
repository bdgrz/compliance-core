using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Processes;

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
