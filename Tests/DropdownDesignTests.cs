using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using ComboBox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Xunit;

namespace ComboBox.Tests;

/// <summary>
/// The additions for the Dropdown 2.0 design: templates with row state, clear button, loading and
/// error states, localizable announcements, accessibility attributes and search behaviour.
/// </summary>
public class DropdownDesignTests : ComboTestContext
{
    private static readonly KeyboardEventArgs ArrowDown = new() { Key = "ArrowDown" };
    private static readonly KeyboardEventArgs ArrowUp = new() { Key = "ArrowUp" };
    private static readonly KeyboardEventArgs Enter = new() { Key = "Enter" };
    private static readonly KeyboardEventArgs Space = new() { Key = " " };
    private static readonly KeyboardEventArgs Escape = new() { Key = "Escape" };

    private static readonly List<string> Fruits = ["Apple", "Banana", "Cherry"];

    private static IElement Trigger(IRenderedFragment cut) => cut.Find("button.combo-trigger");

    private static IElement Search(IRenderedFragment cut) => cut.Find(".combo-search-input");

    private static async Task<IElement> OpenCombobox(IRenderedFragment cut)
    {
        await Trigger(cut).ClickAsync(new MouseEventArgs());
        return cut.WaitForElement(".combo-search-input");
    }

    private static async Task OpenSelect(IRenderedFragment cut)
    {
        await Trigger(cut).ClickAsync(new MouseEventArgs());
        cut.WaitForElement(".combo-dropdown");
    }

    private void AssertFocusedLast(IComponent component, string field)
    {
        var reference = (ElementReference)component.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(component)!;
        var focused = (ElementReference)JSInterop.Invocations["Blazor._internal.domWrapper.focus"].Last().Arguments[0]!;
        Assert.Equal(reference.Id, focused.Id);
    }

    private IRenderedComponent<Combobox<string>> RenderFruits(Action<ComponentParameterCollectionBuilder<Combobox<string>>>? configure = null) =>
        RenderComponent<Combobox<string>>(p =>
        {
            p.Add(c => c.StaticData, Fruits).Add(c => c.Virtualize, false).Add(c => c.SearchDebounceMs, 0);
            configure?.Invoke(p);
        });

    // 1. ItemContentTemplate

    [Fact]
    public async Task ItemContentTemplate_GetsTheRowState_AndWinsOverItemTemplate()
    {
        var contexts = new List<ComboItemContext<string>>();
        var cut = RenderFruits(p => p
            .Add(c => c.Value, "Banana")
            .Add(c => c.ItemDisabledFunc, x => x == "Cherry")
            .Add(c => c.ItemTemplate, item => b => b.AddContent(0, "old " + item))
            .Add(c => c.ItemContentTemplate, ctx => b =>
            {
                contexts.Add(ctx);
                b.AddContent(0, $"{ctx.Item}|{ctx.IsSelected}|{ctx.IsHighlighted}|{ctx.IsDisabled}|{ctx.SearchText}");
            }));

        await OpenCombobox(cut);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);

        Assert.Equal(
            ["Apple|False|True|False|", "Banana|True|False|False|", "Cherry|False|False|True|"],
            cut.FindAll(".combo-item").Select(e => e.TextContent.Trim()));

        await Search(cut).InputAsync(new ChangeEventArgs { Value = "an" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.Equal("Banana|True|True|False|an", cut.Find(".combo-item").TextContent.Trim());
        Assert.DoesNotContain("old Banana", cut.Markup);
    }

    [Fact]
    public void SplitByMatch_MarksEveryCaseInsensitiveOccurrence()
    {
        var context = new ComboItemContext<string>("Banana", false, false, false, "AN");

        Assert.Equal(
            [new("B", false), new("an", true), new("an", true), new("a", false)],
            context.SplitByMatch("Banana"));
        Assert.Equal([new ComboTextSegment("Banana", false)], new ComboItemContext<string>("Banana", false, false, false, "").SplitByMatch("Banana"));
    }

    [Fact]
    public async Task ItemContentTemplate_WithoutCheckmarks_LeavesTheWholeRowToTheTemplate()
    {
        var cut = RenderFruits(p => p
            .Add(c => c.ShowCheckmarkInList, false)
            .Add(c => c.ItemContentTemplate, ctx => b => b.AddMarkupContent(0, $"<input type=\"checkbox\" {(ctx.IsSelected ? "checked" : "")} />{ctx.Item}")));

        await OpenCombobox(cut);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);

        Assert.Empty(cut.FindAll(".combo-checkmark"));
        Assert.Equal(3, cut.FindAll(".combo-item input[type=checkbox]").Count);
    }

