using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace ComboBox.Components;

public partial class Combobox<TItem> : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime? Js { get; set; }

    /// <summary>
    /// The current selected value of the component.
    /// </summary>
    [Parameter]
    public TItem? Value { get; set; }

    /// <summary>
    /// Callback invoked when the selected value changes.
    /// </summary>
    [Parameter]
    public EventCallback<TItem?> ValueChanged { get; set; }

    /// <summary>
    /// Placeholder text displayed when no value is selected.
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "";

    /// <summary>
    /// Function to convert an item of type <typeparamref name="TItem"/> to its string representation.
    /// </summary>
    [Parameter]
    public Func<TItem, string>? ToStringFunc { get; set; }

    /// <summary>
    /// Determines whether a checkmark is shown next to selected items in the list.
    /// </summary>
    [Parameter]
    public bool ShowCheckmarkInList { get; set; } = true;

    /// <summary>
    /// Optional provider for asynchronous data loading, taking a single <see cref="ComboState"/> object and a <see cref="CancellationToken"/>.
    /// </summary>
    [Parameter]
    public Func<ComboState, CancellationToken, ValueTask<ItemsProviderResult<TItem>>>? DataProvider { get; set; }

    /// <summary>
    /// A static list of items to display when using preloaded data instead of a data provider.
    /// </summary>
    [Parameter]
    public List<TItem>? StaticData { get; set; }

    /// <summary>
    /// Template for rendering each item in the dropdown list.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>
    /// Template for rendering the selected item when the dropdown is closed.
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? SelectedTemplate { get; set; }

    /// <summary>
    /// Template displayed when no matching items are found.
    /// </summary>
    [Parameter]
    public RenderFragment? NoResultsTemplate { get; set; }

    /// <summary>
    /// If set to <c>true</c>, the component is disabled and user interaction is prevented.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// CSS class applied to the root element of the component.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// CSS class applied to the input element.
    /// </summary>
    [Parameter]
    public string? InputClass { get; set; }

    /// <summary>
    /// CSS class applied to the dropdown list container.
    /// </summary>
    [Parameter]
    public string? DropdownClass { get; set; }

    /// <summary>
    /// Label text displayed for the component, if applicable.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>
    /// The fixed pixel height of each item in the virtualized list.
    /// </summary>
    [Parameter]
    public float ItemSize { get; set; } = 50f;


    private string Classes => new ClassBuilder()
        .AddClass("combo-container")
        .AddClass("combo-disabled", Disabled)
        .AddClass(Class)
        .Build();

    private string InputClasses => new ClassBuilder()
        .AddClass("combo-trigger")
        .AddClass(InputClass)
        .Build();

    private string DropdownClasses => new ClassBuilder()
        .AddClass("combo-dropdown")
        .AddClass(DropdownClass)
        .Build();

    private ElementReference? _rootRef;
    private bool IsLoading { get; set; }
    private IJSObjectReference? _outsideClickListener;
    private DotNetObjectReference<Combobox<TItem>>? _dotNetObjectReference;
    private CancellationTokenSource? _debounceCts;
    private string? SearchText { get; set; }
    private Virtualize<TItem>? _virtualizeRef;
    private bool IsOpen { get; set; }
    private ElementReference _elementReference;
    private List<TItem> _visibleItems = [];
    private int _highlightedIndex;
    private bool ShowNoResults => _visibleItems.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);

    private bool IsSelectedItem(TItem item)
    {
        return Value is not null && Value.Equals(item);
    }

    private async Task ToggleDropdown()
    {
        if (Disabled)
            return;
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            SearchText = "";
            _highlightedIndex = 0;
            _ = Task.Delay(10).ContinueWith(_ => { InvokeAsync(() => _elementReference.FocusAsync()); });
            _ = RefreshItems();

            // Register outside click
            _dotNetObjectReference = DotNetObjectReference.Create(this);
            if (Js != null)
            {
                _outsideClickListener = await Js.InvokeAsync<IJSObjectReference>(
                    "comboBoxRegisterOutsideClick",
                    _rootRef,
                    _dotNetObjectReference
                );
            }
        }
        else
        {
            await RemoveOutsideClickListener();
        }
    }

    private string GetItemString(TItem item)
    {
        if (ToStringFunc != null)
            return ToStringFunc(item);
        return item?.ToString() ?? "";
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        SearchText = e.Value?.ToString() ?? "";

        if (_debounceCts != null)
        {
            await _debounceCts.CancelAsync();
        }

        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(300, token);
            await RefreshItems();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task RefreshItems()
    {
        if (_virtualizeRef != null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }


    private async ValueTask<ItemsProviderResult<TItem>> LoadItems(
        ItemsProviderRequest request)
    {
        if (DataProvider is not null)
        {
            IsLoading = true;
            StateHasChanged();

            // Invoke the provider and honor cancellation
            var result = await DataProvider(new ComboState(SearchText, request.Count, request.StartIndex), request.CancellationToken);

            _visibleItems = result.Items.ToList();

            IsLoading = false;
            StateHasChanged();

            return result;
        }

        if (StaticData is not null)
        {
            // Client‐side mode: you could check cancellationToken.IsCancellationRequested here if you like,
            // but for an in-memory filter it’s usually unnecessary.
            var filtered = string.IsNullOrEmpty(SearchText)
                ? StaticData
                : StaticData.Where(item => ItemMatches(item, SearchText)).ToList();

            var page = filtered
                .Skip(request.StartIndex)
                .Take(request.Count)
                .ToList();

            _visibleItems = page;
            return new ItemsProviderResult<TItem>(page, filtered.Count);
        }

        // No data
        return new ItemsProviderResult<TItem>(Array.Empty<TItem>(), 0);
    }


    private bool ItemMatches(TItem item, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var str = GetItemString(item);
        return str.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHighlighted(TItem item)
    {
        return item != null && _visibleItems.ElementAtOrDefault(_highlightedIndex)?.Equals(item) == true;
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (Disabled || _visibleItems.Count == 0) return;

        if (e.Key == "ArrowDown")
        {
            _highlightedIndex = Math.Min(_highlightedIndex + 1, _visibleItems.Count - 1);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "ArrowUp")
        {
            _highlightedIndex = Math.Max(_highlightedIndex - 1, 0);
            StateHasChanged();
            await ScrollHighlightedItemIntoView();
        }
        else if (e.Key == "Enter")
        {
            if (_highlightedIndex >= 0 && _highlightedIndex < _visibleItems.Count)
            {
                await ItemClicked(_visibleItems[_highlightedIndex]);
            }
        }
        else if (e.Key == "Escape")
        {
            IsOpen = false;
        }
    }

    private async Task ScrollHighlightedItemIntoView()
    {
        if (Js != null && _rootRef.HasValue)
        {
            await Js.InvokeVoidAsync("comboBoxScrollToHighlighted", _rootRef);
        }
    }

    private async Task ItemClicked(TItem item)
    {
        Value = item;
        IsOpen = false;
        await ValueChanged.InvokeAsync(item);
    }

    [JSInvokable] public async Task CloseDropdown()
    {
        IsOpen = false;
        await RemoveOutsideClickListener();
        StateHasChanged();
    }

    private async Task RemoveOutsideClickListener()
    {
        if (_outsideClickListener != null)
        {
            await _outsideClickListener.InvokeVoidAsync("dispose");
            await _outsideClickListener.DisposeAsync();
            _outsideClickListener = null;
        }

        _dotNetObjectReference?.Dispose();
        _dotNetObjectReference = null;
    }


    public async ValueTask DisposeAsync()
    {
        if (_outsideClickListener is not null)
            await _outsideClickListener.DisposeAsync();

        await DisposeResourceAsync(_dotNetObjectReference);
        await DisposeResourceAsync(_debounceCts);

        _dotNetObjectReference = null;
        _debounceCts = null;
        _outsideClickListener = null;
        _virtualizeRef = null; // You don’t need to dispose Virtualize component manually.

        return;

        static async ValueTask DisposeResourceAsync(object? resource)
        {
            switch (resource)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}