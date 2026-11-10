using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Progress;

namespace Sim.Core.Commands
{
    /// <summary>Marks a letter chapter seen (spec 009 §5). Seeing it again changes nothing.</summary>
    public sealed class SeeChapter : TimedCommand
    {
        public string ChapterId { get; }

        public SeeChapter(long at, string chapterId) : base(at) { ChapterId = chapterId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            OnboardingDefs d = sim.Config.Game?.Onboarding;
            int c = d == null ? -1 : d.IndexOfChapter(ChapterId);
            if (c < 0) return CommandResult.Reject(RejectReason.UnknownChapter);
            GuidanceState g = sim.State.Guidance;
            if (!g.HasSeen(c)) g.Seen.Add(c);
            return CommandResult.Ok;
        }
    }

    /// <summary>Advances the first session past its current step (spec 009 §5); the view sends it when the step's goal holds.</summary>
    public sealed class CompleteStep : TimedCommand
    {
        public string StepId { get; }

        public CompleteStep(long at, string stepId) : base(at) { StepId = stepId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            OnboardingDefs d = sim.Config.Game?.Onboarding;
            int s = d == null ? -1 : d.IndexOfStep(StepId);
            if (s < 0) return CommandResult.Reject(RejectReason.UnknownStep);
            GuidanceState g = sim.State.Guidance;
            if (g.Skipped || s != g.Step) return CommandResult.Reject(RejectReason.NotCurrentStep);
            g.Step++;
            return CommandResult.Ok;
        }
    }

    /// <summary>"I'll find my own way" (spec 009 §3.2): ends the first session; the week's chapters still come.</summary>
    public sealed class SkipFirstSession : TimedCommand
    {
        public SkipFirstSession(long at) : base(at) { }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Guidance == null) return CommandResult.Reject(RejectReason.UnknownStep);
            sim.State.Guidance.Skipped = true;
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Records Codex cards found (spec 018 §3.3, ADR 0095 §7): the view layer sees the moment, with the player's numbers then, and sends
    /// every card found in a pass together, so a replay finds the same. Each card is found once; any card already found or unknown, or
    /// numbers that don't match its formats, refuse the lot.
    /// </summary>
    public sealed class FindCodexCards : TimedCommand
    {
        public IReadOnlyList<CodexFind> Cards { get; }

        public FindCodexCards(long at, IReadOnlyList<CodexFind> cards) : base(at) { Cards = cards; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            CodexDefs d = sim.Config.Game?.Codex;
            GuidanceState g = sim.State.Guidance;
            if (d == null || g == null || Cards == null || Cards.Count == 0) return CommandResult.Reject(RejectReason.UnknownCodexCard);
            var seen = new List<string>();
            foreach (CodexFind f in Cards)
            {
                int i = d.IndexOf(f.Id);
                if (i < 0 || f.Numbers.Length != d.Cards[i].Numbers.Count) return CommandResult.Reject(RejectReason.UnknownCodexCard);
                if (g.Found(f.Id) != null || seen.Contains(f.Id)) return CommandResult.Reject(RejectReason.CodexCardState);
                seen.Add(f.Id);
            }
            long t = sim.State.Clock.SimTime;
            foreach (CodexFind f in Cards)
            {
                g.Codex.Add(new CodexFind(f.Id, (long[])f.Numbers.Clone()) { FoundAt = t });
                sim.Emit(new Events.CodexCardFound(t, f.Id));
            }
            return CommandResult.Ok;
        }
    }

    /// <summary>Marks a found Codex card read (spec 018 §3.3): its New tag goes. Reading it again changes nothing.</summary>
    public sealed class ReadCodexCard : TimedCommand
    {
        public string CardId { get; }

        public ReadCodexCard(long at, string cardId) : base(at) { CardId = cardId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            CodexFind f = sim.State.Guidance?.Found(CardId);
            if (f == null) return CommandResult.Reject(sim.Config.Game?.Codex?.IndexOf(CardId) >= 0 ? RejectReason.CodexCardState : RejectReason.UnknownCodexCard);
            f.Read = true;
            return CommandResult.Ok;
        }
    }
}
