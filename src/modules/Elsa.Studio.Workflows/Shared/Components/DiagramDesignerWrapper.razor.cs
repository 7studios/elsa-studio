using System.Text.Json.Nodes;
using Elsa.Api.Client.Extensions;
using Elsa.Api.Client.Resources.ActivityDescriptors.Enums;
using Elsa.Studio.Contracts;
using Elsa.Studio.Workflows.Args;
using Elsa.Studio.Workflows.Designer;
using Elsa.Studio.Workflows.Domain.Contracts;
using Elsa.Studio.Workflows.Models;
using Elsa.Studio.Workflows.UI.Contracts;
using Humanizer;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Elsa.Studio.Workflows.Shared.Components;

public partial class DiagramDesignerWrapper
{
    private IDiagramDesigner? _diagramDesigner;
    private Stack<ActivityPathSegment> _pathSegments = new();
    private List<BreadcrumbItem> _breadcrumbItems = new();

    [Parameter] public JsonObject Activity { get; set; } = default!;
    [Parameter] public bool IsReadOnly { get; set; }
    [Parameter] public RenderFragment CustomToolbarItems { get; set; } = default!;
    [Parameter] public bool IsProgressing { get; set; }
    [Parameter] public Func<JsonObject, Task>? ActivitySelected { get; set; }
    [Parameter] public Func<Task>? GraphUpdated { get; set; }
    [Inject] private IDiagramDesignerService DiagramDesignerService { get; set; } = default!;
    [Inject] private IActivityDisplaySettingsRegistry ActivityDisplaySettingsRegistry { get; set; } = default!;
    [Inject] private IActivityRegistry ActivityRegistry { get; set; } = default!;
    [Inject] private IActivityIdGenerator ActivityIdGenerator { get; set; } = default!;

    private ActivityPathSegment? CurrentPathSegment => _pathSegments.TryPeek(out var segment) ? segment : default;

    public Task<JsonObject> ReadActivityAsync()
    {
        return Task.FromResult(Activity);
    }

    public async Task LoadActivityAsync(JsonObject activity)
    {
        await _diagramDesigner!.LoadRootActivityAsync(activity);
        await UpdatePathSegmentsAsync(segments => segments.Clear());
    }

    public Task UpdateActivityAsync(string activityId, JsonObject activity)
    {
        return _diagramDesigner!.UpdateActivityAsync(activityId, activity);
    }

    protected override async Task OnInitializedAsync()
    {
        _diagramDesigner = DiagramDesignerService.GetDiagramDesigner(Activity);
        await UpdatePathSegmentsAsync(segments => segments.Clear());
    }

    private IEnumerable<GraphSegment> ResolvePath()
    {
        var currentContainer = Activity;

        foreach (var pathSegment in _pathSegments.Reverse())
        {
            var flowchart = currentContainer;
            var activities = flowchart.GetActivities();
            var currentActivity = activities.First(x => x.GetId() == pathSegment.ActivityId);
            var propName = pathSegment.PortName.Camelize();
            currentContainer = currentActivity.GetProperty(propName)!.AsObject();

            yield return new GraphSegment(currentActivity, pathSegment.PortName, currentContainer);
        }
    }

    private async Task UpdatePathSegmentsAsync(Action<Stack<ActivityPathSegment>> action)
    {
        action(_pathSegments);
        await UpdateBreadcrumbItemsAsync();
    }

    private async Task UpdateBreadcrumbItemsAsync()
    {
        _breadcrumbItems = (await GetBreadcrumbItems()).ToList();
        StateHasChanged();
    }

