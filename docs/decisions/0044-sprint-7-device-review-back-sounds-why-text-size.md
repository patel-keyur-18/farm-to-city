# 0044. Back between sheets, sounds only for events, a *Why?* only from its ?, text size 80–125% (Sprint 7 device review)

- Status: Accepted
- Date: 2026-10-20
- Deciders: Keyur Patel
- Amends: ADR 0043 §3 (how a *Why?* opens), §4 (the HUD's long-presses), §9 (the text sizes), §10 (the sounds); spec 008 §3.2, §3.3, §3.6, §3.13, §10
- Related: ADR 0043 (the Sprint 7 panels), ADR 0005 (layout), ADR 0028 and 0033 (land prices)
- GDD sections: UI/UX and screens (Mobile ergonomics, Accessibility), Art, audio and feel

## Context

The owner's device run of the Sprint 7 build (iPhone 13, 2026-10-20) passed except for seven points:

1. A sheet opened from another (the Menu's Store, the Store's mandi trip) had no way back, only the close button.
2. The tap sound on every button press was irritating.
3. The same as 1, for every sheet.
4. A *Why?* opened by itself in the Store. Two causes: a finger resting on a row while scrolling counted as a long-press (600 ms), and a slow tap on the HUD's store pill counted as its hold.
5. After repaying a loan in full, it still showed with "Repay" and 0 coins owing. The cause: the Repay sheet fixed the amount when it was built. Interest accrues every second, and a sheet whose text hasn't changed is not rebuilt, so the payment fell a fraction of a coin short and the loan stayed open.
6. "What you own minus what you owe" looked unrealistic (11,31,632 coins with 13,695 in cash).
7. The text looked bad at 150%.

## Decisions

1. **Back between sheets.** A sheet opened from another sheet (a row's button or a row tap) gets a round **←** back button at the left of its header, returning to the sheet it came from, as many steps as were taken. A sheet opened from the map, the HUD or a card starts fresh, with no back button. Closing the sheet or tapping the map forgets the way back. After a step, the sheet returns the way it came (an offer to its good, a loan or repayment to Money and loans, a purchase to Buy inputs). After finished work, the sheet closes, as before (Hay Day style).
2. **Sounds only for what happens in the town.** Sowing and planting (the rustle, lower), harvest, coins in, coins out, something ready, and an urgent alert. There is no sound for a button tap or for a sheet opening or closing. Their three clips are deleted.
3. **A *Why?* opens only from its ?.** A row no longer opens its *Why?* on a tap or a long-press. A row that opens a sheet (a good in the Store) still opens it on a tap. The HUD's cash and store pills are plain taps (Money and loans, the Store). Their *Why?* moves to the **?** on the Cash row and on a new "In the store" row at the top of the Store sheet. The time card's 3-second hold for the developer panel stays. Every number still has a *Why?* (spec 008 AC-15).
4. **Text size: Small 80%, Normal 100%, Large 125%.** 150% is removed. A saved 150% reads as Normal.
5. **Paying a loan pays what's owed when pressed.** "All of it" and "the next payment" work out the amount at the moment of the press, not when the sheet was drawn. Paying all of it closes the loan, and Money and loans then shows "No loans · You owe nothing".
6. **Net worth is right and stays as it is. Land is shown on its own row under it.** In a new town it is 11,50,735 coins: 9 acres of land at today's price (the 6 inherited farm acres, the 2-acre yard and the labour quarters, about 1,17,600 an acre: 10,55,664), buildings 55,000, goods in store 20,071, living costs paid ahead 16,500, and cash 3,500.
   - The land price is ADR 0028's and ADR 0033's (1,20,000 an acre, scaled by soil and fertility). Land is most of what a smallholder owns, and real farmland in India costs far more an acre than this.
   - Being land-rich and cash-poor is the lesson, so the number stays. Money and loans adds "Of that, your land" under it, so the size explains itself.

## Consequences

- `SheetPresenter` has `CanGoBack` and `OnBack`. `GameRoot` keeps the way back (`OpenSheet` starts fresh; `Push`, `Back` and `Return` move along the way back).
- `UiKit.OnPress`, `SheetPresenter.OnSlide` and `HudPresenter.OnWhy` are removed. `tap.ogg`, `sheet_open.ogg` and `sheet_close.ogg` are deleted, and `ASSETS_LICENSES.md` is updated.
- `SettingsIds.TextSizes` is { 80, 100, 125 }.
- `Repay.Build` creates its commands with the amount at press time. `PanelTests.Choices` repays all of it from a sheet built an hour earlier and checks that the loan closes, that Money and loans shows "No loans", and that the land row is there.
- `StoreSheet` opens with the "In the store" row, with its *Why?*, in place of the subtitle.
