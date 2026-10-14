using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using ComboBox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Xunit;

namespace ComboBox.Tests;

public class MultiSelectTests : ComboTestContext
{
    private static readonly KeyboardEventArgs ArrowDown = new() { Key = "ArrowDown" };
    private static readonly KeyboardEventArgs Enter = new() { Key = "Enter" };
    private static readonly KeyboardEventArgs Space = new() { Key = " " };
    private static readonly KeyboardEventArgs Escape = new() { Key = "Escape" };
    private static readonly KeyboardEventArgs Tab = new() { Key = "Tab" };

    private static readonly string[] Fruits = ["Apple", "Banana", "Cherry"];

    private static IElement Trigger(IRenderedFragment cut) => cut.Find("button.combo-trigger");

    // Unchecked rows keep an invisible checkmark (style="opacity: 0") to hold the column.
    private static string[] CheckedTexts(IRenderedFragment cut) =>
        cut.FindAll(".combo-item")
            .Where(e => e.QuerySelector(".combo-checkmark") is { } mark && mark.GetAttribute("style") is null)
            .Select(e => e.TextContent.Trim())
            .ToArray();

    private static string[] SelectedTexts(IRenderedFragment cut) =>
        cut.FindAll(".combo-item").Where(e => e.GetAttribute("aria-selected") == "true").Select(e => e.TextContent.Trim()).ToArray();

    private static string[] ItemTexts(IRenderedFragment cut) =>
        cut.FindAll(".combo-item").Select(e => e.TextContent.Trim()).ToArray();

