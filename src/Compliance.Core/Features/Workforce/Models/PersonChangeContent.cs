using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Workforce.Models;

/// <summary>Exact producing native transition and prior values; historical bodies are never selected from live current state.</summary>
public sealed record PersonChangeContent(PersonHeader? Before, ImmutableContentRef? BeforeContent,
    PersonHeader After, ImmutableContentRef Content, string CommandKind, bool Changed);
