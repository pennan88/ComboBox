using AngleSharp.Dom;
using Bunit;
using ComboBox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Xunit;

namespace ComboBox.Tests;

public class LoadingAndSearchTextTests : ComboTestContext
{
    /// <summary>
    /// A data provider whose calls stay pending until the test completes them.
    /// </summary>
    private sealed class PendingProvider
    {
        public List<(string? Search, TaskCompletionSource<ItemsProviderResult<SearchResult>> Result)> Calls { get; } = [];

        public ValueTask<ItemsProviderResult<SearchResult>> Invoke(ComboState state, CancellationToken token)
        {
            var result = new TaskCompletionSource<ItemsProviderResult<SearchResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => result.TrySetCanceled(token));
            Calls.Add((state.Search, result));
            return new ValueTask<ItemsProviderResult<SearchResult>>(result.Task);
        }

        public void Complete(int call)
        {
            var search = Calls[call].Search;
            var items = SearchResult.All
                .Where(x => string.IsNullOrEmpty(search) || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Type)
                .ToList();
            Calls[call].Result.SetResult(new ItemsProviderResult<SearchResult>(items, items.Count));
        }
    }

    private static IElement TriggerInput(IRenderedFragment cut) => cut.Find("input.combo-trigger-input");

    private IRenderedComponent<Combobox<SearchResult>> RenderSearchBox(PendingProvider provider) =>
        RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, provider.Invoke)
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Add(c => c.GroupBy, x => x.Type)
            .Add(c => c.GroupFooterTemplate, g => b => b.AddMarkupContent(0, $"<button class=\"more\">More {g.Key}</button>"))
            .Add(c => c.FooterTemplate, b => b.AddContent(0, "Keyboard hints"))
            .Add(c => c.NoResultsTemplate, b => b.AddContent(0, "Nothing here")));

    [Fact]
    public void FirstLoad_ShowsOnlyTheLoadingRow()
    {
        var provider = new PendingProvider();
        var cut = RenderSearchBox(provider);

        _ = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "A" });
        cut.WaitForState(() => provider.Calls.Count == 1);

        // The loading row sits in the results area; footers and "no results" wait for the provider.
        var loadingRow = cut.WaitForElement(".combo-content-container > .combo-loading-row");
        Assert.NotNull(loadingRow.QuerySelector(".progress"));
        Assert.Empty(cut.FindAll(".combo-footer"));
        Assert.Empty(cut.FindAll(".combo-group-footer"));
        Assert.Empty(cut.FindAll(".combo-no-results"));
        Assert.DoesNotContain("combo-progress-active", cut.Find(".combo-progress-track").ClassList);
        Assert.Contains("Loading results", cut.Find(".combo-sr-only").TextContent);

        provider.Complete(0);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count > 0);

        Assert.Empty(cut.FindAll(".combo-loading-row"));
        Assert.Contains("Keyboard hints", cut.Find(".combo-footer").TextContent);
        Assert.NotEmpty(cut.FindAll(".combo-group-footer .more"));
        Assert.DoesNotContain("Loading results", cut.Find(".combo-sr-only").TextContent);
    }

    [Fact]
    public void FirstLoad_WithNoResults_ShowsNoResultsTemplateOnlyAfterTheProviderReturns()
    {
        var provider = new PendingProvider();
        var cut = RenderSearchBox(provider);

        _ = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "zzz" });
        cut.WaitForElement(".combo-loading-row");
        Assert.Empty(cut.FindAll(".combo-no-results"));

        provider.Complete(0);
        cut.WaitForElement(".combo-no-results");
        Assert.Empty(cut.FindAll(".combo-loading-row"));
        Assert.Single(cut.FindAll(".combo-footer"));
    }

    [Fact]
    public async Task Reload_KeepsThePreviousResultsAndShowsTheProgressBar()
    {
        var provider = new PendingProvider();
        var cut = RenderSearchBox(provider);

        _ = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "A" });
        cut.WaitForState(() => provider.Calls.Count == 1);
        provider.Complete(0);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count > 0);
        var shown = cut.FindAll(".combo-item").Select(e => e.TextContent).ToList();

        // RefreshAsync: same results stay on screen, only the thin bar shows the reload.
        var refresh = cut.Instance.RefreshAsync();
        cut.WaitForState(() => provider.Calls.Count == 2);
        Assert.Equal(shown, cut.FindAll(".combo-item").Select(e => e.TextContent));
        Assert.Empty(cut.FindAll(".combo-loading-row"));
        Assert.Contains("combo-progress-active", cut.Find(".combo-progress-track").ClassList);
        Assert.Single(cut.FindAll(".combo-footer"));
        Assert.Contains("Loading results", cut.Find(".combo-sr-only").TextContent);

        provider.Complete(1);
        await refresh;
        Assert.DoesNotContain("combo-progress-active", cut.Find(".combo-progress-track").ClassList);

        // Typing more behaves the same way.
        _ = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "At" });
        cut.WaitForState(() => provider.Calls.Count == 3);
        Assert.Equal(shown, cut.FindAll(".combo-item").Select(e => e.TextContent));
        Assert.Empty(cut.FindAll(".combo-loading-row"));
        Assert.Contains("combo-progress-active", cut.Find(".combo-progress-track").ClassList);

        provider.Complete(2);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);
        Assert.DoesNotContain("combo-progress-active", cut.Find(".combo-progress-track").ClassList);
    }

    [Fact]
    public async Task SearchText_UnchangedNull_DoesNotOverwriteTyping()
    {
        var cut = RenderComponent<NullSearchTextHost>();
        var host = cut.Instance;

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        var renders = host.RenderCount;

        Assert.Equal(["Atl"], host.Emitted);
        Assert.True(renders > 1, "The host should have re-rendered after SearchTextChanged.");
        Assert.Equal("Atl", TriggerInput(cut).GetAttribute("value"));
        Assert.Equal(["Atl"], host.Searches);
        cut.WaitForState(() => cut.FindAll(".combo-item").Count == 1);

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atla" });

        Assert.Equal(["Atl", "Atla"], host.Emitted);
        Assert.True(host.RenderCount > renders);
        Assert.Equal("Atla", TriggerInput(cut).GetAttribute("value"));
        Assert.Equal(["Atl", "Atla"], host.Searches);
    }

    [Fact]
    public async Task SearchText_EchoOfTheEmittedValue_DoesNotTouchTheInput()
    {
        var searches = new List<string?>();
        var emitted = new List<string?>();
        Func<ComboState, CancellationToken, ValueTask<ItemsProviderResult<SearchResult>>> provider = (state, _) =>
        {
            searches.Add(state.Search);
            return ValueTask.FromResult(new ItemsProviderResult<SearchResult>([], 0));
        };
        var cut = RenderComponent<Combobox<SearchResult>>(p => p
            .Add(c => c.DataProvider, provider)
            .Add(c => c.SearchInTrigger, true)
            .Add(c => c.Virtualize, false)
            .Add(c => c.SearchDebounceMs, 0)
            .Bind(c => c.SearchText, null, text => emitted.Add(text)));

        await TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atl" });
        Assert.Equal(["Atl"], emitted);
        Assert.Equal(["Atl"], searches);

        // The user keeps typing (still inside the debounce) when the parent's echo of "Atl" arrives.
        cut.SetParametersAndRender(p => p.Add(c => c.SearchDebounceMs, 60_000));
        _ = TriggerInput(cut).InputAsync(new ChangeEventArgs { Value = "Atla" });
        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "Atl"));

        Assert.Equal("Atla", TriggerInput(cut).GetAttribute("value"));
        Assert.Equal(["Atl"], searches);

        // Re-rendering with the same value changes nothing either.
        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "Atl"));
        Assert.Equal("Atla", TriggerInput(cut).GetAttribute("value"));
        Assert.Equal(["Atl"], searches);

        // A real change from the parent is still applied, without searching.
        cut.SetParametersAndRender(p => p.Add(c => c.SearchText, "Apollo"));
        Assert.Equal("Apollo", TriggerInput(cut).GetAttribute("value"));
        Assert.Equal(["Atl"], searches);
    }

    [Fact]
    public void AddOptionPosition_DefaultsToTop()
    {
        Assert.Equal(AddOptionPosition.Top, new Combobox<string>().AddOptionPosition);
    }
}
