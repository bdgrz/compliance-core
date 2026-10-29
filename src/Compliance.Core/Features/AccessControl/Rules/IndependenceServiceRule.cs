using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

public sealed record IndependenceServiceRule(string ServiceType,
    IndependenceServiceClassification Classification,
    IndependenceServiceClassification ManagementFunctionsClassification);
