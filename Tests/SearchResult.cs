namespace ComboBox.Tests;

public sealed record SearchResult(string Name, string Type, string Details)
{
    public static IReadOnlyList<SearchResult> All { get; } =
    [
        new("Apollo", "Grupp", "12 medlemmar"),
        new("Atlas", "Grupp", "4 medlemmar"),
        new("Andromeda", "Grupp", "7 medlemmar"),
        new("Anna Andersson", "Användare", "anna@example.com"),
        new("Albin Ek", "Användare", "albin@example.com"),
        new("Alva Berg", "Användare", "alva@example.com"),
        new("Alfa-projektet", "Projekt", "Startat 2024"),
        new("Arkivet", "Projekt", "Avslutat"),
        new("Aurora", "Projekt", "Pågående"),
        new("Bergakungen", "Projekt", "Pågående"),
    ];
}