    private async Task<IEnumerable<BreadcrumbItem>> GetBreadcrumbItems()
    {
        var breadcrumbItems = new List<BreadcrumbItem>();

        if (_pathSegments.Any())
            breadcrumbItems.Add(new BreadcrumbItem("Root", "#_root_", false, Icons.Material.Outlined.Home));

        var resolvedPath = ResolvePath().ToList();

        foreach (var segment in resolvedPath)
        {
            var currentActivity = segment.Activity;
            var activityTypeName = currentActivity.GetTypeName();
            var activityDescriptor = (await ActivityRegistry.FindAsync(activityTypeName))!;
            var embeddedPortCount = activityDescriptor.Ports.Count(x => x.Type == PortType.Embedded);
            var displaySettings = ActivityDisplaySettingsRegistry.GetSettings(activityTypeName);
            var disabled = segment == resolvedPath.Last();
            var displayText = currentActivity.GetName() ?? activityDescriptor.DisplayName;
            var activityBreadcrumbItem = new BreadcrumbItem(displayText, $"#{currentActivity.GetId()}", disabled, displaySettings.Icon);

            breadcrumbItems.Add(activityBreadcrumbItem);

            if (embeddedPortCount <= 1)
                continue;

            var embeddedPort = activityDescriptor.Ports.First(x => x.Name == segment.PortName);
            var embeddedPortBreadcrumbItem = new BreadcrumbItem(embeddedPort.DisplayName, "#", true);
            breadcrumbItems.Add(embeddedPortBreadcrumbItem);
        }

        return breadcrumbItems;
    }

    private JsonObject? GetCurrentActivity()
    {
        var resolvedPath = ResolvePath().LastOrDefault();
        return resolvedPath?.Activity;
    }

    private JsonObject GetCurrentContainerActivity()
    {
        var resolvedPath = ResolvePath().LastOrDefault();
        return resolvedPath?.EmbeddedActivity ?? Activity;
    }

    private async Task DisplayCurrentSegmentAsync()
    {
        var currentContainerActivity = GetCurrentContainerActivity();

        if (_diagramDesigner == null)
            return;

        await _diagramDesigner.LoadRootActivityAsync(currentContainerActivity);
    }

    private async Task OnActivityEmbeddedPortSelected(ActivityEmbeddedPortSelectedArgs args)
    {
        var activity = args.Activity;
        var embeddedActivity = activity.GetProperty(args.PortName)?.AsObject();

        if (embeddedActivity != null)
        {
            // If the embedded activity has no designer support, then open it in the activity properties editor by raising the ActivitySelected event.
            if (embeddedActivity.GetTypeName() != "Elsa.Flowchart")
            {
                ActivitySelected?.Invoke(embeddedActivity);
                return;
            }
        }
        else
        {
            // Create a flowchart and embed it into the activity.
            embeddedActivity = new JsonObject(new Dictionary<string, JsonNode?>
            {
                ["id"] = ActivityIdGenerator.GenerateId(),
                ["type"] = "Elsa.Flowchart",
                ["version"] = 1,
                ["name"] = "Flowchart1",
            });
            var propName = args.PortName.Camelize();
            activity[propName] = embeddedActivity;

            // Update the graph in the designer.
            await _diagramDesigner!.UpdateActivityAsync(activity.GetId(), activity);
        }

        // Create a new path segment of the container activity and push it onto the stack.
        var segment = new ActivityPathSegment(activity.GetId(), activity.GetTypeName(), args.PortName);

        await UpdatePathSegmentsAsync(segments => segments.Push(segment));
        await DisplayCurrentSegmentAsync();
    }

    private async Task OnGraphUpdated()
    {
        var rootActivity = await _diagramDesigner!.ReadRootActivityAsync();
        var currentActivity = GetCurrentActivity();
        var currentSegment = CurrentPathSegment;

        if (currentActivity == null || currentSegment == null)
            Activity = rootActivity;
        else
        {
            var propName = currentSegment.PortName.Camelize();
            currentActivity[propName] = rootActivity;
        }

        if (GraphUpdated != null)
            await GraphUpdated();
    }

    private async Task OnBreadcrumbItemClicked(BreadcrumbItem item)
    {
        if (item.Href == "#_root_")
        {
            await UpdatePathSegmentsAsync(segments => segments.Clear());
            await DisplayCurrentSegmentAsync();
            return;
        }

        var activityId = item.Href[1..];

        await UpdatePathSegmentsAsync(segments =>
        {
            while (segments.TryPeek(out var segment))
                if (segment.ActivityId == activityId)
                    break;
                else
                    segments.Pop();
        });

        await DisplayCurrentSegmentAsync();
    }
}