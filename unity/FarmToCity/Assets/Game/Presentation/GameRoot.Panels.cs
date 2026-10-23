using System;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Game.Services;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.View.Text;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The Sprint 7 panels and settings (spec 008 §3.3–§3.6, §3.13, ADR 0043): the HUD's and the sheets' wiring, every panel's sheet,
    /// the Why? card, and how the game looks and sounds (number format, text size, reduced motion, sound, haptics).
    /// </summary>
    public sealed partial class GameRoot
    {
        private Sounds _sounds;

        // ---- the settings file (spec 008 §3.13): PlayerPrefs, never in the town ----

        /// <summary>The text size in percent: 80, 100 or 125 (ADR 0044 §4); a size no longer offered (150) reads as 100.</summary>
        private static int TextSize { get { int t = PlayerPrefs.GetInt("look.text", 100); return Array.IndexOf(SettingsIds.TextSizes, t) >= 0 ? t : 100; } set { PlayerPrefs.SetInt("look.text", value); PlayerPrefs.Save(); } }
        private static bool SoundOn { get { return PlayerPrefs.GetInt("look.sound", 1) == 1; } set { PlayerPrefs.SetInt("look.sound", value ? 1 : 0); PlayerPrefs.Save(); } }
        private static bool HapticsOn { get { return PlayerPrefs.GetInt("look.haptics", 1) == 1; } set { PlayerPrefs.SetInt("look.haptics", value ? 1 : 0); PlayerPrefs.Save(); } }

        /// <summary>Reduced motion: the player's choice once made, iOS's Reduce Motion until then.</summary>
        private static bool ReducedMotion
        {
            get { return PlayerPrefs.HasKey("look.reducedMotion") ? PlayerPrefs.GetInt("look.reducedMotion") == 1 : NativeFeel.SystemReducesMotion; }
            set { PlayerPrefs.SetInt("look.reducedMotion", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Numbers: the player's choice once made, the phone's region until then (spec 008 §3.11).</summary>
        private NumberFormat Numbers
        {
            get { return PlayerPrefs.HasKey("look.numbers") ? (NumberFormat)PlayerPrefs.GetInt("look.numbers") : Formatter.FormatForRegion(RegionCode()); }
            set { PlayerPrefs.SetInt("look.numbers", (int)value); PlayerPrefs.Save(); }
        }

        private void ApplyLookAndSound()
        {
            _tweens.ReducedMotion = ReducedMotion;
            _sounds.Enabled = SoundOn;
            _document.panelSettings.scale = TextSize / 100f;
            if (Numbers != _format)
            {
                _format = Numbers;
                _fmt = new Formatter(_strings, _format);
                _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            }
        }

        private void Haptic() { if (HapticsOn) NativeFeel.LightTap(); }

        /// <summary>Settings' look and sound rows (ADR 0043 §9). True when handled.</summary>
        private bool LookAction(string id)
        {
            NumberFormat before = _format;
            int size = TextSize;
            if (id == SettingsIds.NumbersIndian) Numbers = NumberFormat.Indian;
            else if (id == SettingsIds.NumbersWestern) Numbers = NumberFormat.Western;
            else if (id.StartsWith(SettingsIds.TextPrefix, StringComparison.Ordinal)) TextSize = int.Parse(id.Substring(SettingsIds.TextPrefix.Length));
            else if (id == SettingsIds.ReducedMotion) ReducedMotion = !ReducedMotion;
            else if (id == SettingsIds.Sound) SoundOn = !SoundOn;
            else if (id == SettingsIds.Haptics) { HapticsOn = !HapticsOn; Haptic(); }
            else return false;
            ApplyLookAndSound();
            if (_format != before || TextSize != size)
            {
                // The formatter or the scale changed: a fresh HUD and Settings sheet in the new look, its way back kept.
                BuildUi();
                Refresh(false);
                Display(new SheetRequest("Settings", 0));
            }
            return true;
        }

        // ---- the HUD and the sheets ----

        /// <summary>Builds the HUD, the sheet presenter, the toast and the developer panel (again after the look changes).</summary>
        private void BuildUi()
        {
            _safeRoot.Clear();
            _fx = new Fx(_safeRoot, _tweens, _ui.MaxFlyingPerEvent, _ui.MaxFlyingTotal);
            _hud = new HudPresenter(_safeRoot, _tweens, _strings, _fmt)
            {
                OnStore = () => OpenSheet(new SheetRequest("Store", 0)),
                OnCollect = CollectAll,
                OnPeople = () => OpenSheet(new SheetRequest("People", 0)),
                OnForecast = () => OpenSheet(new SheetRequest("Forecast", 0)),
                OnMoney = () => OpenSheet(new SheetRequest("Money", 0)),
                OnMenu = () => OpenSheet(new SheetRequest("Menu", 0)),
                OnAlerts = () => OpenSheet(new SheetRequest("Alerts", 0)),
                OnOrders = () => OpenSheet(new SheetRequest("Orders", 0)),
                OnUrgent = () => _sounds.Play("urgent"),
            };
            _sheet = new SheetPresenter(_safeRoot, _tweens)
            {
                OnAction = OnSheetAction,
                OnFertiliser = OnFertiliser,
                OnClosed = OnSheetClosed,
                OnWhy = ShowWhy,
                OnOpenRow = Push,
                CanGoBack = () => _back.Count > 0,
                OnBack = Back,
                OkLabel = _strings.Get("action.ok"),
                WhyLabel = _strings.Get("why.label"),
            };
            BuildToast();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _dev = new DevTools.DevPanel(_safeRoot, new DevTools.DevPanel.Host
            {
                Sim = () => Sim,
                Advance = (s, mode) => { Sim.Execute(new DebugAdvance(s, mode)); AfterCommands(); },
                Snapshot = () => _snapshot = Sim.State.Clone(),
                Restore = RestoreSnapshot,
                Status = () => "snapshot " + (_snapshot == null ? "none" : "saved"),
                Visibility = v => _hud.SetVisible(v),
            });
            _hud.OnTimeHeld = () => _dev.Open();
#endif
        }

        private void ShowWhy(string key, long id)
        {
            _ctx.LocalNow = DateTime.Now;
            WhyVm why = WhyView.Build(_ctx, key, id);
            // On the next UI tick: a card built inside a click handler misses its first touch (STATUS, Editor quirks).
            _safeRoot.schedule.Execute(() => _sheet.ShowWhy(why));
        }

        /// <summary>The Sprint 7 sheets (ADR 0043 §4–§7).</summary>
        private SheetRequest ShedSheetRequest() { return new SheetRequest("Tile", Sim.Config.Game.Livestock.Livestock.ShedTile); }

        private void ShowPanel(SheetRequest r, bool refreshOnly)
        {
            switch (r.Kind)
            {
                case "Menu": _sheet.Show(MenuSheet.Build(_ctx), refreshOnly); break;
                case "Letter": _sheet.Show(Letter.List(_ctx), refreshOnly); break;
                case "Progress": _sheet.Show(ProgressSheet.Build(_ctx), refreshOnly); break;
                case "Lands": _sheet.Show(LandsSheet.Build(_ctx), refreshOnly); break;
                case "Money": _sheet.Show(MoneySheet.Build(_ctx), refreshOnly); break;
                case "Report": _sheet.Show(ReportSheet.Build(_ctx, r.Id), refreshOnly); break;
                case "Borrow": _sheet.Show(BorrowSheet.Build(_ctx, (int)r.Id, r.Arg), refreshOnly); break;
                case "Repay":
                    Loan loan = Sim.State.Credit.Loans.Find(l => l.Id == r.Id);
                    if (loan == null || !loan.IsOpen) { Return(new SheetRequest("Money", 0)); break; } // repaid in full
                    _sheet.Show(Repay.Build(_ctx, r.Id), refreshOnly);
                    break;
                case "LandLoan": _sheet.Show(LandLoanSheet.Build(_ctx, (int)r.Id), refreshOnly); break;
                case "Founder": _sheet.Show(FounderSheet.Build(_ctx), refreshOnly); break;
                case "Good": _sheet.Show(GoodSheet.Build(_ctx, (int)r.Id), refreshOnly); break;
                case "Offer": _sheet.Show(GoodSheet.Offer(_ctx, (int)r.Id, r.Arg), refreshOnly); break;
                case "BuyInputs": _sheet.Show(BuyInputsSheet.Build(_ctx), refreshOnly); break;
                case "BuyInput": _sheet.Show(BuyInputsSheet.One(_ctx, (int)r.Id), refreshOnly); break;
                case "Mandi": _sheet.Show(MandiSheet.Build(_ctx, r.Arg), refreshOnly); break;
                case "MandiLoad": _sheet.Show(MandiSheet.Load(_ctx, (int)r.Id, r.Arg), refreshOnly); break;
                case "Forecast": _sheet.Show(ForecastView.Sheet(_ctx), refreshOnly); break;
                case "Alerts": _sheet.Show(Alerts.Sheet(_ctx), refreshOnly); break;
                case "Orders": _sheet.Show(OrdersSheet.Build(_ctx), refreshOnly); break;
                case "BuyAnimal": _sheet.Show(AnimalBuySheet.Build(_ctx, (int)r.Id), refreshOnly); break;
                case "AnimalLoan": _sheet.Show(AnimalLoanSheet.Build(_ctx, (int)r.Id), refreshOnly); break;
                case "Animal":
                    if (global::Sim.Core.Livestock.LivestockRules.Find(Sim, r.Id) == null) { Return(ShedSheetRequest()); break; } // sold
                    _sheet.Show(AnimalSheet.Build(_ctx, r.Id), refreshOnly);
                    break;
            }
        }

        /// <summary>
        /// After a sheet's command: the sheet closes when the work is done (Hay Day style), stays open where the next choice follows
        /// (the store, people, the founder, money, a good, alerts), and returns to its hub after a step (an offer to its good, a loan
        /// to Money and loans, a purchase to Buy inputs). A spray keeps its sheet.
        /// </summary>
        private void AfterRun(Command[] commands)
        {
            if (!_open.HasValue) return;
            SheetRequest r = _open.Value;
            switch (r.Kind)
            {
                case "Store": case "People": case "Hand": case "Founder": case "Money": case "Good": case "Alerts": case "Settings": case "Orders": return;
                case "Offer": Return(new SheetRequest("Good", r.Id)); return;
                case "Borrow": case "Repay": Return(new SheetRequest("Money", 0)); return;
                case "BuyInput":
                    // Back where the purchase started: Buy inputs, the shed's Fill, a buy card, an alert.
                    if (_back.Count > 0) Back(); else Return(new SheetRequest("BuyInputs", 0));
                    return;
                case "BuyAnimal": case "AnimalLoan": case "Animal": Return(ShedSheetRequest()); return;
            }
            // The shed sheet stays open: building, buying from neighbours on or off, the hand's animals order (spec 012 §7).
            if (r.Kind == "Tile" && Sim.Config.Game.Livestock != null && r.Id == Sim.Config.Game.Livestock.Livestock.ShedTile) return;
            if (commands[commands.Length - 1] is Spray || commands[commands.Length - 1] is HireLabourers) return; // the field sheet stays: see who's on it
            _sheet.Close();
        }
    }
}