    // 2. Clearable

    [Fact]
    public async Task Clearable_Combobox_ClearsTheValue_FromASiblingButton_AndFocusesTheTrigger()
    {
        string? value = "Banana";
        var cleared = 0;
        var cut = RenderFruits(p => p
            .Add(c => c.Clearable, true)
            .Add(c => c.ClearButtonAriaLabel, "Rensa val")
            .Add(c => c.OnClear, () => cleared++)
            .Bind(c => c.Value, value, v => value = v));

        var clear = cut.Find(".combo-clear-value");
        Assert.Equal("Rensa val", clear.GetAttribute("aria-label"));
        Assert.Equal("BUTTON", clear.TagName);
        // Not nested in the trigger button, but its sibling.
        Assert.Null(clear.Closest(".combo-trigger"));
        Assert.Same(Trigger(cut).ParentElement, clear.ParentElement);
        Assert.Contains("combo-trigger-wrapper", clear.ParentElement!.ClassList);
        // The spacer keeps room for it between the value and the chevron.
        Assert.Equal(["combo-trigger-content", "combo-clear-spacer", "combo-chevron"],
            Trigger(cut).Children.Select(e => e.ClassList.First()));

        await clear.ClickAsync(new MouseEventArgs());

        Assert.Null(value);
        Assert.Equal(1, cleared);
        Assert.Empty(cut.FindAll(".combo-clear-value"));
        Assert.Empty(cut.FindAll(".combo-clear-spacer"));
        AssertFocusedLast(cut.Instance, "_triggerRef");
    }

