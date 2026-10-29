using Cntryl.Portia;
using Bdgrz.Compliance.Features.Exports.Commands;
namespace Bdgrz.Compliance.Features.AccessControl.Contracts;

/// <summary>Platform-authored draft rules; never client business-record authority or firm ratification.</summary>
public interface IIndependenceRuleAdministrationRequest : IRequestBase;
