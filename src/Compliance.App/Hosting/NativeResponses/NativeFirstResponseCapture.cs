using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>One request-local original response; ambiguity fails closed instead of choosing a later result.</summary>
public sealed record NativeFirstResponseCapture(AcceptedOperationBinding Binding, NativeFactReference Fact,
    ImmutableContentRef FirstResponse, FirstResponseDescriptor Response, bool Ambiguous = false);
