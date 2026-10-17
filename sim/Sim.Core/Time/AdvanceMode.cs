namespace Sim.Core.Time
{
    /// <summary>How time is being advanced (spec 001 §3.4).</summary>
    public enum AdvanceMode
    {
        /// <summary>The player is in the app.</summary>
        Live = 0,

        /// <summary>Catching up after the app was closed: away rules apply (ADR 0002).</summary>
        Away = 1,
    }
}
