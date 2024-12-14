using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda;
using Noggog;

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
            if (!state.LoadOrder.TryGetValue("Placed Light.esp", out var placedLight) || placedLight.Mod is null)
            {
                Console.Error.WriteLine("'Placed Light.esp' cannot be found. Make sure you have installed Placed Light.");
                return;
            };

            if (state.LoadOrder.TryGetValue("PL - Dark.esp", out var _) && state.LoadOrder.TryGetValue("PL - Darker.esp", out var _))
            {
                Console.Error.WriteLine("You are using both 'PL - Dark.esp' and 'PL - Darker.esp', please choose only one Lighting Template plugin.");
                return;
            }

            // Setup list of Placed Light plugins, adding the Lighting Template override plugins if present
            var placedLightPlugins = new List<ISkyrimModGetter> { placedLight.Mod };
            foreach (var pluginName in new[] { "PL - Dark.esp", "PL - Darker.esp" })
            {
                if (state.LoadOrder.TryGetValue(pluginName, out var plugin) && plugin.Mod is not null)
                    placedLightPlugins.Add(plugin.Mod);
            }

            var loadOrderLinkCache = state.LoadOrder.ToImmutableLinkCache();
            var placedLightLinkCache = placedLightPlugins.ToImmutableLinkCache();

            uint patchedCellCount = 0;
            foreach (var winningCellContext in state.LoadOrder.PriorityOrder.Cell().WinningContextOverrides(loadOrderLinkCache))
            {
                if (!placedLightLinkCache.TryResolve<ICellGetter>(winningCellContext.Record.FormKey, out var placedLightCellRecord))
                    continue;

                var winningLighting = winningCellContext.Record.Lighting;
                if (winningLighting is null)
                    continue;

                // Forward PL's values
                var patchRecord = winningCellContext.GetOrAddAsOverride(state.PatchMod);
                if (placedLightCellRecord.Lighting is not null)
                {
                    patchRecord.Lighting = placedLightCellRecord.Lighting.DeepCopy();
                    patchRecord.LightingTemplate.FormKey = placedLightCellRecord.LightingTemplate.FormKey;
                    patchRecord.SkyAndWeatherFromRegion.FormKey = placedLightCellRecord.SkyAndWeatherFromRegion.FormKey;
                    patchedCellCount++;
                };
            }

            uint patchedLightCount = 0;
            foreach (var winningLightRecord in state.LoadOrder.PriorityOrder.Light().WinningOverrides())
            {
                if (!placedLightLinkCache.TryResolve<ILightGetter>(winningLightRecord.FormKey, out var placedLightRecord))
                    continue;

                if (!loadOrderLinkCache.TryResolve<ILightGetter>(winningLightRecord.FormKey, out var originLightRecord, ResolveTarget.Origin))
                    continue;

                // Forward Light records if the winning record is using vanilla values
                if (winningLightRecord.Equals(originLightRecord) && !winningLightRecord.Equals(placedLightRecord))
                {
                    state.PatchMod.Lights.DuplicateInAsNewRecord(placedLightRecord);
                    patchedLightCount++;
                }
            }

            Console.WriteLine($"Patched {patchedCellCount} cells");
            Console.WriteLine($"Patched {patchedLightCount} lights");
        }
    }
}
