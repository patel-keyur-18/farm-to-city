using System;
using System.Collections.Generic;
using Game.Presentation.Art;
using Game.Presentation.Map;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Game.Services;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.People;
using Sim.View.Content;
using Sim.View.Text;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;
using ViewCollect = Sim.View.Views.CollectAll;

namespace Game.Presentation
{
    /// <summary>
    /// The composition root (ADR 0017), like a Spring Boot application class: it loads content, builds the simulation and the
    /// view layer, and wires the map, the HUD and the sheets. Nothing is placed in the scene by hand: Unity calls
    /// <see cref="Boot"/> after the bootstrap scene loads, and everything else is created here in code. The presentation only
    /// reads view models and sends ordinary commands (spec 008 §3.1).
    /// </summary>
    public sealed partial class GameRoot : MonoBehaviour
    {
        public Simulation Sim { get; private set; }
        public IEntitlementService Entitlements { get; } = new AlwaysUnlockedEntitlementService();

        private SimConfig _config;
        private StringTable _strings;
        private UiDef _ui;
        private Formatter _fmt;
        private NumberFormat _format;
        private ViewContext _ctx;

        private Camera _camera;
        private Tweens _tweens;
        private MapPresenter _map;
        private MapInput _input;
        private HudPresenter _hud;
        private SheetPresenter _sheet;
        private Fx _fx;
        private UIDocument _document;
        private VisualElement _safeRoot, _toast;
        private Rect _lastSafe;
        private Label _toastText;

        private long _lastSyncWall;
        private MapVm _mapVm;
        private Hit _selected = Hit.None;
        private SheetRequest? _open;
        private readonly List<SheetRequest> _back = new List<SheetRequest>(); // the sheets behind the open one (ADR 0044 §1)
        private string _fertiliser = "none";

        // Placing a hand's hut (ADR 0039 §2): the ghost's acre and the bar with Build here / Cancel.
        private int _ghostTile = -1;
        private enum PlaceKind { Hut, House, Business }

        private PlaceKind _placing; // a hand's hut, a family house (spec 014 §3.13) or a business (spec 016 §3.3)
        private int _placeType;     // the business type being placed, and whether you run it
        private bool _placeSelf;

        private bool _placingHouse { get { return _placing == PlaceKind.House; } }
        private VisualElement _placeBar;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private DevTools.DevPanel _dev;
        private GameState _snapshot;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        private static long WallNow() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            WeatherSet weather = ContentLoader.LoadWeather(ContentSource.Read("weather.json"));
            GameDefs game = ContentLoader.LoadGame(ContentSource.Read, weather);
            bool dev = Debug.isDebugBuild || Application.isEditor; // development builds and the Editor only (ADR 0038)
            _config = new SimConfig(ContentLoader.LoadClock(ContentSource.Read("clock.json")), weather, new SimOptions { DevCommandsEnabled = dev }, game: game);
            _strings = StringTable.Load(ContentSource.Read("strings/en.json"));
            _strings.ThrowOnMissing = false; // a gap shows as ⟦key⟧ instead of crashing the player
            _ui = UiLoader.Load(ContentSource.Read("ui.json"));
            _format = Formatter.FormatForRegion(RegionCode());
            _fmt = new Formatter(_strings, _format);

            CardVm first = LoadTown(); // the saved town, a backup after a crash, or a new town (spec 007 §3.3)
            BuildScene();
            RestoreCamera();
            Refresh(false);
            if (first != null) ShowCard(first, _ => { });
        }

