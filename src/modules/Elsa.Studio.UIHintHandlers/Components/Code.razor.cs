using BlazorMonaco.Editor;
using Elsa.Api.Client.Expressions;
using Elsa.Studio.Contracts;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using Microsoft.AspNetCore.Components;
using ThrottleDebounce;

namespace Elsa.Studio.UIHintHandlers.Components;

public partial class Code
{
    private readonly string _monacoEditorId = $"monaco-editor-{Guid.NewGuid()}:N";
    private StandaloneCodeEditor? _monacoEditor;
    private string? _lastMonacoEditorContent;
    private readonly RateLimitedFunc<Task> _throttledValueChanged;

    public Code()
    {
        _throttledValueChanged = Debouncer.Debounce<Task>(InvokeValueChangedCallback, TimeSpan.FromMilliseconds(500));
    }

    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = default!;
    [Parameter] public RenderFragment ChildContent { get; set; } = default!;
    [Inject] private ISyntaxService SyntaxService { get; set; } = default!;

    private string InputValue => EditorContext.Value?.Expression.ToString() ?? string.Empty;


    private StandaloneEditorConstructionOptions ConfigureMonacoEditor(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = "javascript",
            Value = InputValue,
            FontFamily = "Roboto Mono, monospace",
            RenderLineHighlight = "none",
            Minimap = new EditorMinimapOptions
            {
                Enabled = false
            },
            AutomaticLayout = true,
            LineNumbers = "on",
            Theme = "vs",
            RoundedSelection = true,
            ScrollBeyondLastLine = false,
            ReadOnly = false,
            OverviewRulerLanes = 0,
            OverviewRulerBorder = false,
            LineDecorationsWidth = 0,
            HideCursorInOverviewRuler = true,
            GlyphMargin = false
        };
    }

    private async Task OnMonacoContentChanged(ModelContentChangedEvent e)
    {
        await _throttledValueChanged.InvokeAsync();
    }

    private async Task InvokeValueChangedCallback()
    {
        var value = await _monacoEditor!.GetValue();

        // This event gets fired even when the content hasn't changed, but for example when the containing pane is resized.
        // This happens from within the monaco editor itself (or the Blazor wrapper, not sure).
        if (value == _lastMonacoEditorContent)
            return;

        _lastMonacoEditorContent = value;
        var expression = new LiteralExpression(value);
        await EditorContext.UpdateExpressionAsync(expression);
    }

    public void Dispose() => _throttledValueChanged.Dispose();
}