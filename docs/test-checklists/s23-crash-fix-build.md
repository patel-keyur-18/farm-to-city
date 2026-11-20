# Sprint 23: the crash-fix Development build (2026-11-20)

Install over the app already on the phone (do not delete it first), so your town stays. Export your town before you install, as you did.

| # | Try | Expect | Report if |
| --- | --- | --- | --- |
| 1 | Open the app with the town that crashed | The HUD (money, forecast, people) draws; the town loads with its progress | The HUD is blank or the console shows errors |
| 2 | Open the weather (tap the sun, top left) | A forecast of runs for 3 days | It is empty or errors |
| 3 | Developer panel, **+1 hour** about 30 times, then **+1 day** twice | The HUD stays alive; the weather changes | Any step fails: **copy the top line of the Development Console and send it** (it names the system that failed) |
| 4 | Close the app for a minute and open it again | The away card or the HUD, no console errors | Errors on open |

What was fixed (ADR 0139): a failing step no longer stops the clock's hourly work, and a town saved in that state is mended when it loads. The first failing step is still unknown, so check 3 matters.
| 5 | Flour mill sheet, tap the ? on **Buy wheat at the market** | It says the trader takes 400 kg of atta and 100 kg of bran a day, the village does not buy from the mill, and the mill buys only while unsold stock is under 2 days of that | The text is missing |
| 6 | Switch **Buy wheat at the market** on and let a few days pass | The mill's unsold atta stays under about 800 kg; its cash is not all in stock | Stock grows past that, or cash stays 0 while profit is positive |
