namespace Game.Services
{
    /// <summary>
    /// Whether the full game is unlocked (GDD Monetisation, ADR 0006). A port in the hexagonal sense, like a
    /// Spring interface with one bean per environment: store builds would add StoreKit and Play Billing
    /// implementations behind it.
    /// </summary>
    public interface IEntitlementService
    {
        bool IsFullGameUnlocked { get; }
    }

    /// <summary>Personal builds are always unlocked: there are no purchases (ADR 0007).</summary>
    public sealed class AlwaysUnlockedEntitlementService : IEntitlementService
    {
        public bool IsFullGameUnlocked { get { return true; } }
    }
}
