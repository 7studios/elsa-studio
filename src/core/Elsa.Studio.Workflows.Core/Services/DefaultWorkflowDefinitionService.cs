using Elsa.Api.Client.Activities;
using Elsa.Api.Client.Models;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Contracts;
using Elsa.Api.Client.Resources.WorkflowDefinitions.Models;
using Elsa.Studio.Backend.Contracts;
using Elsa.Studio.Backend.Extensions;
using Elsa.Studio.Workflows.Contracts;

namespace Elsa.Studio.Workflows.Services;

public class DefaultWorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IBackendConnectionProvider _backendConnectionProvider;

    public DefaultWorkflowDefinitionService(IBackendConnectionProvider backendConnectionProvider)
    {
        _backendConnectionProvider = backendConnectionProvider;
    }

    public async Task<ListWorkflowDefinitionsResponse> ListAsync(ListWorkflowDefinitionsRequest request, VersionOptions versionOptions, CancellationToken cancellationToken = default)
    {
        return await _backendConnectionProvider
            .GetApi<IWorkflowDefinitionsApi>()
            .ListAsync(request, versionOptions, cancellationToken);
    }

    public async Task<WorkflowDefinition?> FindByDefinitionIdAsync(string definitionId, VersionOptions? versionOptions = default, bool includeCompositeRoot = false, CancellationToken cancellationToken = default)
    {
        return await _backendConnectionProvider
            .GetApi<IWorkflowDefinitionsApi>()
            .GetAsync(definitionId, versionOptions, includeCompositeRoot, cancellationToken);
    }

    public async Task<WorkflowDefinition> SaveAsync(SaveWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        return await _backendConnectionProvider
            .GetApi<IWorkflowDefinitionsApi>()
            .SaveAsync(request, cancellationToken);
    }

    public async Task<bool> GetIsNameUniqueAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await _backendConnectionProvider
            .GetApi<IWorkflowDefinitionsApi>()
            .GetIsNameUniqueAsync(name, cancellationToken);
        
        return response.IsUnique;
    }

    public async Task<string> GenerateUniqueNameAsync(CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 100;
        var attempt = 0;
        
        while(attempt < maxAttempts)
        {
            var name = $"Workflow {++attempt}";
            var isUnique = await GetIsNameUniqueAsync(name, cancellationToken);
            
            if(isUnique)
                return name;
        }
        
        throw new Exception($"Failed to generate a unique workflow name after {maxAttempts} attempts.");
    }

    public async Task<WorkflowDefinition> CreateNewWorkflowDefinitionAsync(string name, string? description = default, CancellationToken cancellationToken = default)
    {
        var saveRequest = new SaveWorkflowDefinitionRequest
        {
            Model = new WorkflowDefinitionModel
            {
                Name = name,
                Description = description,
                Version = 1,
                IsLatest = true,
                IsPublished = false,
                Root = new Activity
                {
                    Type = "Elsa.Flowchart",
                    Id = "Flowchart1",
                    Version = 1
                }
            }
        };
        
        return await SaveAsync(saveRequest, cancellationToken);
    }
}