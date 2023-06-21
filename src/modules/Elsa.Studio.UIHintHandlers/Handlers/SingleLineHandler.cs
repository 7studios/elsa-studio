using Elsa.Studio.Contracts;
using Elsa.Studio.Models;
using Elsa.Studio.UIHintHandlers.Components;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Elsa.Studio.UIHintHandlers.Handlers;

public class SingleLineHandler : IUIHintHandler
{
    public bool GetSupportsUIHint(string uiHint) => uiHint == "single-line";

    public RenderFragment DisplayInputEditor(DisplayInputEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent(0, typeof(SingleLine));
            builder.AddAttribute(1, nameof(SingleLine.EditorContext), context);
            builder.CloseComponent();
        };
    }
}