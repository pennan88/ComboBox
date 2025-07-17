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
    [Parameter] public TItem? Value { get; set; }
    [Parameter] public EventCallback<TItem?> ValueChanged { get; set; }
    [Parameter] public string Placeholder { get; set; } = "";
    [Parameter] public Func<TItem, string>? ToStringFunc { get; set; }
    [Parameter] public bool ShowCheckmarkInList { get; set; } = true;
    [Parameter] public RenderFragment? NoResultsTemplate { get; set; }
    [Parameter] public Func<string?, int, int, ValueTask<ItemsProviderResult<TItem>>>? DataProvider { get; set; }
    [Parameter] public List<TItem>? StaticData { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment<TItem>? SelectedTemplate { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? InputClass { get; set; }
    [Parameter] public string? DropdownClass { get; set; }
    [Parameter] public string? Label { get; set; }

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

    private async ValueTask<ItemsProviderResult<TItem>> LoadItems(ItemsProviderRequest request)
    {
        if (DataProvider is not null)
        {
            IsLoading = true;
            StateHasChanged();
            // Server-side mode
            var result = await DataProvider(SearchText, request.StartIndex, request.Count);
            _visibleItems = result.Items.ToList();
            IsLoading = false;
            StateHasChanged();
            return result;
        }

        if (StaticData is not null)
        {
            
            // Client-side mode
            var filtered = StaticData;
            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered
                    .Where(item => ItemMatches(item, SearchText))
                    .ToList();
            }

            var page = filtered
                .Skip(request.StartIndex)
                .Take(request.Count)
                .ToList();

            _visibleItems = page;

            return new ItemsProviderResult<TItem>(page, filtered.Count);
        }

        // No data
        return new ItemsProviderResult<TItem>(new List<TItem>(), 0);
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