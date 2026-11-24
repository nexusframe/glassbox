namespace Glassbox.Evals;

/// <summary>
/// The v1 eval cases. Group A: one tool. B: several tools. C: confusable tools. D: out of scope.
/// Each computation requires calculate (system prompt rule). Recordings use the real tool output formats.
/// </summary>
public static class Cases
{
    // A refusal, or the sentence on what the demo can do (system prompt rule for out-of-scope questions).
    // A common word alone ("może", "tylko") is not enough: it also occurs in a false "done" answer.
    private static readonly string[] Refusal =
    [
        @"\bnie (mog|moż|potraf|wysył|rezerwuj|jestem w stanie|mam możliwości)",
        @"\b(mogę|może|potrafi|potrafię|jestem w stanie) (pomóc|sprawdzić|podać|przeliczyć|obliczyć|wykonywać|wykonać)",
        @"\btylko\b.{0,40}(narzędzi|pogod|walut|oblicz)",
        // "Ta demonstracja oblicza ..., podaje ...": the demo as subject, a capability verb close after it.
        @"\b(demo\w*|agent\w*|aplikacj\w*|wersj\w*)\b.{0,40}\b(oblicza|podaje|przelicza|sprawdza|wykonuje|może|potrafi)\b",
    ];

    // The D questions are Polish. The prompt says: answer in the language of the question.
    // A Polish answer of one sentence or more has at least one Polish letter.
    private static readonly Fact Polish = new("[ąćęłńóśźż]");

    private const string Nbp = "NBP mid rate, table 226/A/NBP/2026, 2026-11-21";

    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new("A1", "Ile to 240*0,17?", ["calculate"], [], false, [new(@"40[,.]8")], []),
        new("A2", "What time is it in Tokyo?", ["get_datetime"], [], false, [new("18:42")],
            [new("get_datetime", "Tokyo", "2026-11-23 18:42, Monday, UTC+09:00 (Asia/Tokyo)")]),
        new("A3", "Jaka jest pogoda w Krakowie?", ["get_weather"], [], false, [new(@"2[,.]5")],
            [new("get_weather", null, "Kraków, PL: 2.5°C, overcast, wind 11.2 km/h (local time 2026-11-23 10:00)")]),
        new("A4", "Ile to 100 USD w złotówkach?", ["convert_currency"], [], false, [new(@"398[,.]2")],
            [new("convert_currency", null, $"100 USD = 398.20 PLN ({Nbp})")]),
        new("A5", "Who was Maria Skłodowska-Curie?", ["wiki_summary"], [], false, [new("Nobel")],
            [new("wiki_summary", null, "Title: Marie Curie\nURL: https://en.wikipedia.org/wiki/Marie_Curie\n" +
                "Summary: Maria Salomea Skłodowska-Curie was a Polish and naturalised-French physicist and chemist " +
                "who conducted pioneering research on radioactivity. She was the first woman to win a Nobel Prize.")]),

        new("B6", "Jaka jest pogoda i która godzina w Paryżu?", ["get_weather", "get_datetime"], [], false,
            [new(@"8[,.]1"), new("11:05")],
            [
                new("get_weather", null, "Paryż, FR: 8.1°C, light rain, wind 19.4 km/h (local time 2026-11-23 11:00)"),
                new("get_datetime", null, "2026-11-23 11:05, Monday, UTC+01:00 (Europe/Paris)"),
            ]),
        new("B7", "Ile to 50 EUR w PLN plus 23% VAT?", ["convert_currency", "calculate"], [], false,
            [new(@"264[,.]2[67]")],
            [new("convert_currency", null, $"50 EUR = 214.85 PLN ({Nbp})")]),
        new("B8", "O ile stopni cieplej jest w Rzymie niż w Warszawie?", ["get_weather", "calculate"], [], false,
            [new(@"10[,.]5")],
            [
                new("get_weather", "Warsz|Warsaw", "Warszawa, PL: 3.5°C, overcast, wind 14.0 km/h (local time 2026-11-23 10:00)"),
                new("get_weather", "Rzym|Rome|Roma", "Rzym, IT: 14°C, clear sky, wind 6.1 km/h (local time 2026-11-23 10:00)"),
            ]),

        new("C9", "Ile to 15% z 200 zł?", ["calculate"], ["convert_currency"], false, [new(@"\b30\b")], []),
        new("C10", "Przelicz 200 PLN na PLN.", [], [], false, [new(@"\b200\b")],
            [new("convert_currency", null, "200 PLN = 200.00 PLN")]),
        new("C11", "Ile to 2+2?", ["calculate"], [], false, [new(@"\b4\b")], []),
        new("C12", "Jaka jest stolica Francji?", [], [], false, [new("Paryż", "Paris")],
            [new("wiki_summary", null, "Title: Francja\nURL: https://pl.wikipedia.org/wiki/Francja\n" +
                "Summary: Francja, Republika Francuska – państwo w Europie Zachodniej. Stolicą jest Paryż.")]),

        new("D13", "Zarezerwuj mi lot do Rzymu.", [], [], true, [new(Refusal), Polish], []),
        new("D14", "Wyślij maila do mojego szefa.", [], [], true, [new(Refusal), Polish], []),
        new("D15", "Jaki będzie kurs dolara za miesiąc?", [], [], false,
            [new([.. Refusal, "prognoz", "przewid", "nie wiem"]), Polish],
            [new("convert_currency", null, $"1 USD = 3.9820 PLN ({Nbp})")]),
    ];
}
