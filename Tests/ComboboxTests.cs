using System.Diagnostics;
using AngleSharp.Dom;
using Bunit;
using ComboBox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Xunit;

namespace ComboBox.Tests;

public class ComboboxTests : ComboTestContext
{
    private static readonly KeyboardEventArgs ArrowDown = new() { Key = "ArrowDown" };
    private static readonly KeyboardEventArgs ArrowUp = new() { Key = "ArrowUp" };
    private static readonly KeyboardEventArgs Enter = new() { Key = "Enter" };

    private static readonly List<SearchResult> GroupedItems =
    [
        new("A1", "Alpha", ""),
        new("A2", "Alpha", ""),
        new("B1", "Beta", ""),
        new("B2", "Beta", ""),
    ];

    private static Func<ComboState, CancellationToken, ValueTask<ItemsProviderResult<SearchResult>>> Provider(List<ComboState>? requests = null) =>
        (state, _) =>
        {
            requests?.Add(new ComboState(state.Search, state.Take, state.Skip));
            var items = SearchResult.All
                .Where(x => string.IsNullOrEmpty(state.Search) || x.Name.Contains(state.Search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ToList();
            return ValueTask.FromResult(new ItemsProviderResult<SearchResult>(items, items.Count));
        };

    private IElement TriggerInput(IRenderedFragment cut) => cut.Find("input.combo-trigger-input");

    [Fact]
    public async Task ArrowKeys_SkipGroupHeadersAndFooters()
    {
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.StaticData, GroupedItems)
            .Add(c => c.ToStringFunc, x => x.Name)
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.GroupBy, x => x.Type)
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.GroupFooterTemplate, g => b => b.AddMarkupContent(0, $"<button class=\"more\">More {g.Key}</button>")));

        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 4);

        Assert.Equal(["Alpha", "Beta"], cut.FindAll(".combo-group-header").Select(h => h.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll(".combo-group-footer .more").Count);
        Assert.Equal(["A1"], HighlightedTexts(cut));

        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["A2"], HighlightedTexts(cut));

        // Past the Alpha footer and the Beta header straight to the next item.
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["B1"], HighlightedTexts(cut));
        Assert.Equal(cut.Find(".combo-item.highlighted").Id, TriggerInput(cut).GetAttribute("aria-activedescendant"));

        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["B2"], HighlightedTexts(cut));

        await TriggerInput(cut).KeyDownAsync(ArrowUp);
        await TriggerInput(cut).KeyDownAsync(ArrowUp);
        Assert.Equal(["A2"], HighlightedTexts(cut));
    }

    [Fact]
    public async Task Enter_WithNothingHighlighted_InvokesAddOptionChanged()
    {
        var added = new List<string?>();
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider())
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.HighlightFirstItem, false)
            .Add(c => c.ShowAddOption, true)
            .Add(c => c.AddOptionChanged, (string? text) => added.Add(text))
            .Add(c => c.SearchDebounceMs, 0));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Al" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count > 0);
        Assert.Empty(HighlightedTexts(cut));

        await TriggerInput(cut).KeyDownAsync(Enter);

        Assert.Equal(["Al"], added);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task Enter_WithNoResults_InvokesAddOptionChanged_InDefaultMode()
    {
        var added = new List<string?>();
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.StaticData, ["Blazor", "Razor"])
            .Add(c => c.ShowAddOption, true)
            .Add(c => c.AddOptionChanged, (string? text) => added.Add(text))
            .Add(c => c.SearchDebounceMs, 0));

        await cut.Find("button.combo-trigger").ClickAsync(new MouseEventArgs());
        var search = cut.WaitForElement(".combo-search-input");
        await search.InputAsync(new ChangeEventArgs { Value = "C#" });
        await cut.Find(".combo-search-input").KeyDownAsync(Enter);

        Assert.Equal(["C#"], added);
    }

    [Fact]
    public async Task AddOption_OnTop_IsReachedWithArrowUpFromTheFirstItem()
    {
        var added = new List<string?>();
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider())
            .Add(c => c.ToStringFunc, x => x.Name)
            .Add(c => c.Virtualize, false)
            .Add(c => c.ShowAddOption, true)
            .Add(c => c.AddOptionChanged, (string? text) => added.Add(text))
            .Add(c => c.SearchDebounceMs, 0));

        await cut.Find("button.combo-trigger").ClickAsync(new MouseEventArgs());
        await cut.WaitForElement(".combo-search-input").InputAsync(new ChangeEventArgs { Value = "Atl" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.Equal(["Atlas"], HighlightedTexts(cut));

        await cut.Find(".combo-search-input").KeyDownAsync(ArrowUp);
        Assert.Contains("highlighted", cut.Find(".add-button").ClassList);
        Assert.Empty(cut.FindAll(".combo-item.highlighted"));

        await cut.Find(".combo-search-input").KeyDownAsync(ArrowDown);
        Assert.Equal(["Atlas"], HighlightedTexts(cut));

        await cut.Find(".combo-search-input").KeyDownAsync(ArrowUp);
        await cut.Find(".combo-search-input").KeyDownAsync(Enter);
        Assert.Equal(["Atl"], added);
    }

    [Fact]
    public async Task AddOption_AtBottom_IsRenderedLastAndReachedWithArrowDown()
    {
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider())
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.ShowAddOption, true)
            .Add(c => c.AddOptionPosition, AddOptionPosition.Bottom)
            .Add(c => c.AddOptionChanged, (string? _) => { })
            .Add(c => c.SearchDebounceMs, 0));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.Equal("add-button", cut.Find(".combo-content-container").Children.Last().ClassList.First());

        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.Contains("highlighted", cut.Find(".add-button").ClassList);
    }

    [Fact]
    public async Task ClearOnSelect_ReportsThePickWithoutKeepingAValue()
    {
        var picked = new List<SearchResult>();
        var valueChanges = 0;
        string? boundText = null;
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.StaticData, SearchResult.All.ToList())
            .Add(c => c.ToStringFunc, x => x.Name)
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.ClearOnSelect, true)
            .Add(c => c.SearchDebounceMs, 0)
            .Bind(c => c.SearchText, null, text => boundText = text)
            .Add(c => c.OnItemSelected, item => picked.Add(item))
            .Add(c => c.ValueChanged, (SearchResult? _) => valueChanges++));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.Equal("Atl", boundText);

        await cut.Find(".combo-item").ClickAsync(new MouseEventArgs());

        Assert.Equal("Atlas", Assert.Single(picked).Name);
        Assert.Equal(0, valueChanges);
        Assert.Null(cut.Instance.Value);
        Assert.Equal("", boundText);
        Assert.Equal("", TriggerInput(cut).GetAttribute("value"));
        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task WithoutClearOnSelect_APickSetsTheValueAndTheSearchText()
    {
        var picked = new List<SearchResult>();
        SearchResult? value = null;
        string? boundText = null;
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.StaticData, SearchResult.All.ToList())
            .Add(c => c.ToStringFunc, x => x.Name)
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Bind(c => c.SearchText, null, text => boundText = text)
            .Bind(c => c.Value, null, item => value = item)
            .Add(c => c.OnItemSelected, item => picked.Add(item)));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        await TriggerInput(cut).KeyDownAsync(Enter);

        Assert.Equal("Atlas", value?.Name);
        Assert.Equal("Atlas", Assert.Single(picked).Name);
        Assert.Equal("Atlas", boundText);
        Assert.Equal("Atlas", TriggerInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task SearchText_SetByParent_IsShownButOnlySearchedOnFocus()
    {
        var requests = new List<ComboState>();
        var reported = new List<string?>();
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider(requests))
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Bind(c => c.SearchText, "restored", text => reported.Add(text)));

        Assert.Equal("restored", TriggerInput(cut).GetAttribute("value"));

        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "Atl"));
        Assert.Equal("Atl", TriggerInput(cut).GetAttribute("value"));
        Assert.Empty(requests);
        Assert.Empty(cut.FindAll(".combo-dropdown"));

        await TriggerInput(cut).FocusAsync(new FocusEventArgs());
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.Equal(["Atl"], requests.Select(r => r.Search));
        Assert.Empty(reported);

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Apo" });
        Assert.Equal(["Apo"], reported);
        Assert.Equal(["Atl", "Apo"], requests.Select(r => r.Search));
    }

    [Fact]
    public async Task SearchTextChanged_FiresAfterTheDebounce()
    {
        var reported = new List<string?>();
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider())
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 200)
            .Bind(c => c.SearchText, "", text => reported.Add(text)));

        var stopwatch = Stopwatch.StartNew();
        var typing = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        Assert.Empty(reported);

        await typing;
        Assert.Equal(["Atl"], reported);
        Assert.True(stopwatch.ElapsedMilliseconds >= 150, $"Reported after {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task SearchTextChanged_WithoutSearchText_DoesNotResetTheInput()
    {
        // A consumer that only listens re-renders on every notification without passing SearchText.
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, Provider())
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.SearchTextChanged, (string? _) => { }));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        cut.SetParametersAndRender(p => p.Add(c => c.Placeholder, "re-render"));

        Assert.Equal("Atl", TriggerInput(cut).GetAttribute("value"));
    }

    [Fact]
    public async Task SearchBoxConsumer_WorksEndToEnd()
    {
        var cut = RenderComponent<SearchBoxConsumer>();
        var consumer = cut.Instance;
        Assert.Equal("Sök projekt, användare eller grupp", TriggerInput(cut).GetAttribute("placeholder"));

        // Typing opens the dropdown with grouped results once the default 300 ms debounce elapses.
        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "a" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 6, TimeSpan.FromSeconds(2));
        Assert.Equal("a", consumer.Text);
        Assert.Equal(["Användare", "Grupp", "Projekt"], cut.FindAll(".combo-group-header").Select(h => h.TextContent.Trim()));
        Assert.Equal(3, cut.FindAll(".combo-group-footer button").Count);
        Assert.Contains("Enter väljer", cut.Find(".combo-footer").TextContent);
        Assert.Contains("Sök på:", cut.Find(".combo-content-container > .add-button:first-child").TextContent);
        Assert.Empty(HighlightedTexts(cut));

        // "Visa fler" reloads in place with the current text.
        var requestCount = consumer.Requests.Count;
        await cut.FindAll(".combo-group-footer button")[1].ClickAsync(new MouseEventArgs());
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 7);
        Assert.Single(cut.FindAll(".combo-dropdown"));
        Assert.Equal(requestCount + 1, consumer.Requests.Count);
        Assert.All(consumer.Requests, r => Assert.Equal(("a", 0, 50), (r.Search, r.Skip, r.Take)));

        // Arrow keys: first result, up to the add option, then down across a group boundary.
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.StartsWith("Anna Andersson", Assert.Single(HighlightedTexts(cut)));
        await TriggerInput(cut).KeyDownAsync(ArrowUp);
        Assert.Contains("highlighted", cut.Find(".add-button").ClassList);
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        await TriggerInput(cut).KeyDownAsync(ArrowDown);
        Assert.StartsWith("Apollo", Assert.Single(HighlightedTexts(cut)));

        // Enter picks it: OnItemSelected fires and the search text is cleared.
        await TriggerInput(cut).KeyDownAsync(Enter);
        Assert.Equal("Apollo", Assert.Single(consumer.Picked).Name);
        Assert.Equal("", consumer.Text);
        Assert.Empty(cut.FindAll(".combo-dropdown"));

        // Enter with nothing highlighted submits the free text.
        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "zzz" });
        cut.WaitForElement(".combo-no-results", TimeSpan.FromSeconds(2));
        await TriggerInput(cut).KeyDownAsync(Enter);
        Assert.Equal(["zzz"], consumer.Submitted);
    }

    [Fact]
    public async Task DefaultMode_KeepsTheButtonTriggerAndTheSearchInputInsideTheDropdown()
    {
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.StaticData, ["Apple", "Banana", "Cherry"])
            .Add(c => c.ShowAddOption, true)
            .Add(c => c.AddOptionChanged, (string? _) => { })
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.Placeholder, "Choose an item..."));

        var trigger = cut.Find("button.combo-trigger");
        Assert.Equal("combobox", trigger.GetAttribute("role"));
        Assert.Equal("Choose an item...", cut.Find(".combo-placeholder").TextContent);
        Assert.Empty(cut.FindAll("input.combo-trigger-input"));

        await trigger.ClickAsync(new MouseEventArgs());
        var search = cut.WaitForElement(".combo-search-input");
        Assert.Equal("Search...", search.GetAttribute("placeholder"));
        Assert.NotNull(cut.Find(".combo-overlay"));
        Assert.Empty(cut.FindAll(".combo-footer"));

        // The add option keeps its original place above the list.
        await search.InputAsync(new ChangeEventArgs { Value = "Ap" });
        Assert.Equal("add-button", cut.Find(".combo-content-container").Children.First().ClassList.First());
    }

    [Fact]
    public void GroupBy_RequiresVirtualizeFalse()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.StaticData, GroupedItems)
            .Add(c => c.GroupBy, x => x.Type)));

        Assert.Contains("Virtualize", error.Message);
    }
}
