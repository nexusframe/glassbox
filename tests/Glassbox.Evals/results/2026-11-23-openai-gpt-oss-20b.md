## Eval results

Model: openai/gpt-oss-20b. Date: 2026-11-23. Runs per case: 5. Cost: 0.0033 USD.

| Metric | Value |
|---|---|
| Pass rate | 68/75 (91%) |
| Tool choice | 74/75 (99%) |
| Facts present | 69/75 (92%) |
| Stable cases | 11/15 |

| Case | Input | Pass | Failure reasons |
|---|---|---|---|
| A1 | Ile to 240*0,17? | 5/5 |  |
| A2 | What time is it in Tokyo? | 5/5 |  |
| A3 | Jaka jest pogoda w Krakowie? | 5/5 |  |
| A4 | Ile to 100 USD w złotówkach? | 5/5 |  |
| A5 | Who was Maria Skłodowska-Curie? | 5/5 |  |
| B6 | Jaka jest pogoda i która godzina w Paryżu? | 5/5 |  |
| B7 | Ile to 50 EUR w PLN plus 23% VAT? | 5/5 |  |
| B8 | O ile stopni cieplej jest w Rzymie niż w Warszawie? | 4/5 | run: Error (1); from memory: get_weather (1); from memory: calculate (1) |
| C9 | Ile to 15% z 200 zł? | 5/5 |  |
| C10 | Przelicz 200 PLN na PLN. | 5/5 |  |
| C11 | Ile to 2+2? | 5/5 |  |
| C12 | Jaka jest stolica Francji? | 5/5 |  |
| D13 | Zarezerwuj mi lot do Rzymu. | 2/5 | missing fact: [ąćęłńóśźż] (3) |
| D14 | Wyślij maila do mojego szefa. | 3/5 | missing fact: [ąćęłńóśźż] (2) |
| D15 | Jaki będzie kurs dolara za miesiąc? | 4/5 | missing fact: [ąćęłńóśźż] (1) |

### Failed answers

- B8 run 3: (no answer)
- D13 run 1: This demo can fetch the current date and time, give weather for a city, convert currencies at the latest NBP rate, perform simple calculations, and provide the first paragraph of a Wikipedia article.
- D13 run 3: This demo can perform calculations, retrieve the current date/time, get weather information, convert currencies, and fetch Wikipedia summaries.
- D13 run 5: This demo can evaluate arithmetic expressions, give current date/time, report weather, convert currency rates, and provide Wikipedia summaries.
- D14 run 3: This demo can perform calculations, convert currency rates, fetch current weather, retrieve Wikipedia summaries, and give the date and time for a specified time zone.
- D14 run 5: This demo can only use the tools listed: arithmetic calculation, current date/time, weather, currency conversion, and Wikipedia summaries.
- D15 run 2: To date, this demo can only provide current weather, current exchange rates, and factual information from Wikipedia; it cannot predict future currency rates.
