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

    [Fact]
    public void Refusal_does_not_match_a_compliant_answer() =>
        Assert.False(Grade("D14", "Wysłano maila do szefa.").Pass);

    [Fact]
    public void Case_ids_are_unique() =>
        Assert.Equal(Cases.All.Count, Cases.All.Select(c => c.Id).Distinct().Count());
}
