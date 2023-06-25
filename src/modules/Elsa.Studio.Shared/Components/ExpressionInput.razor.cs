using BlazorMonaco.Editor;
using Elsa.Api.Client.Expressions;
using Elsa.Api.Client.Models;
using Elsa.Studio.Contracts;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using Humanizer;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ThrottleDebounce;

namespace Elsa.Studio.Components;

public partial class ExpressionInput : IDisposable
{
    private const string DefaultSyntax = "Literal";
    private readonly string[] _uiSyntaxes = { "Literal", "Object" };
    //private string _defaultSyntax = "Literal";
    private string _selectedSyntax = DefaultSyntax;
    private string _monacoLanguage = "";
    private StandaloneCodeEditor? _monacoEditor = default!;
    private bool _isInternalContentChange;
    private bool _isMonacoInitialized;
    private string _monacoEditorId = $"monaco-editor-{Guid.NewGuid()}:N";
    private RateLimitedFunc<ActivityInput, Task> _throttledValueChanged;

    public ExpressionInput()
    {
        _throttledValueChanged = Debouncer.Debounce<ActivityInput, Task>(InvokeValueChangedCallback, TimeSpan.FromMilliseconds(500));
    }

    [Parameter] public DisplayInputEditorContext EditorContext { get; set; } = default!;
    [Parameter] public RenderFragment ChildContent { get; set; } = default!;
    [Inject] private ISyntaxService SyntaxService { get; set; } = default!;
    
    private string UISyntax => EditorContext.UIHintHandler.UISyntax;
    private bool IsUISyntax => _selectedSyntax == UISyntax;
    private string? ButtonIcon => IsUISyntax ? Icons.Material.Filled.MoreVert : default;
    private string? ButtonLabel => IsUISyntax ? default : _selectedSyntax;
    private Variant ButtonVariant => IsUISyntax ? default : Variant.Filled;
    private Color ButtonColor => IsUISyntax ? default : Color.Primary;
    private string? ButtonEndIcon => IsUISyntax ? default : Icons.Material.Filled.KeyboardArrowDown;
    private Color ButtonEndColor => IsUISyntax ? default : Color.Secondary;
    private bool ShowMonacoEditor => !IsUISyntax;
    private string DisplayName => EditorContext.InputDescriptor.DisplayName ?? EditorContext.InputDescriptor.Name;
    private string? Description => EditorContext.InputDescriptor.Description;
    private string InputValue => EditorContext.Value?.Expression.ToString() ?? string.Empty;
    
    private IEnumerable<SyntaxDescriptor> GetSupportedSyntaxes()
    {
        yield return new SyntaxDescriptor(UISyntax, "Default");
        var syntaxes = SyntaxService.ListSyntaxes().Except(_uiSyntaxes);
        
        foreach (var syntax in syntaxes)
            yield return new SyntaxDescriptor(syntax, syntax);
    }

    private async Task UpdateMonacoLanguageAsync(string syntax)
    {
        if (_monacoEditor == null || !_isMonacoInitialized)
            return;

        if (SyntaxService.GetSyntaxProviderByName(syntax) is not IMonacoSyntaxProvider syntaxProvider)
            return;

        var model = await _monacoEditor.GetModel();
        await Global.SetModelLanguage(model, syntaxProvider.Language);
    }


    protected override async Task OnParametersSetAsync()
    {
        _selectedSyntax = UISyntax;
        _monacoLanguage = (EditorContext.SelectedSyntaxProvider as IMonacoSyntaxProvider)?.Language ?? "";

        if (_isMonacoInitialized)
        {
            _isInternalContentChange = true;
            var model = await _monacoEditor!.GetModel();
            await model.SetValue(InputValue);
            _isInternalContentChange = false;
            await Global.SetModelLanguage(model, _monacoLanguage);
        }
    }

    private StandaloneEditorConstructionOptions ConfigureMonacoEditor(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            Language = _monacoLanguage,
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

    private async Task OnSyntaxSelected(string syntax)
    {
        _selectedSyntax = syntax;

        var syntaxProvider = SyntaxService.GetSyntaxProviderByName(_selectedSyntax);
        var value = InputValue;
        var input = EditorContext.Value ?? new ActivityInput();
        input.Expression = syntaxProvider.CreateExpression(value);
        await InvokeValueChangedCallback(input);
        await UpdateMonacoLanguageAsync(syntax);
    }

    private async Task OnMonacoContentChanged(ModelContentChangedEvent e)
    {
        if (_isInternalContentChange)
            return;

        var syntaxProvider = SyntaxService.GetSyntaxProviderByName(_selectedSyntax);
        var value = await _monacoEditor!.GetValue();

        var input = EditorContext.Value ?? new ActivityInput();
        input.Expression = syntaxProvider.CreateExpression(value);
        await ThrottleValueChangedCallback(input);
    }

    private async Task ThrottleValueChangedCallback(ActivityInput input) => await _throttledValueChanged.InvokeAsync(input);
    private async Task InvokeValueChangedCallback(ActivityInput input) => await EditorContext.OnValueChanged(input);

    private void OnMonacoInitialized() => _isMonacoInitialized = true;
    private void OnMonacoDisposed() => _isMonacoInitialized = false;

    public void Dispose() => _throttledValueChanged.Dispose();
}

public record SyntaxDescriptor(string Syntax, string DisplayName);