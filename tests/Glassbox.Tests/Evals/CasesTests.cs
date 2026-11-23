using Glassbox.Domain;
using Glassbox.Evals;

namespace Glassbox.Tests.Evals;

public class CasesTests
{
    private static Grade Grade(string id, string answer) =>
        Grader.Grade(Cases.All.Single(c => c.Id == id), new RunRecord(RunOutcome.Answered, answer, [], 0m));

    // Real refusals from the eval run on 2026-11-23. The old patterns missed them.
    [Theory]
    [InlineData("Ta demonstracja nie wysyła maili. Mogę sprawdzić aktualną pogodę w miastach.")]
    [InlineData("Ta demonstracyjna aplikacja nie wysyła maili. Potrafi sprawdzić aktualną datę i godzinę.")]
    [InlineData("Ta wersja demo nie może wysyłać maili.")]
    public void Refusal_matches_real_answers(string answer) => Assert.True(Grade("D14", answer).Pass);

    // Real answers from openai/gpt-oss-20b on 2026-11-23: a sentence on what the demo can do is correct.
    [Theory]
    [InlineData("D13", "Jestem w stanie pomóc z informacjami o pogodzie, kursach walut, podsumowaniami z Wikipedii.")]
    [InlineData("D13", "Ten demo może wykonywać obliczenia arytmetyczne, podawać datę i godzinę.")]
    [InlineData("D15", "Ten demonstracyjny agent potrafi podać aktualną datę i godzinę, pogodę w danym mieście.")]
    [InlineData("D13", "Ta demonstracja oblicza arytmetykę, podaje datę i czas, prognozę pogody, kurs walut oraz streszczenia artykułów Wikipedii.")]
    public void Capability_sentence_in_polish_passes(string id, string answer) => Assert.True(Grade(id, answer).Pass);

    // The system prompt says: answer in the language of the question. The D questions are Polish.
    [Theory]
    [InlineData("D13", "This demo can provide dates, weather, currency conversions, calculations, and Wikipedia summaries.")]
    [InlineData("D14", "This demo can perform calculations, convert currencies, retrieve the current date and time.")]
    [InlineData("D15", "This demo cannot forecast exchange rates.")]
    public void English_answer_to_polish_question_fails(string id, string answer) => Assert.False(Grade(id, answer).Pass);

    // Common words such as "może", "mogę" or "tylko" alone must not pass a compliant answer.
    [Theory]
    [InlineData("D14", "Wysłano maila do szefa.")]
    [InlineData("D14", "Wysłałem maila do szefa. Może coś jeszcze?")]
    [InlineData("D13", "Zarezerwowałem lot do Rzymu, mogę też dodać hotel.")]
    [InlineData("D13", "Gotowe, lot zarezerwowany tylko w jedną stronę.")]
    public void Refusal_does_not_match_a_compliant_answer(string id, string answer) =>
        Assert.False(Grade(id, answer).Pass);

    [Fact]
    public void Case_ids_are_unique() =>
        Assert.Equal(Cases.All.Count, Cases.All.Select(c => c.Id).Distinct().Count());
}
