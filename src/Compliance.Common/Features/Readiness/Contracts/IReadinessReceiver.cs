using Cntryl.Portia;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Readiness.Contracts;

/// <summary>Marks the trusted internal receiver commands of the readiness run and readiness decision processes.</summary>
public interface IReadinessReceiver : IRequestBase;
