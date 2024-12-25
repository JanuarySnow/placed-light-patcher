using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda;
using Noggog;
using Mutagen.Bethesda.Plugins;

namespace PlacedLightPatcher
{
    public class Program
    {
        static ModKey PlacedLight { get; } = ModKey.FromFileName("Placed Light.esm");
        static ModKey[] PlacedLightAddons { get; } = [
            ModKey.FromNameAndExtension("Placed Light - CC.esp"),
            ModKey.FromNameAndExtension("PL - Default.esp"),
            ModKey.FromNameAndExtension("PL - Dark.esp")
        ];

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "PlacedLightPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            if (state.LoadOrder.TryGetValue(PlacedLight) is not { Mod: not null } placedLight)
            {
                Console.Error.WriteLine("'Placed Light.esm' cannot be found. Make sure you have installed Placed Light.");
                return;
            };

            if (state.LoadOrder.TryGetValue("PL - Default.esp", out var _) && state.LoadOrder.TryGetValue("PL - Dark.esp", out var _))
            {
                Console.Error.WriteLine("You are using both 'PL - Default.esp' and 'PL - Dark.esp', please choose only one Lighting Template plugin.");
                return;
            }

            // Setup list of Placed Light plugins, adding the Lighting Template override plugins if present
            var placedLightPlugins = new List<ISkyrimModGetter> { placedLight.Mod };

            // Add addons to the list of placed light plugins if found
            foreach (var modKey in PlacedLightAddons)
            {
                if (state.LoadOrder.TryGetValue(modKey) is not { Mod: not null } addon)
                    continue;
                placedLightPlugins.Add(addon.Mod);
            }

            var loadOrderLinkCache = state.LoadOrder.ToImmutableLinkCache();
            var placedLightLinkCache = placedLightPlugins.ToImmutableLinkCache();

            //Find all interior cells where Placed Light.esm is not already the winner
            var cellContexts = state.LoadOrder.PriorityOrder.Cell()
                .WinningContextOverrides(loadOrderLinkCache)
                .Where(static i => i.ModKey != PlacedLight)
                .Where(static i => i.Record.Flags.HasFlag(Cell.Flag.IsInteriorCell))
                .Where(static i => !i.Record.MajorFlags.HasFlag(Cell.MajorFlag.Persistent));

            var cellMask = new Cell.TranslationMask(false)
            {
                Lighting = true
            };

            uint patchedCellCount = 0;
            foreach (var winningCellContext in cellContexts)
            {
                if (!placedLightLinkCache.TryResolve<ICellGetter>(winningCellContext.Record.FormKey, out var placedLightCellRecord))
                    continue;

                if (placedLightCellRecord.Lighting == null)
                    continue;

                // If the winning cell record already has the same lighting values as Placed Light, skip it.
                if (winningCellContext.Record.Equals(placedLightCellRecord, cellMask))
                    continue;

                winningCellContext.GetOrAddAsOverride(state.PatchMod).Lighting = placedLightCellRecord.Lighting.DeepCopy();
                patchedCellCount++;
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