        private static string RegionCode()
        {
            try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; }
            catch (Exception) { return ""; }
        }

        // ---- building the scene (all code, ADR 0017) ----

        private void BuildScene()
        {
            _camera = Camera.main;
            _camera.orthographic = true;
            _camera.backgroundColor = Art.Painter.Hex(0x86CDD8);

            _tweens = Tweens.Create(transform);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/DefaultTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            UiKit.SetFont(root, UiKit.Body);

            // Everything sits inside the device's safe area (notch, home indicator).
            _safeRoot = new VisualElement { pickingMode = PickingMode.Ignore };
            _safeRoot.style.position = Position.Absolute;
            _safeRoot.style.left = 0; _safeRoot.style.right = 0; _safeRoot.style.top = 0; _safeRoot.style.bottom = 0;
            root.Add(_safeRoot);

            _mapVm = MapView.Build(_ctx);
            _map = MapPresenter.Create(transform, _tweens, _ui);
            _map.Refresh(_mapVm, false);
            _input = MapInput.Create(gameObject, _camera, _map, _document, OnTap);
            _input.Focus(_mapVm.HomeX, _mapVm.HomeY); // the yard (spec 010 §3.5)

            _sounds = new Sounds(gameObject);
            ApplyLookAndSound();
            BuildUi();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void RestoreSnapshot()
        {
            if (_snapshot == null) return;
            Sim = Simulation.FromState(_config, _snapshot.Clone());
            _lastSyncWall = WallNow(); // the time between is not an away advance: the clock re-anchors
            Sim.State.Clock.Reanchor(_lastSyncWall);
            _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            _sheet.Close();
            Refresh(false);
        }
#endif

        private void BuildToast()
        {
            _toast = UiKit.Panel(26);
            _toast.pickingMode = PickingMode.Ignore;
            _toast.style.position = Position.Absolute;
            _toast.style.top = 150;
            _toast.style.alignSelf = Align.Center;
            _toast.style.left = Length.Percent(25); _toast.style.right = Length.Percent(25);
            UiKit.Padding(_toast, 14, 26);
            _toastText = UiKit.Text("", 36, UiKit.Ink, UiKit.Heading);
            _toastText.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toast.Add(_toastText);
            _toast.style.display = DisplayStyle.None;
            _safeRoot.Add(_toast);
        }

        private void Toast(string text)
        {
            _toastText.text = text;
            _toast.style.display = DisplayStyle.Flex;
            _tweens.Run(_toast, 2.2f, Ease.Linear, k => _toast.style.opacity = k < 0.8f ? 1f : 1f - (k - 0.8f) / 0.2f, () => _toast.style.display = DisplayStyle.None);
        }

        // ---- the loop ----

        private void Update()
        {
            ApplySafeArea();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_dev != null && _dev.IsOpen) { return; }
#endif
            // Sync once per wall-clock second: the sim's time unit is a whole second (spec 001 §3.4).
            long now = WallNow();
            if (now == _lastSyncWall) return;
            _lastSyncWall = now;
            Sim.Execute(new Sync(now));
            AfterCommands();
            AutosaveIfDue(now);
        }

        private void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == _lastSafe || _safeRoot.panel == null) return;
            _lastSafe = safe;
            float k = _safeRoot.panel.scaledPixelsPerPoint;
            _safeRoot.style.left = safe.xMin / k;
            _safeRoot.style.right = (Screen.width - safe.xMax) / k;
            _safeRoot.style.top = (Screen.height - safe.yMax) / k;
            _safeRoot.style.bottom = safe.yMin / k;
        }

        /// <summary>After any command or Sync: play the events' motion, then redraw from fresh view models.</summary>
        private void AfterCommands()
        {
            List<SimEvent> events = Sim.DrainEvents();
            HandleEvents(events);
            OnTownEvents(events);
            GuideAfter(events);
            Refresh(true);
        }

        private void Refresh(bool animate)
        {
            _ctx.LocalNow = DateTime.Now;
            _mapVm = MapView.Build(_ctx);
            _map.Refresh(_mapVm, animate);
            _hud.Refresh(HudView.Build(_ctx), animate);
            if (_open.HasValue && _sheet.IsOpen) ShowCurrentSheet(refreshOnly: true);
        }

        // ---- events drive motion (spec 008 §3.12) ----

        private void HandleEvents(List<SimEvent> events)
        {
            bool coins = false, produce = false, away = false, ready = false, sown = false;
            long earned = 0, closed = -1;
            foreach (SimEvent e in events) if (e is AwayAdvanceCompleted) away = true;
            foreach (SimEvent e in events)
            {
                switch (e)
                {
                    case SoldToTrader t: earned += t.Amount; break;
                    case MarketSales m: earned += m.Amount; break;
                    case MandiTripReturned r: earned += r.Net; break;
                    case DayLabourPaid d: earned += d.Amount; break;
                    case OrderPaid op: earned += op.Amount; break;
                    case CropReady _: case BedReady _: case FruitRipe _: ready |= !away; break;
                    case CropSown _: case TreePlanted _: sown |= !away; break;
                    case SeasonClosed c: if (!away) closed = c.Period; break;
                }
                switch (e)
                {
                    case CropHarvested h:
                        produce |= FlyProduce(IconBank.Good(h.CropId), _map.WorldOf(new Hit(HitKind.Tile, h.TileId)), 5);
                        break;
                    case GardenPicked g:
                        produce |= FlyProduce(IconBank.Good(g.GoodId), _map.WorldOf(new Hit(HitKind.Bed, g.BedId)), 4);
                        break;
                    case FruitPicked f:
                        produce |= FlyProduce(IconBank.Good(f.TreeId), _map.WorldOf(new Hit(HitKind.Slot, f.SlotId)), 4);
                        break;
                    case SoldToTrader _:
                    case MarketSales _:
                    case MandiTripReturned _:
                    case DayLabourPaid _:
                    case OrderPaid _:
                        coins = true;
                        break;
                    case OrdersPosted _: if (!away) Toast(_strings.Get("toast.orders")); break;
                    case OrderDelivered d:
                        // To the supermarket's van at the truck stop: from the store pill to the van (spec 011 §7).
                        if (!away) _fx.Fly(IconBank.Good(d.GoodId), _hud.ScreenCentre(_hud.StoreTarget), _input.WorldToScreen(_map.VanWorld), 3, null);
                        break;
                    case PestWarning p:
                        Toast(_strings.Get("toast.pests"));
                        _map.Shake(p.TileId);
                        break;
                    case FounderSick _: Toast(_strings.Get("toast.sick")); break;
                    case ApplicantArrived _: Toast(_strings.Get("toast.applicant")); break;
                    case HandHutBuilt _: Toast(_strings.Get("toast.hutBuilt")); break;
                    case HandHired _: Toast(_strings.Get("toast.handHired")); break;
                    case OrderCompleted _: Toast(_strings.Get("toast.orderDone")); break;
                    case ContractLapsed _: Toast(_strings.Get("toast.lapsed")); break;
                }
            }
            if (away) ShowAway();
            if (produce) { _sounds.Harvest(); Haptic(); }
            if (ready) _sounds.Play("ready");
            if (sown) _sounds.Sow();
            if (closed >= 0) _safeRoot.schedule.Execute(() => OpenSheet(new SheetRequest("Report", closed))); // the season's report (spec 008 §3.8)
            if (coins)
            {
                _sounds.CoinsIn(earned / 100);
                Haptic();
                Vector2 from = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
                _fx.Fly(IconBank.Coin, from, _hud.ScreenCentre(_hud.CashTarget), 4, _hud.BounceCash);
            }
        }

        private bool FlyProduce(Sprite icon, Vector2 world, int count)
        {
            Vector2 from = _input.WorldToScreen(world);
            _fx.Fly(icon, from, _hud.ScreenCentre(_hud.StoreTarget), count, _hud.BounceStore);
            return true;
        }

        // ---- taps, sheets and commands ----

        private void OnTap(Vector2 screen)
        {
            if (_sheet.IsOpen) { _sheet.Close(); return; }
            Hit hit = _map.Pick(_input.ScreenToWorld(screen));
            if (hit.Kind == HitKind.None) return;
            _selected = hit;
            _map.Select(hit, _mapVm);
            _map.Bounce(hit);
            switch (hit.Kind)
            {
                case HitKind.Hut: OpenSheet(new SheetRequest("Founder", 0)); break; // You and your family (ADR 0043 §6)
                case HitKind.HandHut:
                {
                    Hand who = hit.Id > 0 ? Sim.State.People.Hands.Find(h => h.HutId == hit.Id) : null;
                    OpenSheet(who != null ? new SheetRequest("Hand", who.Id) : new SheetRequest("People", 0));
                    break;
                }
                case HitKind.Truck: OpenSheet(new SheetRequest("Store", 0)); break; // the trader's truck (spec 011 §7)
                case HitKind.Van: OpenSheet(new SheetRequest("Orders", 0)); break;
                case HitKind.Slot: OpenSheet(new SheetRequest("Slot", hit.Id)); break;
                case HitKind.Bed: OpenSheet(new SheetRequest("Bed", hit.Id)); break;
                default: OpenSheet(new SheetRequest("Tile", hit.Id)); break;
            }
        }

        /// <summary>A sheet on its own (from the map, the HUD, a card): nothing to go back to.</summary>
        private void OpenSheet(SheetRequest request)
        {
            _back.Clear();
            Display(request);
        }

        /// <summary>A sheet opened from the open one: the back button returns to it.</summary>
        private void Push(SheetRequest request)
        {
            if (_open.HasValue && _sheet.IsOpen) _back.Add(_open.Value);
            Display(request);
        }

        private void Back()
        {
            if (_back.Count == 0) return;
            SheetRequest r = _back[_back.Count - 1];
            _back.RemoveAt(_back.Count - 1);
            Display(r);
        }

        /// <summary>After a step, back to its hub: the way it came if the hub is behind, otherwise straight to it.</summary>
        private void Return(SheetRequest hub)
        {
            int at = _back.FindLastIndex(r => r.Kind == hub.Kind && r.Id == hub.Id);
            if (at < 0) { OpenSheet(hub); return; }
            _back.RemoveRange(at, _back.Count - at);
            Display(hub);
        }

        private void Display(SheetRequest request)
        {
            if (request.Kind == "ShowTile")
            {
                // From the Menu's land list: pan to the parcel and open its sheet.
                _selected = new Hit(HitKind.Tile, request.Id);
                _map.Select(_selected, _mapVm);
                request = new SheetRequest("Tile", request.Id);
            }
            if (request.Kind == "Settings") _townFiles = _saves.TownFiles().Count;
            _guide.SheetVisited(request.Kind, Sim, request.Id);
            _ctx.LocalNow = DateTime.Now;
            _open = request;
            ShowCurrentSheet(refreshOnly: false);
            if (_selected.Kind != HitKind.None) _input.Reveal(_map.WorldOf(_selected));
        }

        private void ShowCurrentSheet(bool refreshOnly)
        {
            SheetRequest r = _open.Value;
            switch (r.Kind)
            {
                case "People": _sheet.Show(PeopleSheet.Build(_ctx), refreshOnly); break;
                case "Hire": _sheet.Show(PeopleSheet.Hire(_ctx), refreshOnly); break;
                case "Word": _sheet.Show(PeopleSheet.Word(_ctx), refreshOnly); break;
                case "Hand":
                    if (Sim.State.People.Hand(r.Id) == null) { _sheet.Close(); break; } // the hand left
                    _sheet.Show(HandSheet.Build(_ctx, r.Id), refreshOnly);
                    break;
                case "Order":
                    bool tree = r.Arg != null && r.Arg.StartsWith("tree", StringComparison.Ordinal);
                    OrderTargetKind kind = tree ? OrderTargetKind.Tree : OrderTargetKind.Field;
                    // A field's crop is chosen on the sow sheet, with the fertiliser and every figure (ADR 0059 §3).
                    if (OrderSheet.IsCropStep(kind, r.Arg) && PeopleWords.OrderHand(Sim, kind, r.Id) != null) _sheet.Show(OrderSheet.Crops(_ctx, r.Id, r.Arg), refreshOnly);
                    else _sheet.Show(OrderSheet.Build(_ctx, kind, r.Id, r.Arg), refreshOnly);
                    break;
                case "HutPlacement": if (!refreshOnly) StartPlacing(PlaceKind.Hut); break;
                case "HousePlacement": if (!refreshOnly) StartPlacing(PlaceKind.House); break;
                case "BusinessPlacement": if (!refreshOnly) StartPlacing(PlaceKind.Business, (int)r.Id, r.Arg == "self"); break;
                case "Businesses": _sheet.Show(BusinessesSheet.Build(_ctx), refreshOnly); break;
                case "BusinessResearch": _sheet.Show(BusinessResearchSheet.Build(_ctx, (int)r.Id), refreshOnly); break;
                case "BusinessPlan": _sheet.Show(BusinessPlanSheet.Build(_ctx, (int)r.Id, r.Arg == "self"), refreshOnly); break;
                case "Business": _sheet.Show(BusinessSheet.Build(_ctx, r.Id, r.Arg), refreshOnly); break;
                case "Town": _sheet.Show(TownSheet.Build(_ctx, r.Arg), refreshOnly); break;
                case "Household":
                    if (Sim.State.Citizens?.Household(r.Id) == null) { _sheet.Close(); break; } // the family left
                    _sheet.Show(HouseholdSheet.Build(_ctx, r.Id), refreshOnly);
                    break;
                case "Citizen": _sheet.Show(CitizenSheet.Build(_ctx, r.Id), refreshOnly); break;
                case "Departed": _sheet.Show(CitizenSheet.Departed(_ctx, r.Id), refreshOnly); break;
                case "Tile": _sheet.Show(FieldSheets.ForTile(_ctx, (int)r.Id), refreshOnly); break;
                case "Slot": _sheet.Show(FieldSheets.ForSlot(_ctx, r.Id), refreshOnly); break;
                case "Bed": _sheet.Show(FieldSheets.ForBed(_ctx, r.Id), refreshOnly); break;
                case "Store": _sheet.Show(StoreSheet.Build(_ctx), refreshOnly); break;
                case "Settings": _sheet.Show(SettingsSheet.Build(_ctx, Info()), refreshOnly); break;
                case "Import":
                    if (!refreshOnly) ScanImports();
                    _sheet.Show(ImportSheet.Build(_ctx, _importFiles, Info()), refreshOnly);
                    break;
                case "Sow": ShowSow((int)r.Id, refreshOnly); break;
                default: ShowPanel(r, refreshOnly); break;
            }
        }

        /// <summary>A fertiliser chip: on the sow sheet it changes this sowing; on a hand's order, the order's (ADR 0059 §3).</summary>
        private void OnFertiliser(string f)
        {
            if (_open.Value.Kind == "Order")
            {
                _open = new SheetRequest("Order", _open.Value.Id, "+" + f);
                ShowCurrentSheet(refreshOnly: true);
                return;
            }
            _fertiliser = f;
            ShowSow((int)_open.Value.Id, refresh: true);
        }

                private void ShowSow(int tile, bool refresh = false)
        {
            _open = new SheetRequest("Sow", tile);
            _sheet.Show(SowSheet.Build(_ctx, tile, _fertiliser), refresh);
        }

        private void OnSheetClosed()
        {
            _open = null;
            _back.Clear();
            _selected = Hit.None;
            _map.ClearSelection();
            _fertiliser = "none";
            UpdateNudge();
            _safeRoot.schedule.Execute(MaybeOpenLetter); // a page that came while the sheet was open
        }

        private void OnSheetAction(ActionVm action)
        {
            if (action.Id == Alerts.CollectId) { _sheet.Close(); CollectAll(); return; }
            if (!action.Opens.HasValue && action.Confirm == null && SettingsAction(action)) return;
            if (action.Opens.HasValue)
            {
                if (action.Opens.Value.Kind == "LetterPage")
                {
                    // Reading a page again (AC-12): over the letter's list, which stays open; nothing is sent.
                    string id = Sim.Config.Game.Onboarding.Chapters[(int)action.Opens.Value.Id].Id;
                    _safeRoot.schedule.Execute(() => ShowLetter(id, reading: true));
                    return;
                }
                if (action.Opens.Value.Kind == "HutPlacement" || action.Opens.Value.Kind == "HousePlacement" || action.Opens.Value.Kind == "BusinessPlacement")
                {
                    // Start on the next UI tick, outside this click: a bar built inside the handler never got its own clicks on touch.
                    SheetRequest place = action.Opens.Value;
                    PlaceKind kind = place.Kind == "HousePlacement" ? PlaceKind.House : place.Kind == "BusinessPlacement" ? PlaceKind.Business : PlaceKind.Hut;
                    _sheet.Close();
                    _safeRoot.schedule.Execute(() => StartPlacing(kind, (int)place.Id, place.Arg == "self"));
                    return;
                }
                // A tab or a sort changes the sheet in place: nothing new to go back to.
                if (action.Id.StartsWith("tab:", StringComparison.Ordinal) || action.Id.StartsWith("sort:", StringComparison.Ordinal)) { Display(action.Opens.Value); return; }
                Push(action.Opens.Value);
                return;
            }
            if (action.Confirm != null)
            {
                // ADR 0039 §6: an ordered field sown with another crop asks whether the order changes too.
                _sheet.Confirm(action.Confirm, () => Run(action.Confirm.Yes(Sim.State.Clock.SimTime)), () => { if (action.Confirm.NoSends) Run(new[] { action.Create(Sim.State.Clock.SimTime) }); });
                return;
            }
            Run(new[] { action.Create(Sim.State.Clock.SimTime) });
        }

        private void Run(Command[] commands)
        {
            long cashBefore = Sim.State.Ledger.Cash;
            foreach (Command command in commands)
            {
                CommandResult result = Sim.Execute(command);
                if (!result.Accepted)
                {
                    _guide.Rejected(Sim, command, result.Reason);
                    Toast(_ctx.Reject(result.Reason));
                    AfterCommands();
                    return;
                }
            }
            if (Sim.State.Ledger.Cash < cashBefore) _sounds.Play("coins_out");
            AfterCommands();
            AfterRun(commands);
        }

        // ---- placing a hand's hut (ADR 0039 §2) ----

        private void StartPlacing(PlaceKind kind, int type = 0, bool self = false)
        {
            _open = null;
            _map.ClearSelection();
            _placing = kind;
            _placeType = type;
            _placeSelf = self;
            List<HutPlaceVm> places = kind == PlaceKind.House ? HousePlacement.Places(_ctx) : kind == PlaceKind.Business ? BusinessPlacement.Places(_ctx) : HutPlacement.Places(_ctx);
            HutPlaceVm first = places.Find(p => p.Allowed) ?? (places.Count > 0 ? places[0] : null);
            if (first == null) return;
            if (kind != PlaceKind.Hut) _map.LightPlaces(places.FindAll(p => p.Allowed).ConvertAll(p => p.TileId));
            _input.Placing = w => { int tile = _map.Grid.Pick(w.x, w.y); if (tile >= 0) MoveGhost(tile); };
            MoveGhost(first.TileId);
            _input.Reveal(_map.WorldOf(new Hit(HitKind.Tile, first.TileId)));
        }

        private void MoveGhost(int tile)
        {
            string reason = _placingHouse ? HousePlacement.ReasonFor(_ctx, tile) : _placing == PlaceKind.Business ? BusinessPlacement.ReasonFor(_ctx, tile) : HutPlacement.ReasonFor(_ctx, tile);
            if (tile == _ghostTile && _placeBar != null) return;
            _ghostTile = tile;
            if (_placing == PlaceKind.Business) _map.ShowBusinessGhost(tile, reason == null, Sim.Config.Game.Businesses.Types[_placeType].Id);
            else if (_placingHouse)
                _map.ShowHouseGhost(tile, reason == null, global::Sim.Core.People.HouseRules.HousesOn(Sim, tile), Sim.State.Land.Tiles[tile].Terrain == global::Sim.Core.Definitions.Terrain.Quarters);
            else _map.ShowGhost(tile, reason == null);
            if (_placeBar != null) _safeRoot.Remove(_placeBar);
            _placeBar = UiKit.Panel(30);
            _placeBar.style.position = Position.Absolute;
            _placeBar.style.bottom = 24;
            int side = _placingHouse ? 8 : 18; // the house's bar has a third button (a home loan, ADR 0071)
            _placeBar.style.left = Length.Percent(side); _placeBar.style.right = Length.Percent(side);
            _placeBar.style.flexDirection = FlexDirection.Row;
            _placeBar.style.alignItems = Align.Center;
            UiKit.Padding(_placeBar, 10, 24);
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            string what = _placingHouse ? "place.house" : _placing == PlaceKind.Business ? "place.business" : "place.";
            text.Add(UiKit.Heading1(_strings.Get(what + (what == "place." ? "title" : "Title")), 40));
            text.Add(UiKit.Text(reason ?? _strings.Get(what + (what == "place." ? "hint" : "Hint")), 28, reason == null ? UiKit.Wood : UiKit.Warn, UiKit.Body));
            _placeBar.Add(text);
            ActionVm build = _placingHouse ? HousePlacement.Confirm(_ctx, tile)
                : _placing == PlaceKind.Business ? BusinessPlacement.Confirm(_ctx, tile, _placeType, _placeSelf) : HutPlacement.Confirm(_ctx, tile);
            _placeBar.Add(UiKit.MakeButton(build.Label, ActionLook.Primary, () =>
            {
                Command c = build.Create(Sim.State.Clock.SimTime);
                CommandResult r = Sim.Execute(c);
                if (!r.Accepted) { _guide.Rejected(Sim, c, r.Reason); Toast(_ctx.Reject(r.Reason)); return; }
                StopPlacing();
                AfterCommands();
            }, build.Detail, build.Enabled));
            if (_placingHouse)
            {
                // ADR 0071: build with the bank's home loan, choosing the term on its sheet.
                ActionVm loan = HousePlacement.WithLoan(_ctx, tile);
                _placeBar.Add(UiKit.MakeButton(loan.Label, ActionLook.Secondary, () =>
                {
                    StopPlacing();
                    _safeRoot.schedule.Execute(() => OpenSheet(loan.Opens.Value)); // built on the next tick, so its buttons take touches
                }, loan.Detail, loan.Enabled));
            }
            _placeBar.Add(UiKit.MakeButton(_strings.Get("action.cancel"), ActionLook.Secondary, StopPlacing));
            _safeRoot.Add(_placeBar);
        }

        private void StopPlacing()
        {
            _input.Placing = null;
            _ghostTile = -1;
            _map.HideGhost();
            if (_placeBar != null) { _safeRoot.Remove(_placeBar); _placeBar = null; }
        }

        // ---- While you were away, the people part (US-182) ----

        private void ShowAway()
        {
            AwayCard.Vm vm = AwayCard.Build(_ctx, Sim.LastAwayReport);
            if (vm == null) return; // nothing happened: no card (ADR 0043 §8)
            _guide.AwayCardShown(Sim); // "Welcome back" follows the card (spec 009 §3.3)
            _sheet.Away(vm, _strings.Plural("hud.collect", vm.ReadyCount), _strings.Get("away.seeReport"), _strings.Get("action.close"),
                _strings.Plural("away.more", vm.More.Count), CollectAll, () => OpenSheet(new SheetRequest("Report", vm.ReportPeriod)));
        }

        private void CollectAll()
        {
            foreach (Command c in CollectAllCommands()) Sim.Execute(c);
            AfterCommands();
            // What didn't fit: one prompt to sell the rest to the trader, never without asking (spec 008 §3.9).
            CollectRestVm rest = CollectAllRest.Build(Sim);
            if (rest == null) return;
            ShowCard(Cards.SellRest(_ctx, rest), id =>
            {
                if (id != SettingsIds.Yes) return;
                CollectRestVm now = CollectAllRest.Build(Sim); // stamped now: a second may have passed since the card opened
                if (now == null) return;
                foreach (Command c in now.Commands) Sim.Execute(c);
                AfterCommands();
            });
        }

        private List<Command> CollectAllCommands() { return ViewCollect.Commands(Sim, Sim.State.Clock.SimTime); }
    }
}
