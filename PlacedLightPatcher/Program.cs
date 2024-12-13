using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Mutagen.Bethesda.Plugins.Cache;

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
                Console.Error.WriteLine("`Placed Light.esp` cannot be found. Make sure you have installed Placed Light.");
                return;
            };

            var loadOrderLinkCache = state.LoadOrder.ToImmutableLinkCache();
            var placedLightLinkCache = placedLight.Mod.ToImmutableLinkCache();


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
