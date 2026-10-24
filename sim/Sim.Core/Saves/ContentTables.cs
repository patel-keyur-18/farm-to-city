using System;
using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Saves
{
    /// <summary>The ids of each content list a state can point into, in the running content's order (spec 007 §3.1).</summary>
    public static class ContentTables
    {
        public const int Count = (int)ContentTable.Village + 1;

        public static string[] Ids(SimConfig config, ContentTable table)
        {
            GameDefs g = config.Game;
            switch (table)
            {
                case ContentTable.Weather: return Of(config.Weather.Types, d => d.Id);
                case ContentTable.Soil: return g == null ? None : Of(g.Soils, d => d.Id);
                case ContentTable.Drainage: return g == null ? None : Of(g.Drainages, d => d.Id);
                case ContentTable.Parcel: return g == null ? None : Of(g.Map.Parcels, d => d.Id);
                case ContentTable.Building: return g == null ? None : Of(g.Buildings, d => d.Id);
                case ContentTable.Good: return g == null ? None : Of(g.Market.Goods, d => d.Id);
                case ContentTable.Crop: return g == null ? None : Of(g.Farm.Crops, d => d.Id);
                case ContentTable.GardenCrop: return g == null ? None : Of(g.Farm.GardenCrops, d => d.Id);
                case ContentTable.Fertiliser: return g == null ? None : Of(g.Farm.Fertilisers, d => d.Id);
                case ContentTable.Tree: return g == null ? None : Of(g.Farm.Trees, d => d.Id);
                case ContentTable.DietPlan: return g == null ? None : Of(g.Farm.DietPlans, d => d.Id);
                case ContentTable.WorkHours: return g == null ? None : Of(g.Farm.WorkHours, d => d.Id);
                case ContentTable.Lender: return g?.Credit == null ? None : Of(g.Credit.Lenders, d => d.Id);
                case ContentTable.Trait: return g?.People == null ? None : Of(g.People.Traits, d => d.Id);
                case ContentTable.Name: return g?.People == null ? None : Of(g.People.Names, d => d.Id);
                case ContentTable.Step: return g?.Onboarding == null ? None : Of(g.Onboarding.Steps, d => d.Id);
                case ContentTable.Chapter: return g?.Onboarding == null ? None : Of(g.Onboarding.Chapters, d => d.Id);
                case ContentTable.Era: return g?.Onboarding == null ? None : Of(g.Onboarding.Eras, d => d.Id);
                case ContentTable.Animal: return g?.Livestock == null ? None : Of(g.Livestock.Animals, d => d.Id);
                case ContentTable.Village: return g?.Population == null ? None : Of(g.Population.Villages, d => d);
                default: throw new ArgumentOutOfRangeException(nameof(table));
            }
        }

        private static readonly string[] None = new string[0];

        private static string[] Of<T>(IReadOnlyList<T> list, Func<T, string> id)
        {
            var ids = new string[list.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = id(list[i]);
            return ids;
        }
    }
}
