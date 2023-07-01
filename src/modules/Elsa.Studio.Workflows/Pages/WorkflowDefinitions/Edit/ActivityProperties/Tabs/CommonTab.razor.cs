using Elsa.Api.Client.Activities;
using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.ActivityDescriptors.Models;
using Microsoft.AspNetCore.Components;

namespace Elsa.Studio.Workflows.Pages.WorkflowDefinitions.Edit.ActivityProperties.Tabs;

public partial class CommonTab
{
    [Parameter] public Activity? Activity { get; set; }
    [Parameter] public ActivityDescriptor? ActivityDescriptor { get; set; }
    [Parameter] public Func<Activity, Task>? OnActivityUpdated { get; set; }
    
    private bool IsTrigger => ActivityDescriptor?.Kind == ActivityKind.Trigger;
    
    private async Task OnActivityIdChanged(string value)
    {
        Activity!.Id = value;
        await RaiseActivityUpdated();
    }
    
    private async Task RaiseActivityUpdated()
    {
        if (OnActivityUpdated != null)
            await OnActivityUpdated(Activity!);
    }
    
    private async Task OnActivityDisplayTextChanged(string value)
    {
        Activity!.SetDisplayText(value);
        await RaiseActivityUpdated();
    }
    
    private async Task OnActivityDescriptionChanged(string value)
    {
        Activity!.SetDescription(value);
        await RaiseActivityUpdated();
    }
    
    private async Task OnShowDescriptionChanged(bool? value)
    {
        Activity!.SetShowDescription(value == true);
        await RaiseActivityUpdated();
    }
    
    private async Task OnCanStartWorkflowChanged(bool? value)
    {
        Activity!.SetCanStartWorkflow(value == true);
        await RaiseActivityUpdated();
    }
}