    [Fact]
    public async Task Clearable_Select_MultiSelect_ClearsAllValues()
    {
        IReadOnlyList<string>? values = ["Apple", "Cherry"];
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.Clearable, true)
            .Bind(c => c.Values, values, v => values = v));

        await cut.Find(".combo-clear-value").ClickAsync(new MouseEventArgs());

        Assert.Empty(values!);
        Assert.Empty(cut.FindAll(".combo-clear-value"));
        AssertFocusedLast(cut.Instance, "_triggerRef");
    }

    [Fact]
    public void Clearable_WithoutAValue_ShowsNoClearButton_AndWithoutClearable_NoWrapper()
    {
        var clearable = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.Clearable, true));
        Assert.Empty(clearable.FindAll(".combo-clear-value"));

        var plain = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.Value, "Apple"));
        Assert.Empty(plain.FindAll(".combo-trigger-wrapper"));
        Assert.Empty(plain.FindAll(".combo-clear-value"));
    }

    // 3. Loading

    [Fact]
    public async Task Loading_ShowsASpinnerBesideTheChevron_AndTheTriggerStillOpens()
    {
        var cut = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.Loading, true));

        Assert.Equal(["combo-trigger-content", "combo-spinner", "combo-chevron"],
            Trigger(cut).Children.Select(e => e.ClassList.First()));
        Assert.Equal("true", Trigger(cut).GetAttribute("aria-busy"));

        await OpenSelect(cut);
        Assert.Equal(3, cut.FindAll(".combo-item").Count);
    }

    // 4. CloseAsync

    [Fact]
    public async Task CloseAsync_ClosesFromAFooterButton_AndFocusesTheTrigger()
    {
        Combobox<string>? combo = null;
        var cut = RenderFruits(p => p
            .Add(c => c.MultiSelect, true)
            .Add(c => c.FooterTemplate, b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "class", "done");
                b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => combo!.CloseAsync()));
                b.AddContent(3, "Klar");
                b.CloseElement();
            }));
        combo = cut.Instance;

        await OpenCombobox(cut);
        await cut.WaitForElement(".combo-footer .done").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".combo-dropdown"));
        AssertFocusedLast(cut.Instance, "_triggerRef");
    }

    [Fact]
    public async Task Select_CloseAsync_ClosesTheDropdown()
    {
        var cut = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits));
        await OpenSelect(cut);

        await cut.InvokeAsync(() => cut.Instance.CloseAsync());

        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    // 5. Errors from the DataProvider

    [Fact]
    public async Task DataProviderError_ShowsErrorText_AndAnnouncesIt()
    {
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) => throw new InvalidOperationException("boom"))
            .Add(c => c.Virtualize, false)
            .Add(c => c.ErrorText, "Kunde inte hämta"));

        await OpenCombobox(cut);

        Assert.Contains("Kunde inte hämta", cut.WaitForElement(".combo-error").TextContent);
        Assert.Empty(cut.FindAll(".combo-no-results"));
        Assert.Contains("Kunde inte hämta", cut.Find(".combo-sr-only").TextContent);
    }

    [Fact]
    public async Task ErrorTemplate_GetsTheException_AndRetryCallsTheProviderAgain()
    {
        var calls = 0;
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) =>
            {
                if (++calls == 1)
                    throw new TimeoutException("slow");
                return ValueTask.FromResult(new ItemsProviderResult<string>(Fruits, Fruits.Count));
            })
            .Add(c => c.Virtualize, false)
            .Add(c => c.ErrorTemplate, ctx => b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "class", "retry");
                b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, ctx.Retry));
                b.AddContent(3, ctx.Exception.Message);
                b.CloseElement();
            }));

        await OpenCombobox(cut);
        var retry = cut.WaitForElement(".combo-error .retry");
        Assert.Equal("slow", retry.TextContent);

        await retry.ClickAsync(new MouseEventArgs());

        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
        Assert.Equal(2, calls);
        Assert.Empty(cut.FindAll(".combo-error"));
    }

    [Fact]
    public async Task DataProviderError_WithVirtualize_IsShownInsteadOfThrowing()
    {
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) => throw new HttpRequestException("offline")));

        await OpenCombobox(cut);
        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForElement(".combo-error");
        Assert.Empty(cut.FindAll(".combo-no-results"));
    }

    [Fact]
    public async Task Select_DataProviderError_ShowsErrorTemplate()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.DataProvider, _ => throw new InvalidOperationException("nope"))
            .Add(c => c.ErrorTemplate, ctx => b => b.AddContent(0, "Fel: " + ctx.Exception.Message)));

        await OpenSelect(cut);

        Assert.Equal("Fel: nope", cut.WaitForElement(".combo-error").TextContent.Trim());
    }

    // 6. Localizable texts and the result count (10)

    [Fact]
    public async Task LoadingText_ShowsSpinnerAndText_InsteadOfTheDots()
    {
        var pending = new TaskCompletionSource<ItemsProviderResult<string>>();
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) => new ValueTask<ItemsProviderResult<string>>(pending.Task))
            .Add(c => c.Virtualize, false)
            .Add(c => c.LoadingText, "Söker ...")
            .Add(c => c.LoadingAnnouncementText, "Laddar"));

        // Opening awaits the first load, which stays pending until the test completes it.
        _ = Trigger(cut).ClickAsync(new MouseEventArgs());

        var row = cut.WaitForElement(".combo-loading-row");
        Assert.NotNull(row.QuerySelector(".combo-spinner"));
        Assert.Equal("Söker ...", row.QuerySelector(".combo-loading-text")!.TextContent);
        Assert.Null(row.QuerySelector(".progress"));
        Assert.Equal("Laddar", cut.Find(".combo-sr-only").TextContent.Trim());

        pending.SetResult(new ItemsProviderResult<string>(Fruits, Fruits.Count));
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
    }

    [Fact]
    public async Task LoadingTemplate_ReplacesTheLoadingRow()
    {
        var pending = new TaskCompletionSource<ItemsProviderResult<string>>();
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) => new ValueTask<ItemsProviderResult<string>>(pending.Task))
            .Add(c => c.Virtualize, false)
            .Add(c => c.LoadingText, "ignored")
            .Add(c => c.LoadingTemplate, b => b.AddMarkupContent(0, "<span class=\"mine\">Hämtar</span>")));

        // Opening awaits the first load, which stays pending until the test completes it.
        _ = Trigger(cut).ClickAsync(new MouseEventArgs());

        var row = cut.WaitForElement(".combo-loading-row");
        Assert.Equal("Hämtar", row.QuerySelector(".mine")!.TextContent);
        Assert.DoesNotContain("ignored", row.TextContent);
        pending.SetResult(new ItemsProviderResult<string>([], 0));
    }

    [Fact]
    public async Task WithoutLoadingText_TheLoadingRowKeepsTheDots()
    {
        var pending = new TaskCompletionSource<ItemsProviderResult<string>>();
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.DataProvider, (ComboState _, CancellationToken _) => new ValueTask<ItemsProviderResult<string>>(pending.Task))
            .Add(c => c.Virtualize, false));

        // Opening awaits the first load, which stays pending until the test completes it.
        _ = Trigger(cut).ClickAsync(new MouseEventArgs());

        var row = cut.WaitForElement(".combo-loading-row");
        Assert.NotNull(row.QuerySelector(".progress"));
        Assert.Null(row.QuerySelector(".combo-spinner"));
        pending.SetResult(new ItemsProviderResult<string>([], 0));
    }

    [Fact]
    public async Task ResultCount_IsAnnouncedButNotShown()
    {
        var cut = RenderFruits(p => p.Add(c => c.ResultCountTextFunc, n => $"{n} träffar"));

        await OpenCombobox(cut);
        cut.WaitForState(() => cut.Find(".combo-sr-only").TextContent.Trim() == "3 träffar");

        await Search(cut).InputAsync(new ChangeEventArgs { Value = "Ch" });
        cut.WaitForState(() => cut.Find(".combo-sr-only").TextContent.Trim() == "1 träffar");

        // Only the visually hidden live region carries it.
        Assert.Equal("polite", cut.Find(".combo-sr-only").GetAttribute("aria-live"));
        Assert.DoesNotContain("träffar", cut.Find(".combo-content-container").TextContent);
    }

    [Fact]
    public async Task ResultCount_DefaultsToEnglish()
    {
        var cut = RenderFruits();
        await OpenCombobox(cut);
        cut.WaitForState(() => cut.Find(".combo-sr-only").TextContent.Trim() == "3 results");
    }

    // 7–9. aria-describedby, aria-invalid, external label

    [Fact]
    public async Task AriaAttributes_AreSetOnTheTriggerAndTheSearchInput()
    {
        var cut = RenderFruits(p => p
            .Add(c => c.Label, "Ignored label")
            .Add(c => c.AriaDescribedBy, "help-1 error-1")
            .Add(c => c.AriaLabelledBy, "my-label")
            .Add(c => c.Invalid, true));

        var trigger = Trigger(cut);
        Assert.Equal("help-1 error-1", trigger.GetAttribute("aria-describedby"));
        Assert.Equal("true", trigger.GetAttribute("aria-invalid"));
        Assert.Equal("my-label", trigger.GetAttribute("aria-labelledby"));
        Assert.Null(trigger.GetAttribute("aria-label"));
        Assert.Contains("combo-invalid", cut.Find(".combo-container").ClassList);

        await OpenCombobox(cut);
        Assert.Equal("help-1 error-1", Search(cut).GetAttribute("aria-describedby"));
        Assert.Equal("my-label", cut.Find("[role=listbox]").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void AriaAttributes_AreAbsentByDefault()
    {
        var cut = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.Placeholder, "Välj"));

        var trigger = Trigger(cut);
        Assert.Null(trigger.GetAttribute("aria-describedby"));
        Assert.Null(trigger.GetAttribute("aria-invalid"));
        Assert.Null(trigger.GetAttribute("aria-labelledby"));
        Assert.Null(trigger.GetAttribute("aria-busy"));
        Assert.Equal("Välj", trigger.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task AriaExpanded_IsAlwaysAnExplicitValue()
    {
        var select = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits));
        Assert.Equal("false", Trigger(select).GetAttribute("aria-expanded"));
        await OpenSelect(select);
        Assert.Equal("true", Trigger(select).GetAttribute("aria-expanded"));

        var combo = RenderFruits();
        Assert.Equal("false", Trigger(combo).GetAttribute("aria-expanded"));
        await OpenCombobox(combo);
        Assert.Equal("true", Trigger(combo).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Select_AriaLabelledByAndInvalid()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.AriaLabelledBy, "lbl")
            .Add(c => c.AriaDescribedBy, "desc")
            .Add(c => c.Invalid, true));

        var trigger = Trigger(cut);
        Assert.Equal("lbl", trigger.GetAttribute("aria-labelledby"));
        Assert.Equal("desc", trigger.GetAttribute("aria-describedby"));
        Assert.Equal("true", trigger.GetAttribute("aria-invalid"));
        Assert.Null(trigger.GetAttribute("aria-label"));
    }

    // 11. Disabled options

    [Fact]
    public async Task DisabledItems_AreReachedWithArrows_ButCannotBePicked()
    {
        string? value = null;
        var cut = RenderFruits(p => p
            .Add(c => c.ItemDisabledFunc, x => x == "Banana")
            .Bind(c => c.Value, value, v => value = v));

        await OpenCombobox(cut);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
        Assert.Equal("true", cut.FindAll(".combo-item")[1].GetAttribute("aria-disabled"));
        Assert.Null(cut.FindAll(".combo-item")[0].GetAttribute("aria-disabled"));

        await Search(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["Banana"], HighlightedTexts(cut));
        Assert.Equal(cut.Find(".combo-item.highlighted").Id, Search(cut).GetAttribute("aria-activedescendant"));

        await Search(cut).KeyDownAsync(Enter);
        await cut.InvokeAsync(() => cut.FindAll(".combo-item")[1].Click());
        Assert.Null(value);
        Assert.Single(cut.FindAll(".combo-dropdown"));

        await Search(cut).KeyDownAsync(ArrowDown);
        await Search(cut).KeyDownAsync(Enter);
        Assert.Equal("Cherry", value);
    }

    [Fact]
    public async Task Select_DisabledItems_AreReachedWithArrows_AndEnterKeepsTheDropdownOpen()
    {
        IReadOnlyList<string>? values = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.MultiSelect, true)
            .Add(c => c.ItemDisabledFunc, x => x == "Banana")
            .Bind(c => c.Values, values, v => values = v));

        await OpenSelect(cut);
        await Trigger(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["Banana"], HighlightedTexts(cut));

        await Trigger(cut).KeyDownAsync(Space);
        await Trigger(cut).KeyDownAsync(Enter);
        // The Enter keydown is followed by the button's click, which must not close the dropdown.
        await Trigger(cut).ClickAsync(new MouseEventArgs());

        Assert.Null(values);
        Assert.Single(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task Select_SingleSelect_EnterOnDisabledItem_DoesNothing()
    {
        string? value = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.ItemDisabledFunc, x => x == "Cherry")
            .Bind(c => c.Value, value, v => value = v));

        await OpenSelect(cut);
        await Trigger(cut).KeyDownAsync(ArrowDown);
        await Trigger(cut).KeyDownAsync(ArrowDown);
        Assert.Equal(["Cherry"], HighlightedTexts(cut));
        Assert.Equal("true", cut.Find(".combo-item.highlighted").GetAttribute("aria-disabled"));

        await Trigger(cut).KeyDownAsync(Enter);
        await Trigger(cut).ClickAsync(new MouseEventArgs());
        Assert.Null(value);
        Assert.Single(cut.FindAll(".combo-dropdown"));
    }

    [Fact]
    public async Task ArrowKeys_ScrollTheListToTheNewlyHighlightedRow_ById()
    {
        var select = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits));
        await OpenSelect(select);
        await Trigger(select).KeyDownAsync(ArrowDown);
        await Trigger(select).KeyDownAsync(ArrowDown);

        var selectCall = JSInterop.Invocations["comboBoxScrollToHighlighted"].Last();
        Assert.Equal(select.Find(".combo-item.highlighted").Id, selectCall.Arguments[2]);
        Assert.Equal(["Cherry"], HighlightedTexts(select));

        var combo = RenderFruits();
        await OpenCombobox(combo);
        await Search(combo).KeyDownAsync(ArrowDown);

        var comboCall = JSInterop.Invocations["comboBoxScrollToHighlighted"].Last();
        Assert.Equal(combo.Find(".combo-item.highlighted").Id, comboCall.Arguments[2]);
    }

    // 12. Escape in the search

    [Fact]
    public async Task Escape_WithSearchText_ClearsFirst_ThenCloses()
    {
        var reported = new List<string?>();
        var cut = RenderFruits(p => p.Add(c => c.SearchTextChanged, (string? text) => reported.Add(text)));

        await OpenCombobox(cut);
        await Search(cut).InputAsync(new ChangeEventArgs { Value = "Ch" });
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);

        await Search(cut).KeyDownAsync(Escape);
        Assert.Single(cut.FindAll(".combo-dropdown"));
        Assert.Equal("", Search(cut).GetAttribute("value"));
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
        Assert.Equal(["Ch", ""], reported);

        await Search(cut).KeyDownAsync(Escape);
        Assert.Empty(cut.FindAll(".combo-dropdown"));
        AssertFocusedLast(cut.Instance, "_triggerRef");
    }

    [Fact]
    public async Task Escape_WithEmptySearch_ClosesRightAway()
    {
        var cut = RenderFruits();
        await OpenCombobox(cut);

        await Search(cut).KeyDownAsync(Escape);

        Assert.Empty(cut.FindAll(".combo-dropdown"));
    }

    // 13. The search after a pick

    [Fact]
    public async Task SingleSelect_WithBoundSearchText_ReopensWithAnEmptySearch()
    {
        string? searchText = null;
        string? value = null;
        var cut = RenderFruits(p => p
            .Bind(c => c.SearchText, searchText, text => searchText = text)
            .Bind(c => c.Value, value, v => value = v));

        await OpenCombobox(cut);
        await Search(cut).InputAsync(new ChangeEventArgs { Value = "Ch" });
        await cut.Find(".combo-item").ClickAsync(new MouseEventArgs());
        Assert.Equal("Cherry", value);
        Assert.Equal("", searchText);

        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, searchText));
        await OpenCombobox(cut);
        Assert.Equal("", Search(cut).GetAttribute("value"));
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
    }

    [Fact]
    public async Task MultiSelect_KeepsTheSearchAfterAPick()
    {
        IReadOnlyList<string>? values = null;
        var cut = RenderFruits(p => p.Add(c => c.MultiSelect, true).Bind(c => c.Values, values, v => values = v));

        await OpenCombobox(cut);
        await Search(cut).InputAsync(new ChangeEventArgs { Value = "an" });
        await Search(cut).KeyDownAsync(Enter);

        Assert.Equal(["Banana"], values);
        Assert.Equal("an", Search(cut).GetAttribute("value"));
        Assert.Single(cut.FindAll(".combo-item"));
    }

    // 14. Mouse highlight

    [Fact]
    public async Task PointerMove_HighlightsTheRowUnderTheMouse_ButNotADisabledOne()
    {
        var cut = RenderFruits(p => p.Add(c => c.ItemDisabledFunc, x => x == "Banana"));
        await OpenCombobox(cut);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
        var ids = cut.FindAll(".combo-item").Select(e => e.Id!).ToArray();

        await cut.InvokeAsync(() => cut.Instance.HighlightOptionFromPointer(ids[2]));
        Assert.Equal(["Cherry"], HighlightedTexts(cut));
        Assert.Equal(ids[2], Search(cut).GetAttribute("aria-activedescendant"));

        await cut.InvokeAsync(() => cut.Instance.HighlightOptionFromPointer(ids[1]));
        Assert.Equal(["Cherry"], HighlightedTexts(cut));

        // The keyboard continues from where the mouse left the highlight.
        await Search(cut).KeyDownAsync(ArrowUp);
        Assert.Equal(["Banana"], HighlightedTexts(cut));
    }

    [Fact]
    public async Task Select_PointerMove_HighlightsTheRowUnderTheMouse()
    {
        var cut = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits));
        await OpenSelect(cut);
        var ids = cut.FindAll(".combo-item").Select(e => e.Id!).ToArray();

        await cut.InvokeAsync(() => cut.Instance.HighlightOptionFromPointer(ids[1]));

        Assert.Equal(["Banana"], HighlightedTexts(cut));
    }

    // 15. FilterFunc

    [Fact]
    public async Task FilterFunc_ReplacesTheDefaultContainsMatch()
    {
        var people = new List<SearchResult>(SearchResult.All);
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.StaticData, people)
            .Add(c => c.ToStringFunc, x => x.Name)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.FilterFunc, (x, search) =>
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Details.Contains(search, StringComparison.OrdinalIgnoreCase)));

        await OpenCombobox(cut);
        await Search(cut).InputAsync(new ChangeEventArgs { Value = "example.com" });

        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 3);
    }

    [Fact]
    public async Task DefaultFilter_MatchesAnywhereInTheText()
    {
        var cut = RenderFruits();
        await OpenCombobox(cut);

        await Search(cut).InputAsync(new ChangeEventArgs { Value = "ERR" });

        Assert.Equal(["Cherry"], cut.FindAll(".combo-item").Select(e => e.TextContent.Trim()));
    }

    // 16. The first hit is active after a search

    [Fact]
    public async Task AfterASearch_EnterPicksTheFirstEnabledHit()
    {
        string? value = null;
        var cut = RenderComponent<Combobox<string>>(p => p
            .Add(c => c.StaticData, ["Banana", "Bandana", "Cherry"])
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.ItemDisabledFunc, x => x == "Banana")
            .Bind(c => c.Value, value, v => value = v));

        await OpenCombobox(cut);
        await Search(cut).InputAsync(new ChangeEventArgs { Value = "an" });
        Assert.Equal(["Bandana"], HighlightedTexts(cut));

        await Search(cut).KeyDownAsync(Enter);

        Assert.Equal("Bandana", value);
    }

    // 17. StartContent

    [Fact]
    public void StartContent_IsRenderedBeforeThePlaceholderAndTheValue()
    {
        RenderFragment icon = b => b.AddMarkupContent(0, "<svg class=\"user-icon\"></svg>");

        var empty = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.StartContent, icon));
        Assert.Equal(["combo-start-content", "combo-trigger-content", "combo-chevron"],
            Trigger(empty).Children.Select(e => e.ClassList.First()));

        var selected = RenderFruits(p => p.Add(c => c.StartContent, icon).Add(c => c.Value, "Apple"));
        Assert.NotNull(Trigger(selected).QuerySelector(".combo-start-content .user-icon"));
        Assert.Equal("combo-start-content", Trigger(selected).Children.First().ClassList.First());
    }

    // 19. DropdownMinWidth / DropdownMaxWidth

    [Fact]
    public async Task DropdownMinAndMaxWidth_UseTheResolvedPlacement()
    {
        JSInterop.Setup<Portal.Position>("portalHelper.getPosition", i => i.Arguments.Count == 4)
            .SetResult(new Portal.Position { Top = 40, Left = 12, Width = 320, Resolved = true });

        var cut = RenderComponent<Select<string>>(p => p
            .Add(c => c.Items, Fruits)
            .Add(c => c.AnchorPosition, DropdownAnchorPosition.Right)
            .Add(c => c.DropdownMinWidth, "300px")
            .Add(c => c.DropdownMaxWidth, "480px"));

        await OpenSelect(cut);

        var call = JSInterop.Invocations["portalHelper.getPosition"].Last();
        var options = Assert.IsType<Portal.WidthOptions>(call.Arguments[3]);
        Assert.Equal(new Portal.WidthOptions(null, "300px", "480px", "Right"), options);

        var style = cut.Find(".combo-dropdown").ParentElement!.GetAttribute("style")!;
        Assert.Contains("left:12.0px", style.Replace(" ", ""));
        Assert.Contains("width:320.0px", style.Replace(" ", ""));
        Assert.Contains("transform:none", style.Replace(" ", ""));
    }

    [Fact]
    public async Task WithoutMinOrMaxWidth_ThePlacementIsUnchanged()
    {
        var cut = RenderComponent<Select<string>>(p => p.Add(c => c.Items, Fruits).Add(c => c.AnchorPosition, DropdownAnchorPosition.Right));

        await OpenSelect(cut);

        Assert.Equal(3, JSInterop.Invocations["portalHelper.getPosition"].Last().Arguments.Count);
        var style = cut.Find(".combo-dropdown").ParentElement!.GetAttribute("style")!.Replace(" ", "");
        Assert.Contains("left:300.0px", style);
        Assert.Contains("transform:translateX(-100%)", style);
    }
}
