using Elsa.Api.Client.Models;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Models;

namespace Elsa.Studio.Workflows.Contracts;

public interface IWorkflowDefinitionService
{
    Task<ListWorkflowDefinitionsResponse> ListAsync(ListWorkflowDefinitionsRequest request, VersionOptions versionOptions, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> FindByDefinitionIdAsync(string definitionId, VersionOptions? versionOptions = default, bool includeCompositeRoot = false, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition> SaveAsync(SaveWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<bool> GetIsNameUniqueAsync(string name, CancellationToken cancellationToken = default);
    Task<string> GenerateUniqueNameAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinition> CreateNewWorkflowDefinitionAsync(string name, string? description = default, CancellationToken cancellationToken = default);
}