using System;
using System.Text;

namespace Anon;

public static class Names
{
    private const int RealShare = 30;

    private static readonly string[] FirstNames =
    [
        "Alice", "Aiden", "Amelia", "Ben", "Clara", "Daniel", "Devon", "Elena", "Eleanor", "Felix",
        "Fiona", "Gabriel", "George", "Hannah", "Hilda", "Ivan", "Isaac", "Jasmine", "Jacob", "Karen",
        "Kelechi", "Kevin", "Leah", "Lila", "Marcus", "Martin", "Nadia", "Nadir", "Olivia", "Oscar",
        "Pablo", "Peter", "Priya", "Quinn", "Rachel", "Rosa", "Samuel", "Sarah", "Sofia", "Tara",
        "Tom", "Uma", "Ulrich", "Vera", "Vivian", "Walter", "Xena", "Yolanda", "Zachary", "Chloe",
    ];

    private static readonly string[] LastNames =
    [
        "Allen", "Baker", "Becker", "Brown", "Calloway", "Carter", "Chen", "Clark", "Diaz", "Davis",
        "Dominguez", "Evans", "Feld", "Foster", "Garcia", "Grant", "Hale", "Hart", "Hoffman", "Ivanov",
        "Jacobs", "Khan", "Lane", "Liang", "Malik", "Miller", "Nair", "Osei", "Okafor", "Patel",
        "Quinn", "Renner", "Russo", "Silva", "Smith", "Sorensen", "Tanaka", "Vance", "Weber", "Wells",
        "Young", "Zhou", "Nguyen", "Reyes", "Sato", "Kim", "Larsen", "Bauer", "Mori", "Adebayo",
    ];

    private static readonly string[] Onsets =
    [
        "b", "c", "ch", "d", "dr", "f", "g", "gr", "k", "l", "m", "n", "p", "r", "rh",
        "s", "sh", "st", "t", "th", "tr", "v", "w", "y", "z",
    ];

    private static readonly string[] Cores =
    [
        "a", "e", "i", "o", "u", "ai", "ia", "io", "ea", "au", "oo",
    ];

    private static readonly string[] Codas =
    [
        "", "", "", "n", "r", "l", "s", "m", "th", "d", "k", "nd", "st",
    ];

    public static string Create(Random random)
    {
        if (random.Next(100) < RealShare)
        {
            var first = Pick(random, FirstNames);
            var last = Pick(random, LastNames);
            while (first == last) last = Pick(random, LastNames);
            return $"{first} {last}";
        }

        return $"{Word(random)} {Word(random)}";
    }

    public static string Fake(string name, int seed) => Create(new Random(Seed(name, seed)));

    public static int Seed(string name, int seed)
    {
        unchecked
        {
            var hash = 17 + seed;
            foreach (var letter in name) hash = (hash * 31) + letter;
            return hash;
        }
    }

    private static string Word(Random random)
    {
        const int Max = 9;
        var target = random.Next(100) switch { < 65 => 2, < 90 => 3, _ => 1 };

        var word = string.Empty;
        for (var i = 0; i < target; i++)
        {
            var part = Pick(random, Onsets) + Pick(random, Cores);
            if (i == target - 1) part += Pick(random, Codas);

            if (word.Length + part.Length > Max && i > 0) break;
            word += part;
        }

        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    private static string Pick(Random random, string[] table) => table[random.Next(table.Length)];

    private static bool StandsAlone(string text, string name, int at) =>
        !(at > 0 && char.IsLetter(text[at - 1]))
        && !(at + name.Length < text.Length && char.IsLetter(text[at + name.Length]));

    public static bool EveryMentionStandsAlone(string text, string name)
    {
        if (name.Length == 0) return false;

        for (var at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
        {
            if (!StandsAlone(text, name, at)) return false;
        }

        return true;
    }

    public static string ReplaceWholeWords(string text, string name, string with)
    {
        if (name.Length == 0) return text;

        var builder = new StringBuilder(text.Length);
        var copied = 0;
        var changed = false;
        for (var at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
        {
            if (!StandsAlone(text, name, at)) continue;

            builder.Append(text, copied, at - copied).Append(with);
            copied = at + name.Length;
            changed = true;
        }

        return changed ? builder.Append(text, copied, text.Length - copied).ToString() : text;
    }
}
