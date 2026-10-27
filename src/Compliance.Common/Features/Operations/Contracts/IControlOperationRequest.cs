using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Operations.Contracts;

/// <summary>
///     A program request that any member with program read access may submit. The handler and
///     aggregate then require the actor to hold the operating responsibility or manage the program.
/// </summary>
public interface IControlOperationRequest : IProgramScopedRequest
{
    string IProgramScopedRequest.RequiredPermission => IProgramReadRequest.ReadPermission;
}
