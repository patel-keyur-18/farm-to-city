using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    /// <summary>A replay file: the commands of one session and, for golden tests, the expected final hash.</summary>
    public sealed class ReplayFile
    {
        public string Description { get; set; } = "";
        public bool DevCommandsEnabled { get; set; }

        /// <summary>Replays with the game content (land, farm, market, ledger), not time and weather only.</summary>
        public bool Game { get; set; }
        public List<Command> Commands { get; } = new List<Command>();

        /// <summary>The expected <c>GameState.ComputeHash()</c> after replaying, or null.</summary>
        public ulong? ExpectedHash { get; set; }
    }

    /// <summary>
    /// Reads and writes replay files (spec 001 AC-13). Hand-written mapping per command type
    /// (ADR 0018 rule 5). Unknown command types fail with the field path.
    /// </summary>
    public static class ReplayJson
    {
        public const int Format = 1;

        public static ReplayFile Load(string json)
        {
            const string root = "replay";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "format", "description", "devCommandsEnabled", "game", "commands", "expectedHash");
            int format = Int(Required(o, root, "format"), root + ".format");
            if (format != Format) throw new ContentException(root + ".format", "unsupported format " + format);

            var file = new ReplayFile();
            if (o["description"] != null) file.Description = String(o["description"], root + ".description");
            if (o["devCommandsEnabled"] != null) file.DevCommandsEnabled = Bool(o["devCommandsEnabled"], root + ".devCommandsEnabled");
            if (o["game"] != null) file.Game = Bool(o["game"], root + ".game");
            if (o["expectedHash"] != null)
            {
                string hex = String(o["expectedHash"], root + ".expectedHash");
                if (!hex.StartsWith("0x", StringComparison.Ordinal) ||
                    !ulong.TryParse(hex.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong hash))
                    throw new ContentException(root + ".expectedHash", "expected a hex string like 0x0123456789ABCDEF");
                file.ExpectedHash = hash;
            }

            List<JToken> commands = Array(Required(o, root, "commands"), root + ".commands");
            for (int i = 0; i < commands.Count; i++)
                file.Commands.Add(ReadCommand(commands[i], Index(root + ".commands", i)));
            return file;
        }

        public static string Write(ReplayFile file)
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            using (var w = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                w.WriteStartObject();
                w.WritePropertyName("format"); w.WriteValue(Format);
                w.WritePropertyName("description"); w.WriteValue(file.Description ?? "");
                w.WritePropertyName("devCommandsEnabled"); w.WriteValue(file.DevCommandsEnabled);
                if (file.Game) { w.WritePropertyName("game"); w.WriteValue(true); }
                w.WritePropertyName("commands");
                w.WriteStartArray();
                foreach (Command c in file.Commands) WriteCommand(w, c);
                w.WriteEndArray();
                if (file.ExpectedHash.HasValue)
                {
                    w.WritePropertyName("expectedHash");
                    w.WriteValue("0x" + file.ExpectedHash.Value.ToString("X16", CultureInfo.InvariantCulture));
                }
                w.WriteEndObject();
            }
            return sw.ToString().Replace("\r\n", "\n") + "\n";
        }

        private static Command ReadCommand(JToken token, string path)
        {
            JObject c = Object(token, path);
            string type = String(Required(c, path, "type"), path + ".type");
            switch (type)
            {
                case "NewGame":
                    OnlyFields(c, path, "type", "seed", "startWallUtc", "localSecondsSinceMidnight");
                    return new NewGame(ULong(Required(c, path, "seed"), path + ".seed"),
                        Long(Required(c, path, "startWallUtc"), path + ".startWallUtc"),
                        Long(Required(c, path, "localSecondsSinceMidnight"), path + ".localSecondsSinceMidnight"));
                case "AdvanceTo":
                    OnlyFields(c, path, "type", "target", "mode");
                    return new AdvanceTo(Long(Required(c, path, "target"), path + ".target"),
                        Mode(Required(c, path, "mode"), path + ".mode"));
                case "Sync":
                    OnlyFields(c, path, "type", "nowWallUtc");
                    return new Sync(Long(Required(c, path, "nowWallUtc"), path + ".nowWallUtc"));
                case "DebugAdvance":
                    OnlyFields(c, path, "type", "seconds", "mode");
                    return new DebugAdvance(Long(Required(c, path, "seconds"), path + ".seconds"),
                        Mode(Required(c, path, "mode"), path + ".mode"));
                default:
                    return ReadGameCommand(c, path, type);
            }
        }

        // ---- Game commands (specs 002–004): "at" is the sim time stamp; ids are content ids ----

        private static Command ReadGameCommand(JObject c, string path, string type)
        {
            long At() { return Long(Required(c, path, "at"), path + ".at"); }
            string S(string f) { return String(Required(c, path, f), path + "." + f); }
            long L(string f) { return Long(Required(c, path, f), path + "." + f); }
            void Only(params string[] f) { var all = new List<string> { "type", "at" }; all.AddRange(f); OnlyFields(c, path, all.ToArray()); }
            Overflow O() { return c["overflow"] == null ? Overflow.StoreOnly : S("overflow") == "sellRest" ? Overflow.SellRestToTrader : Overflow.StoreOnly; }
            switch (type)
            {
                case "BuyParcel": Only("parcel"); return new BuyParcel(At(), S("parcel"));
                case "SellParcel": Only("parcel"); return new SellParcel(At(), S("parcel"));
                case "Sow":
                    Only("tile", "crop", "seed", "fertiliser");
                    return new Sow(At(), (int)L("tile"), S("crop"), S("seed") == "saved" ? SeedSource.Saved : SeedSource.Certified, S("fertiliser"));
                case "Harvest": Only("tile", "overflow"); return new Harvest(At(), (int)L("tile"), O());
                case "ClearField": Only("tile"); return new ClearField(At(), (int)L("tile"));
                case "Spray": Only("tile"); return new Spray(At(), (int)L("tile"));
                case "SowGarden": Only("bed", "crop"); return new SowGarden(At(), L("bed"), S("crop"));
                case "PickGarden": Only("bed", "overflow"); return new PickGarden(At(), L("bed"), O());
                case "PlantTree": Only("slot", "tree"); return new PlantTree(At(), L("slot"), S("tree"));
                case "PickFruit": Only("slot", "overflow"); return new PickFruit(At(), L("slot"), O());
                case "RemoveTree": Only("slot"); return new RemoveTree(At(), L("slot"));
                case "SetDietPlan": Only("plan"); return new SetDietPlan(At(), S("plan"));
                case "SetWorkHours": Only("option"); return new SetWorkHours(At(), S("option"));
                case "SetLabourSplit": Only("share"); return new SetLabourSplit(At(), (int)L("share"));
                case "BuyMedicine": Only(); return new BuyMedicine(At());
                case "SetEatFromStore": Only("on"); return new SetEatFromStore(At(), Bool(Required(c, path, "on"), path + ".on"));
                case "Offer":
                    // A minimum price is written exactly as its raw Q31.32 value (prices are rarely short decimals).
                    Only("good", "quantity", "minPriceRaw");
                    return new Offer(At(), S("good"), L("quantity"), c["minPriceRaw"] == null ? (Fixed?)null : Fixed.FromRaw(L("minPriceRaw")));
                case "Withdraw": Only("good", "quantity"); return new Withdraw(At(), S("good"), L("quantity"));
                case "SellToTrader": Only("good", "quantity"); return new SellToTrader(At(), S("good"), L("quantity"));
                case "Buy": Only("good", "quantity"); return new Buy(At(), S("good"), L("quantity"));
                case "GoToMandi":
                {
                    Only("loads");
                    var loads = new List<MandiLoad>();
                    List<JToken> list = Array(Required(c, path, "loads"), path + ".loads");
                    for (int i = 0; i < list.Count; i++)
                    {
                        string lp = Index(path + ".loads", i);
                        JObject l = Object(list[i], lp);
                        OnlyFields(l, lp, "good", "quantity");
                        loads.Add(new MandiLoad(String(Required(l, lp, "good"), lp + ".good"), Long(Required(l, lp, "quantity"), lp + ".quantity")));
                    }
                    return new GoToMandi(At(), loads);
                }
                default:
                    throw new ContentException(path + ".type", "unknown command type '" + type + "'");
            }
        }

        private static void WriteGameCommand(JsonTextWriter w, Command command)
        {
            void T(string type, long at) { Prop(w, "type", type); w.WritePropertyName("at"); w.WriteValue(at); }
            void N(string name, long v) { w.WritePropertyName(name); w.WriteValue(v); }
            void Ov(Overflow o) { if (o == Overflow.SellRestToTrader) Prop(w, "overflow", "sellRest"); }
            switch (command)
            {
                case BuyParcel x: T("BuyParcel", x.At); Prop(w, "parcel", x.ParcelId); break;
                case SellParcel x: T("SellParcel", x.At); Prop(w, "parcel", x.ParcelId); break;
                case Sow x:
                    T("Sow", x.At); N("tile", x.TileId); Prop(w, "crop", x.CropId);
                    Prop(w, "seed", x.Seed == SeedSource.Saved ? "saved" : "certified"); Prop(w, "fertiliser", x.FertiliserId);
                    break;
                case Harvest x: T("Harvest", x.At); N("tile", x.TileId); Ov(x.Overflow); break;
                case ClearField x: T("ClearField", x.At); N("tile", x.TileId); break;
                case Spray x: T("Spray", x.At); N("tile", x.TileId); break;
                case SowGarden x: T("SowGarden", x.At); N("bed", x.BedId); Prop(w, "crop", x.GardenCropId); break;
                case PickGarden x: T("PickGarden", x.At); N("bed", x.BedId); Ov(x.Overflow); break;
                case PlantTree x: T("PlantTree", x.At); N("slot", x.SlotId); Prop(w, "tree", x.TreeId); break;
                case PickFruit x: T("PickFruit", x.At); N("slot", x.SlotId); Ov(x.Overflow); break;
                case RemoveTree x: T("RemoveTree", x.At); N("slot", x.SlotId); break;
                case SetDietPlan x: T("SetDietPlan", x.At); Prop(w, "plan", x.PlanId); break;
                case SetWorkHours x: T("SetWorkHours", x.At); Prop(w, "option", x.OptionId); break;
                case SetLabourSplit x: T("SetLabourSplit", x.At); N("share", x.SharePercent); break;
                case BuyMedicine x: T("BuyMedicine", x.At); break;
                case SetEatFromStore x: T("SetEatFromStore", x.At); w.WritePropertyName("on"); w.WriteValue(x.On); break;
                case Offer x:
                    T("Offer", x.At); Prop(w, "good", x.GoodId); N("quantity", x.Quantity);
                    if (x.MinPrice.HasValue) N("minPriceRaw", x.MinPrice.Value.Raw);
                    break;
                case Withdraw x: T("Withdraw", x.At); Prop(w, "good", x.GoodId); N("quantity", x.Quantity); break;
                case SellToTrader x: T("SellToTrader", x.At); Prop(w, "good", x.GoodId); N("quantity", x.Quantity); break;
                case Buy x: T("Buy", x.At); Prop(w, "good", x.GoodId); N("quantity", x.Quantity); break;
                case GoToMandi x:
                    T("GoToMandi", x.At);
                    w.WritePropertyName("loads");
                    w.WriteStartArray();
                    foreach (MandiLoad l in x.Loads) { w.WriteStartObject(); Prop(w, "good", l.GoodId); N("quantity", l.Quantity); w.WriteEndObject(); }
                    w.WriteEndArray();
                    break;
                default:
                    throw new ArgumentException("Command type " + command.GetType().Name + " can't be written to a replay file.");
            }
        }

        private static void WriteCommand(JsonTextWriter w, Command command)
        {
            w.WriteStartObject();
            switch (command)
            {
                case NewGame n:
                    Prop(w, "type", "NewGame");
                    w.WritePropertyName("seed"); w.WriteValue(n.Seed);
                    w.WritePropertyName("startWallUtc"); w.WriteValue(n.StartWallUtc);
                    w.WritePropertyName("localSecondsSinceMidnight"); w.WriteValue(n.LocalSecondsSinceMidnight);
                    break;
                case AdvanceTo a:
                    Prop(w, "type", "AdvanceTo");
                    w.WritePropertyName("target"); w.WriteValue(a.Target);
                    Prop(w, "mode", ModeName(a.Mode));
                    break;
                case Sync sync:
                    Prop(w, "type", "Sync");
                    w.WritePropertyName("nowWallUtc"); w.WriteValue(sync.NowWallUtc);
                    break;
                case DebugAdvance d:
                    Prop(w, "type", "DebugAdvance");
                    w.WritePropertyName("seconds"); w.WriteValue(d.Seconds);
                    Prop(w, "mode", ModeName(d.Mode));
                    break;
                default:
                    WriteGameCommand(w, command);
                    break;
            }
            w.WriteEndObject();
        }

        private static void Prop(JsonTextWriter w, string name, string value)
        {
            w.WritePropertyName(name);
            w.WriteValue(value);
        }

        private static long Long(JToken t, string path)
        {
            if (t.Type != JTokenType.Integer || ((JValue)t).Value is BigInteger) throw new ContentException(path, "expected a whole number");
            return t.Value<long>();
        }

        private static ulong ULong(JToken t, string path)
        {
            if (t.Type != JTokenType.Integer) throw new ContentException(path, "expected a whole number");
            object v = ((JValue)t).Value;
            if (v is BigInteger big)
            {
                if (big < 0 || big > ulong.MaxValue) throw new ContentException(path, "number out of range");
                return (ulong)big;
            }
            long l = t.Value<long>();
            if (l < 0) throw new ContentException(path, "must not be negative");
            return (ulong)l;
        }

        private static AdvanceMode Mode(JToken t, string path)
        {
            string s = String(t, path);
            if (s == "live") return AdvanceMode.Live;
            if (s == "away") return AdvanceMode.Away;
            throw new ContentException(path, "expected live or away");
        }

        private static string ModeName(AdvanceMode m) { return m == AdvanceMode.Away ? "away" : "live"; }
    }
}
