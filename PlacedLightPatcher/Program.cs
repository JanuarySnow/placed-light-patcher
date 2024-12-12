using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;

namespace PlacedLightPatcher
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "PlacedLightPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var loadOrderLinkCache = state.LoadOrder.ToImmutableLinkCache();

            // Loop through winning cell records (with context) in load order
            foreach (var winningCellContext in state.LoadOrder.PriorityOrder.Cell().WinningContextOverrides(loadOrderLinkCache))
            {
            };
        }
    }
}
