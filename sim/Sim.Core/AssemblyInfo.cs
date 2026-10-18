using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Sim.Content")]
[assembly: InternalsVisibleTo("Sim.Core.Tests")]

// The balancer's economy model sells to the trader before the SellToTrader command exists (Sprint 3).
[assembly: InternalsVisibleTo("Tools.Balancer")]
