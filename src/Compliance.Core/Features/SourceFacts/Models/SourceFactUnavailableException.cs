using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.SourceFacts.Services;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.SourceFacts.Models;

/// <summary>The exact native source record is not visible yet; durable source consumers may retry.</summary>
public sealed class SourceFactUnavailableException(string message) : SourceFactVerificationException(message);