    // bUnit does not render element reference ids into the markup, so the trigger's reference is
    // read from the component itself and compared with the last FocusAsync call.
    private void AssertTriggerWasFocusedLast(IComponent component)
    {
        var field = component.GetType().GetField("_triggerRef", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var trigger = (ElementReference)field.GetValue(component)!;
        var focused = (ElementReference)JSInterop.Invocations["Blazor._internal.domWrapper.focus"].Last().Arguments[0]!;
        Assert.Equal(trigger.Id, focused.Id);
    }

    // The portal renders its content only after it has been positioned.
    private static async Task OpenSelect(IRenderedFragment cut)
    {
        await Trigger(cut).ClickAsync(new MouseEventArgs());
        cut.WaitForElement(".combo-item");
    }

    private IRenderedComponent<Select<string>> RenderSelect(Action<IReadOnlyList<string>> onValues, IReadOnlyList<string>? values = null) =>
        RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.Placeholder, "Pick fruit")
            .Bind(c => c.Values, values, v => onValues(v!)));

    [Fact]
    public async Task Select_ClickTogglesItemsInAndOut_InPickOrder_AndKeepsTheDropdownOpen()
    {
        var changes = new List<IReadOnlyList<string>>();
        var cut = RenderSelect(changes.Add);
        Assert.Equal("Pick fruit", cut.Find(".combo-placeholder").TextContent);

        await OpenSelect(cut);
        await cut.FindAll(".combo-item")[1].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".combo-item")[0].ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".combo-dropdown"));
        Assert.Equal(["Banana", "Apple"], changes.Last());
        Assert.Equal("Banana, Apple", cut.Find(".combo-trigger .combo-value").TextContent);

        await cut.FindAll(".combo-item")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal(["Apple"], changes.Last());
        Assert.Equal(3, changes.Count);
        Assert.NotSame(changes[0], changes[1]);
        Assert.Single(cut.FindAll(".combo-dropdown"));
        Assert.Equal(["Apple"], SelectedTexts(cut));
    }

    [Fact]
    public async Task Select_ShowsCheckmarksAndAriaForEverySelectedItem()
    {
        var cut = RenderSelect(_ => { }, ["Cherry", "Apple"]);
        Assert.Equal("Cherry, Apple", cut.Find(".combo-value").TextContent);

        await OpenSelect(cut);

        Assert.Equal("true", cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"));
        Assert.Equal(["Apple", "Cherry"], SelectedTexts(cut));
        Assert.Equal("false", cut.FindAll(".combo-item")[1].GetAttribute("aria-selected"));
        Assert.Equal(["Apple", "Cherry"], CheckedTexts(cut));
    }

    [Fact]
    public async Task Select_EnterAndSpaceToggleTheHighlightedItem_EscapeClosesAndFocusesTheTrigger()
    {
        IReadOnlyList<string>? values = null;
        var cut = RenderSelect(v => values = v);

        await OpenSelect(cut);
        Assert.Equal(["Apple"], HighlightedTexts(cut));

        await Trigger(cut).KeyDownAsync(Enter);
        Assert.Equal(["Apple"], values);
        Assert.Single(cut.FindAll(".combo-dropdown"));

        await Trigger(cut).KeyDownAsync(ArrowDown);
        await Trigger(cut).KeyDownAsync(Space);
        Assert.Equal(["Apple", "Banana"], values);
        Assert.Equal(["Banana"], HighlightedTexts(cut));
        Assert.Single(cut.FindAll(".combo-dropdown"));

        await Trigger(cut).KeyDownAsync(Space);
        Assert.Equal(["Apple"], values);

        await Trigger(cut).KeyDownAsync(Escape);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
        Assert.Equal(["Apple"], values);
        AssertTriggerWasFocusedLast(cut.Instance);
    }

    [Fact]
    public async Task Select_TabClosesTheDropdown()
    {
        var cut = RenderSelect(_ => { });

        await OpenSelect(cut);
        await Trigger(cut).KeyDownAsync(Tab);

        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task Select_KeepsTheHighlightWhenTheBoundParentPassesTheNewValuesBack()
    {
        var cut = RenderSelect(_ => { });
        await OpenSelect(cut);
        await Trigger(cut).KeyDownAsync(ArrowDown);
        await Trigger(cut).KeyDownAsync(ArrowDown);
        await Trigger(cut).KeyDownAsync(Space);

        // What a parent using @bind-Values does after the callback.
        cut.SetParametersAndRender(p => p.Add(c => c.Values, ["Cherry"]).Add(c => c.Items, Fruits.ToList()));

        Assert.Equal(["Cherry"], HighlightedTexts(cut));
        Assert.Equal(["Cherry"], SelectedTexts(cut));
    }

    [Fact]
    public async Task Select_UsesMultiSelectionTextFunc()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.Values, ["Apple", "Banana"])
            .Add(c => c.MultiSelectionTextFunc, v => $"{v.Count} selected"));

        Assert.Equal("2 selected", cut.Find(".combo-trigger-content .combo-value").TextContent);

        await OpenSelect(cut);
        await cut.FindAll(".combo-item")[0].ClickAsync(new MouseEventArgs());
        await cut.FindAll(".combo-item")[1].ClickAsync(new MouseEventArgs());

        // Both toggled out again: back to the placeholder.
        Assert.Single(cut.FindAll(".combo-trigger .combo-placeholder"));
        Assert.Empty(cut.FindAll(".combo-trigger .combo-value"));
    }

    [Fact]
    public async Task Select_SingleSelect_IsUnchanged()
    {
        string? value = null;
        var valuesChanges = 0;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Bind(c => c.Value, null, v => value = v)
            .Add(c => c.ValuesChanged, (IReadOnlyList<string> _) => valuesChanges++));

        await OpenSelect(cut);
        Assert.Null(cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"));

        await cut.FindAll(".combo-item")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("Banana", value);
        Assert.Equal(0, valuesChanges);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
        Assert.Equal("Banana", cut.Find(".combo-value").TextContent);

        await OpenSelect(cut);
        Assert.Equal(["Banana"], SelectedTexts(cut));
        Assert.Equal(["Banana"], CheckedTexts(cut));
    }

    [Fact]
    public async Task BindValues_RoundTripsThroughTheParent()
    {
        var cut = RenderComponent<MultiSelectConsumer>();
        var select = cut.FindComponent<Select<string>>();
        var combobox = cut.FindComponent<Combobox<SearchResult>>();

        await OpenSelect(select);
        await select.Find(".select-trigger").KeyDownAsync(ArrowDown);
        await select.Find(".select-trigger").KeyDownAsync(Space);
        await select.Find(".select-trigger").KeyDownAsync(ArrowDown);
        await select.Find(".select-trigger").KeyDownAsync(Space);

        Assert.Equal(["Banana", "Cherry"], cut.Instance.SelectedFruits);
        Assert.Equal(["Cherry"], HighlightedTexts(select));
        Assert.Equal("Banana, Cherry", select.Find(".combo-value").TextContent);

        var search = await OpenCombobox(combobox);
        await search.InputAsync(new ChangeEventArgs { Value = "Atl" });
        await combobox.Find(".combo-search-input").KeyDownAsync(Enter);
        Assert.Equal("Atlas", combobox.Find(".combo-trigger .combo-value").TextContent);

        await combobox.Find(".combo-search-input").InputAsync(new ChangeEventArgs { Value = "Ap" });
        await combobox.Find(".combo-search-input").KeyDownAsync(Enter);

        Assert.Equal(["Atlas", "Apollo"], cut.Instance.SelectedResults.Select(r => r.Name));
        Assert.Equal("2 selected", combobox.Find(".combo-trigger .combo-value").TextContent);
        Assert.Single(combobox.FindAll(".combo-dropdown"));
    }

    private IRenderedComponent<Combobox<string>> RenderCombobox(Action<ComponentParameterCollectionBuilder<Combobox<string>>> configure) =>
        RenderComponent<Combobox<string>>(p =>
        {
            p.Add(c => c.MultiSelect, true)
                .Add(c => c.Placeholder, "Pick fruit")
                .Add(c => c.SearchDebounceMs, 0);
            configure(p);
        });

    private static async Task<IElement> OpenCombobox(IRenderedFragment cut)
    {
        await cut.Find("button.combo-trigger").ClickAsync(new MouseEventArgs());
        return cut.WaitForElement(".combo-search-input");
    }

    [Fact]
    public async Task Combobox_ClickTogglesItemsInAndOut_AndKeepsTheDropdownAndSearchOpen()
    {
        var changes = new List<IReadOnlyList<string>>();
        var picked = new List<string>();
        var cut = RenderCombobox(p => p
            .Add(c => c.StaticData, Fruits.ToList())
            .Add(c => c.Virtualize, false)
            .Add(c => c.OnItemSelected, item => picked.Add(item))
            .Bind(c => c.Values, null, v => changes.Add(v!)));
        Assert.Equal("Pick fruit", cut.Find(".combo-placeholder").TextContent);

        var search = await OpenCombobox(cut);
        Assert.Equal("true", cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"));

        await search.InputAsync(new ChangeEventArgs { Value = "an" });
        Assert.Equal(["Banana"], ItemTexts(cut));
        await cut.Find(".combo-item").ClickAsync(new MouseEventArgs());

        // The search keeps its text and the dropdown stays open for the next pick.
        Assert.Equal(["Banana"], changes.Last());
        Assert.Equal("an", cut.Find(".combo-search-input").GetAttribute("value"));
        Assert.Equal(["Banana"], CheckedTexts(cut));

        await cut.Find(".combo-search-input").InputAsync(new ChangeEventArgs { Value = "" });
        await cut.FindAll(".combo-item")[2].ClickAsync(new MouseEventArgs());
        Assert.Equal(["Banana", "Cherry"], changes.Last());
        Assert.Equal(["Banana", "Cherry"], CheckedTexts(cut));

        await cut.FindAll(".combo-item")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal(["Cherry"], changes.Last());
        Assert.Equal(["Banana", "Cherry", "Banana"], picked);
        Assert.Single(cut.FindAll(".combo-dropdown"));
        Assert.Equal("Cherry", cut.Find(".combo-trigger .combo-value").TextContent);
    }

    [Fact]
    public async Task Combobox_EnterAndSpaceToggle_SpaceTypesOnceSearching_EscapeCloses()
    {
        IReadOnlyList<string>? values = null;
        var cut = RenderCombobox(p => p
            .Add(c => c.StaticData, Fruits.ToList())
            .Add(c => c.Virtualize, false)
            .Bind(c => c.Values, null, v => values = v));

        var search = await OpenCombobox(cut);
        await search.KeyDownAsync(Enter);
        Assert.Equal(["Apple"], values);
        Assert.Single(cut.FindAll(".combo-dropdown"));

        await cut.Find(".combo-search-input").KeyDownAsync(ArrowDown);
        await cut.Find(".combo-search-input").KeyDownAsync(Space);
        Assert.Equal(["Apple", "Banana"], values);
        Assert.Equal(["Banana"], HighlightedTexts(cut));

        // With text in the search box Space belongs to the search, not to the list.
        await cut.Find(".combo-search-input").InputAsync(new ChangeEventArgs { Value = "Ch" });
        await cut.Find(".combo-search-input").KeyDownAsync(Space);
        Assert.Equal(["Apple", "Banana"], values);

        await cut.Find(".combo-search-input").KeyDownAsync(Enter);
        Assert.Equal(["Apple", "Banana", "Cherry"], values);
        Assert.Single(cut.FindAll(".combo-dropdown"));

        await cut.Find(".combo-search-input").KeyDownAsync(Escape);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
        AssertTriggerWasFocusedLast(cut.Instance);

        // Enter did not leave a pending skip behind: the next click on the trigger opens again.
        await OpenCombobox(cut);
        Assert.Single(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task Combobox_TabClosesTheDropdown()
    {
        var cut = RenderCombobox(p => p.Add(c => c.StaticData, Fruits.ToList()).Add(c => c.Virtualize, false));

        var search = await OpenCombobox(cut);
        await search.KeyDownAsync(Tab);

        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task Combobox_ClearOnSelect_ClearsTheSearchButKeepsTheDropdownOpen()
    {
        IReadOnlyList<string>? values = null;
        string? searchText = null;
        var cut = RenderCombobox(p => p
            .Add(c => c.StaticData, Fruits.ToList())
            .Add(c => c.Virtualize, false)
            .Add(c => c.ClearOnSelect, true)
            .Bind(c => c.SearchText, null, text => searchText = text)
            .Bind(c => c.Values, null, v => values = v));

        var search = await OpenCombobox(cut);
        await search.InputAsync(new ChangeEventArgs { Value = "Ch" });
        await cut.Find(".combo-search-input").KeyDownAsync(Enter);

        Assert.Equal(["Cherry"], values);
        Assert.Equal("", searchText);
        Assert.Equal("", cut.Find(".combo-search-input").GetAttribute("value"));
        Assert.Equal(Fruits, ItemTexts(cut));
        Assert.Equal(["Cherry"], CheckedTexts(cut));
    }

    [Fact]
    public async Task Combobox_WithDataProviderAndVirtualize_ChecksItemsFromValues()
    {
        var all = Enumerable.Range(1, 30).Select(i => $"Item {i:00}").ToList();
        var cut = RenderCombobox(p => p
            .Add(c => c.Values, ["Item 03", "Item 01"])
            .Add(c => c.DataProvider, (state, _) =>
            {
                var matches = all.Where(x => string.IsNullOrEmpty(state.Search) || x.Contains(state.Search)).ToList();
                return ValueTask.FromResult(new ItemsProviderResult<string>(matches.Skip(state.Skip).Take(state.Take), matches.Count));
            }));

        Assert.Equal("Item 03, Item 01", cut.Find(".combo-value").TextContent);

        await OpenCombobox(cut);
        cut.WaitForState(() => cut.FindAll(".combo-item[role=option]").Count > 0);
        Assert.Equal(["Item 01", "Item 03"], CheckedTexts(cut));

        // A fresh page from the provider is checked against Values, not against earlier pages.
        await cut.Find(".combo-search-input").InputAsync(new ChangeEventArgs { Value = "3" });
        cut.WaitForState(() => cut.FindAll(".combo-item[role=option]").Count == 4);
        Assert.Equal(["Item 03"], CheckedTexts(cut));
    }

    [Fact]
    public void Combobox_MultiSelectWithSearchInTrigger_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RenderCombobox(p => p
            .Add(c => c.StaticData, Fruits.ToList())
            .Add(c => c.SearchInTrigger, true)));

        Assert.Contains("SearchInTrigger", error.Message);
    }

    [Fact]
    public async Task Combobox_SingleSelect_IsUnchanged()
    {
        string? value = null;
        var valuesChanges = 0;
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.StaticData, Fruits.ToList())
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Bind(c => c.Value, null, v => value = v)
            .Add(c => c.ValuesChanged, (IReadOnlyList<string> _) => valuesChanges++));

        var search = await OpenCombobox(cut);
        Assert.Null(cut.Find("[role=listbox]").GetAttribute("aria-multiselectable"));

        await search.KeyDownAsync(ArrowDown);
        await cut.Find(".combo-search-input").KeyDownAsync(Enter);

        Assert.Equal("Banana", value);
        Assert.Equal(0, valuesChanges);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
        Assert.Equal("Banana", cut.Find(".combo-value").TextContent);
    }
}
