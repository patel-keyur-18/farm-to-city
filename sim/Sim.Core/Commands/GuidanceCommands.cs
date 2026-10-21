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
}
