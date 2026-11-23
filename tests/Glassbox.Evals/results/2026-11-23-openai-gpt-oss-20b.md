## Eval results

Model: openai/gpt-oss-20b. Date: 2026-11-23. Runs per case: 5. Cost: 0.0031 USD.

| Metric | Value |
|---|---|
| Pass rate | 64/75 (85%) |
| Tool choice | 72/75 (96%) |
| Facts present | 65/75 (87%) |
| Stable cases | 10/15 |

| Case | Input | Pass | Failure reasons |
|---|---|---|---|
| A1 | Ile to 240*0,17? | 5/5 |  |
| A2 | What time is it in Tokyo? | 4/5 | missing fact: 18:42 (1) |
| A3 | Jaka jest pogoda w Krakowie? | 5/5 |  |
| A4 | Ile to 100 USD w złotówkach? | 5/5 |  |
| A5 | Who was Maria Skłodowska-Curie? | 5/5 |  |
| B6 | Jaka jest pogoda i która godzina w Paryżu? | 3/5 | from memory: get_datetime (2); missing fact: 11:05 (2) |
| B7 | Ile to 50 EUR w PLN plus 23% VAT? | 5/5 |  |
| B8 | O ile stopni cieplej jest w Rzymie niż w Warszawie? | 5/5 |  |
| C9 | Ile to 15% z 200 zł? | 4/5 | from memory: calculate (1) |
| C10 | Przelicz 200 PLN na PLN. | 5/5 |  |
| C11 | Ile to 2+2? | 5/5 |  |
| C12 | Jaka jest stolica Francji? | 5/5 |  |
| D13 | Zarezerwuj mi lot do Rzymu. | 1/5 | missing fact: \bnie (mog\|moż\|potraf\|wysył\|rezerwuj\|jestem w stanie\|mam możliwości) \| \b(mogę\|może\|potrafi\|potrafię\|jestem w stanie) (pomóc\|sprawdzić\|podać\|przeliczyć\|obliczyć\|wykonywać\|wykonać) \| \btylko\b.{0,40}(narzędzi\|pogod\|walut\|oblicz) \| \b(demo\w*\|agent\w*\|aplikacj\w*\|wersj\w*)\b.{0,40}\b(oblicza\|podaje\|przelicza\|sprawdza\|wykonuje\|może\|potrafi)\b (4); missing fact: [ąćęłńóśźż] (4) |
| D14 | Wyślij maila do mojego szefa. | 2/5 | missing fact: \bnie (mog\|moż\|potraf\|wysył\|rezerwuj\|jestem w stanie\|mam możliwości) \| \b(mogę\|może\|potrafi\|potrafię\|jestem w stanie) (pomóc\|sprawdzić\|podać\|przeliczyć\|obliczyć\|wykonywać\|wykonać) \| \btylko\b.{0,40}(narzędzi\|pogod\|walut\|oblicz) \| \b(demo\w*\|agent\w*\|aplikacj\w*\|wersj\w*)\b.{0,40}\b(oblicza\|podaje\|przelicza\|sprawdza\|wykonuje\|może\|potrafi)\b (3); missing fact: [ąćęłńóśźż] (3) |
| D15 | Jaki będzie kurs dolara za miesiąc? | 5/5 |  |

### Failed answers

- A2 run 3: 23:42 (Tuesday) in Tokyo.
- B6 run 2: Paryż: 8,1 °C, lekkie deszcze, wiatr 19,4 km/h (lokalny czas 2026‑11‑23 11:00).
- B6 run 3: W Paryżu jest 8,1 °C, lekki deszcz, wiatr 19,4 km/h – aktualnie godzina 11:00.
- C9 run 3: 30 zł
- D13 run 1: This demo can perform arithmetic, retrieve the current datetime or weather, convert currencies, or fetch Wikipedia summaries.
- D13 run 3: This demo can perform calculations, give the current weather, convert currency, provide the current date and time, and fetch a first paragraph from Wikipedia.
- D13 run 4: This demo can provide the current date/time, weather, currency conversion, perform calculations, or give a summary from Wikipedia.
- D13 run 5: This demo can retrieve dates and times, weather, currency conversion, Wikipedia summaries and perform arithmetic.
- D14 run 2: I can perform calculations, fetch weather and exchange rates, and provide Wikipedia summaries.
- D14 run 4: This demo can evaluate arithmetic, report weather, provide current time, convert currencies, and fetch Wikipedia summaries.
- D14 run 5: This demo agent can evaluate arithmetic, provide current date/time, weather, currency conversion, or retrieve a Wikipedia summary.
