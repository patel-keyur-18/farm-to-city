using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.Core.Saves
{
    /// <summary>
    /// The command log in a save (spec 007 §3.2): one tag per command type, then its fields. Commands name content by id,
    /// so nothing here needs remapping. A tag is never reused; a new command type takes the next one.
    /// </summary>
    internal static class CommandCodec
    {
        private enum Tag : byte
        {
            NewGame = 1, AdvanceTo, Sync, DebugAdvance, LiveRun,
            BuyParcel, SellParcel, TakeLoan, RepayLoan, RestructureLoan,
            Sow, Harvest, ClearField, Spray, SowGarden, PickGarden, PlantTree, PickFruit, RemoveTree,
            SetDietPlan, SetWorkHours, SetLabourSplit, BuyMedicine, SetEatFromStore,
            Offer, Withdraw, SellToTrader, Buy, GoToMandi,
            HireDayLabourers, BuildHandHut, PostJob, Interview, OfferContract, RenewContract, DismissHand,
            SetStandingOrder, ClearStandingOrder,
            SeeChapter, CompleteStep, SkipFirstSession,
            HireLabourers,
            DeliverOrder,
            BuildShed, BuyAnimal, SellAnimal, SetBuyGreenFodder,
        }

        public static void Write(SaveWriter w, Command command)
        {
            void T(Tag tag) { w.WriteByte((byte)tag); }
            void At(TimedCommand c, Tag tag) { T(tag); w.WriteLong(c.At); }
            switch (command)
            {
                case NewGame c: T(Tag.NewGame); w.WriteULong(c.Seed); w.WriteLong(c.StartWallUtc); w.WriteLong(c.LocalSecondsSinceMidnight); break;
                case AdvanceTo c: T(Tag.AdvanceTo); w.WriteLong(c.Target); w.WriteInt((int)c.Mode); break;
                case Sync c: T(Tag.Sync); w.WriteLong(c.NowWallUtc); break;
                case DebugAdvance c: T(Tag.DebugAdvance); w.WriteLong(c.Seconds); w.WriteInt((int)c.Mode); break;
                case LiveRun c: T(Tag.LiveRun); w.WriteLong(c.FromWallUtc); w.WriteLong(c.ToWallUtc); w.WriteLong(c.SimSeconds); break;
                case BuyParcel c:
                    At(c, Tag.BuyParcel); w.WriteString(c.ParcelId); w.WriteBool(c.Financing != null);
                    if (c.Financing != null) { w.WriteString(c.Financing.LenderId); w.WriteLong(c.Financing.Amount); w.WriteInt(c.Financing.TermSeasons); }
                    break;
                case SellParcel c: At(c, Tag.SellParcel); w.WriteString(c.ParcelId); break;
                case TakeLoan c: At(c, Tag.TakeLoan); w.WriteString(c.LenderId); w.WriteLong(c.Amount); w.WriteInt(c.TermSeasons); break;
                case RepayLoan c: At(c, Tag.RepayLoan); w.WriteLong(c.LoanId); w.WriteLong(c.Amount); break;
                case RestructureLoan c: At(c, Tag.RestructureLoan); w.WriteLong(c.LoanId); break;
                case Sow c: At(c, Tag.Sow); w.WriteInt(c.TileId); w.WriteString(c.CropId); w.WriteInt((int)c.Seed); w.WriteString(c.FertiliserId); break;
                case Harvest c: At(c, Tag.Harvest); w.WriteInt(c.TileId); w.WriteInt((int)c.Overflow); break;
                case ClearField c: At(c, Tag.ClearField); w.WriteInt(c.TileId); break;
                case Spray c: At(c, Tag.Spray); w.WriteInt(c.TileId); break;
                case SowGarden c: At(c, Tag.SowGarden); w.WriteLong(c.BedId); w.WriteString(c.GardenCropId); break;
                case PickGarden c: At(c, Tag.PickGarden); w.WriteLong(c.BedId); w.WriteInt((int)c.Overflow); break;
                case PlantTree c: At(c, Tag.PlantTree); w.WriteLong(c.SlotId); w.WriteString(c.TreeId); break;
                case PickFruit c: At(c, Tag.PickFruit); w.WriteLong(c.SlotId); w.WriteInt((int)c.Overflow); break;
                case RemoveTree c: At(c, Tag.RemoveTree); w.WriteLong(c.SlotId); break;
                case SetDietPlan c: At(c, Tag.SetDietPlan); w.WriteString(c.PlanId); break;
                case SetWorkHours c: At(c, Tag.SetWorkHours); w.WriteString(c.OptionId); break;
                case SetLabourSplit c: At(c, Tag.SetLabourSplit); w.WriteInt(c.SharePercent); break;
                case BuyMedicine c: At(c, Tag.BuyMedicine); break;
                case SetEatFromStore c: At(c, Tag.SetEatFromStore); w.WriteBool(c.On); break;
                case SeeChapter c: At(c, Tag.SeeChapter); w.WriteString(c.ChapterId); break;
                case CompleteStep c: At(c, Tag.CompleteStep); w.WriteString(c.StepId); break;
                case SkipFirstSession c: At(c, Tag.SkipFirstSession); break;
                case Offer c:
                    At(c, Tag.Offer); w.WriteString(c.GoodId); w.WriteLong(c.Quantity); w.WriteBool(c.MinPrice.HasValue);
                    if (c.MinPrice.HasValue) w.WriteFixed(c.MinPrice.Value);
                    break;
                case Withdraw c: At(c, Tag.Withdraw); w.WriteString(c.GoodId); w.WriteLong(c.Quantity); break;
                case SellToTrader c: At(c, Tag.SellToTrader); w.WriteString(c.GoodId); w.WriteLong(c.Quantity); break;
                case DeliverOrder c: At(c, Tag.DeliverOrder); w.WriteLong(c.OrderId); w.WriteLong(c.Quantity); break;
                case Buy c: At(c, Tag.Buy); w.WriteString(c.GoodId); w.WriteLong(c.Quantity); break;
                case GoToMandi c:
                    At(c, Tag.GoToMandi); w.WriteInt(c.Loads.Count);
                    foreach (MandiLoad l in c.Loads) { w.WriteString(l.GoodId); w.WriteLong(l.Quantity); }
                    break;
                case HireLabourers c: At(c, Tag.HireLabourers); w.WriteInt(-1); w.WriteInt(c.Count); break; // the field slot of Sprint 10's first build, unused since ADR 0049
                case BuildHandHut c: At(c, Tag.BuildHandHut); w.WriteInt(c.TileId); break;
                case BuildShed c: At(c, Tag.BuildShed); break;
                case BuyAnimal c:
                    At(c, Tag.BuyAnimal); w.WriteString(c.KindId); w.WriteBool(c.Financing != null);
                    if (c.Financing != null) { w.WriteString(c.Financing.LenderId); w.WriteLong(c.Financing.Amount); w.WriteInt(c.Financing.TermSeasons); }
                    break;
                case SellAnimal c: At(c, Tag.SellAnimal); w.WriteLong(c.AnimalId); break;
                case SetBuyGreenFodder c: At(c, Tag.SetBuyGreenFodder); w.WriteBool(c.On); break;
                case PostJob c: At(c, Tag.PostJob); w.WriteBool(c.AboveMarket); break;
                case Interview c: At(c, Tag.Interview); w.WriteLong(c.ApplicantId); break;
                case OfferContract c: At(c, Tag.OfferContract); w.WriteLong(c.ApplicantId); break;
                case RenewContract c: At(c, Tag.RenewContract); w.WriteLong(c.HandId); break;
                case DismissHand c: At(c, Tag.DismissHand); w.WriteLong(c.HandId); break;
                case SetStandingOrder c:
                {
                    At(c, Tag.SetStandingOrder); w.WriteLong(c.HandId);
                    OrderSpec o = c.Order;
                    w.WriteInt((int)o.Kind); w.WriteLong(o.Target); w.WriteString(o.CropId); w.WriteInt((int)o.Seed); w.WriteString(o.FertiliserId);
                    w.WriteInt((int)o.Sell); w.WriteFixed(o.MinPrice); w.WriteBool(o.Once);
                    break;
                }
                case ClearStandingOrder c: At(c, Tag.ClearStandingOrder); w.WriteLong(c.HandId); w.WriteInt((int)c.Kind); w.WriteLong(c.Target); break;
                default: throw new System.ArgumentException("No save encoding for " + command.GetType().Name + ".");
            }
        }

        public static Command Read(SaveReader r)
        {
            var tag = (Tag)r.ReadByte();
            if (tag == Tag.NewGame) return new NewGame(r.ReadULong(), r.ReadLong(), r.ReadLong());
            if (tag == Tag.AdvanceTo) return new AdvanceTo(r.ReadLong(), (AdvanceMode)r.ReadInt());
            if (tag == Tag.Sync) return new Sync(r.ReadLong());
            if (tag == Tag.DebugAdvance) return new DebugAdvance(r.ReadLong(), (AdvanceMode)r.ReadInt());
            if (tag == Tag.LiveRun) return new LiveRun(r.ReadLong(), r.ReadLong(), r.ReadLong());
            long at = r.ReadLong();
            switch (tag)
            {
                case Tag.BuyParcel:
                {
                    string parcel = r.ReadString();
                    Financing f = r.ReadBool() ? new Financing(r.ReadString(), r.ReadLong(), r.ReadInt()) : null;
                    return new BuyParcel(at, parcel, f);
                }
                case Tag.SellParcel: return new SellParcel(at, r.ReadString());
                case Tag.TakeLoan: return new TakeLoan(at, r.ReadString(), r.ReadLong(), r.ReadInt());
                case Tag.RepayLoan: return new RepayLoan(at, r.ReadLong(), r.ReadLong());
                case Tag.RestructureLoan: return new RestructureLoan(at, r.ReadLong());
                case Tag.Sow: return new Sow(at, r.ReadInt(), r.ReadString(), (SeedSource)r.ReadInt(), r.ReadString());
                case Tag.Harvest: return new Harvest(at, r.ReadInt(), (Overflow)r.ReadInt());
                case Tag.ClearField: return new ClearField(at, r.ReadInt());
                case Tag.Spray: return new Spray(at, r.ReadInt());
                case Tag.SowGarden: return new SowGarden(at, r.ReadLong(), r.ReadString());
                case Tag.PickGarden: return new PickGarden(at, r.ReadLong(), (Overflow)r.ReadInt());
                case Tag.PlantTree: return new PlantTree(at, r.ReadLong(), r.ReadString());
                case Tag.PickFruit: return new PickFruit(at, r.ReadLong(), (Overflow)r.ReadInt());
                case Tag.RemoveTree: return new RemoveTree(at, r.ReadLong());
                case Tag.SetDietPlan: return new SetDietPlan(at, r.ReadString());
                case Tag.SetWorkHours: return new SetWorkHours(at, r.ReadString());
                case Tag.SetLabourSplit: return new SetLabourSplit(at, r.ReadInt());
                case Tag.SeeChapter: return new SeeChapter(at, r.ReadString());
                case Tag.CompleteStep: return new CompleteStep(at, r.ReadString());
                case Tag.SkipFirstSession: return new SkipFirstSession(at);
                case Tag.BuyMedicine: return new BuyMedicine(at);
                case Tag.SetEatFromStore: return new SetEatFromStore(at, r.ReadBool());
                case Tag.Offer:
                {
                    string good = r.ReadString();
                    long q = r.ReadLong();
                    return new Offer(at, good, q, r.ReadBool() ? r.ReadFixed() : (Fixed?)null);
                }
                case Tag.Withdraw: return new Withdraw(at, r.ReadString(), r.ReadLong());
                case Tag.SellToTrader: return new SellToTrader(at, r.ReadString(), r.ReadLong());
                case Tag.DeliverOrder: return new DeliverOrder(at, r.ReadLong(), r.ReadLong());
                case Tag.Buy: return new Buy(at, r.ReadString(), r.ReadLong());
                case Tag.GoToMandi:
                {
                    int n = r.ReadCount();
                    var loads = new List<MandiLoad>(n);
                    for (int i = 0; i < n; i++) loads.Add(new MandiLoad(r.ReadString(), r.ReadLong()));
                    return new GoToMandi(at, loads);
                }
                case Tag.HireDayLabourers: return new HireLabourers(at, r.ReadInt()); // retired by ADR 0048: kept in old logs as a hire
                case Tag.HireLabourers: r.ReadInt(); return new HireLabourers(at, r.ReadInt());
                case Tag.BuildHandHut: return new BuildHandHut(at, r.ReadInt());
                case Tag.BuildShed: return new BuildShed(at);
                case Tag.BuyAnimal:
                {
                    string kind = r.ReadString();
                    Financing f = r.ReadBool() ? new Financing(r.ReadString(), r.ReadLong(), r.ReadInt()) : null;
                    return new BuyAnimal(at, kind, f);
                }
                case Tag.SellAnimal: return new SellAnimal(at, r.ReadLong());
                case Tag.SetBuyGreenFodder: return new SetBuyGreenFodder(at, r.ReadBool());
                case Tag.PostJob: return new PostJob(at, r.ReadBool());
                case Tag.Interview: return new Interview(at, r.ReadLong());
                case Tag.OfferContract: return new OfferContract(at, r.ReadLong());
                case Tag.RenewContract: return new RenewContract(at, r.ReadLong());
                case Tag.DismissHand: return new DismissHand(at, r.ReadLong());
                case Tag.SetStandingOrder:
                {
                    long hand = r.ReadLong();
                    var kind = (OrderTargetKind)r.ReadInt();
                    long target = r.ReadLong();
                    string crop = r.ReadString();
                    var seed = (SeedSource)r.ReadInt();
                    string fert = r.ReadString();
                    var sell = (SellRule)r.ReadInt();
                    Fixed min = r.ReadFixed();
                    bool once = r.ReadBool();
                    OrderSpec o = kind == OrderTargetKind.Tree ? OrderSpec.PickTree(target, sell, min)
                        : kind == OrderTargetKind.Animals ? OrderSpec.LookAfterAnimals((int)min.FloorToLong())
                        : OrderSpec.KeepCrop((int)target, crop, seed, fert, sell, min);
                    return new SetStandingOrder(at, hand, once ? o.ThisSeasonOnly() : o);
                }
                case Tag.ClearStandingOrder: return new ClearStandingOrder(at, r.ReadLong(), (OrderTargetKind)r.ReadInt(), r.ReadLong());
                default: throw new SaveFormatException("command tag " + (int)tag);
            }
        }
    }
}
