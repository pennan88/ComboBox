using Bunit;
using ComboBox.Components;

namespace ComboBox.Tests;

/// <summary>
/// bUnit context with the JS interop the combobox needs. <see cref="Portal"/> only renders its
/// content once <c>portalHelper.getPosition</c> returns a position, so that call is answered.
/// </summary>
public abstract class ComboTestContext : TestContext
{
    protected ComboTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<Portal.Position>("portalHelper.getPosition", _ => true)
            .SetResult(new Portal.Position { Top = 40, Left = 0, Width = 300 });
    }

    protected static string[] HighlightedTexts(IRenderedFragment cut) =>
        cut.FindAll(".highlighted").Select(e => e.TextContent.Trim()).ToArray();
}
