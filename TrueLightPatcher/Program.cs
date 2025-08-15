using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda;
using Noggog;
using Mutagen.Bethesda.Plugins;

namespace TrueLightPatcher
{
    public class Program
    {
        static ModKey TrueLight { get; } = ModKey.FromFileName("True Light.esm");
        static ModKey[] TrueLightAddons { get; } = [
            ModKey.FromNameAndExtension("True Light - Creation Club.esp"),
            ModKey.FromNameAndExtension("PL - Default.esp"),
            ModKey.FromNameAndExtension("PL - Dark.esp")
        ];

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "TrueLightPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            if (state.LoadOrder.TryGetValue(TrueLight) is not { Mod: not null } TrueLight)
            {
                Console.Error.WriteLine("'True Light.esm' cannot be found. Make sure you have installed True Light.");
                return;
            };

            if (state.LoadOrder.TryGetValue("TL - Default.esp", out var _) && state.LoadOrder.TryGetValue("TL - Dark.esp", out var _))
            {
                Console.Error.WriteLine("You are using both 'TL - Default.esp' and 'TL - Dark.esp', please choose only one Lighting Template plugin.");
                return;
            }

            // Setup list of True Light plugins, adding the Lighting Template override plugins if present
            var TrueLightPlugins = new List<ISkyrimModGetter> { TrueLight.Mod };

            // Add addons to the list of True light plugins if found
            foreach (var modKey in TrueLightAddons)
            {
                if (state.LoadOrder.TryGetValue(modKey) is not { Mod: not null } addon)
                    continue;
                TrueLightPlugins.Add(addon.Mod);
            }

            var loadOrderLinkCache = state.LoadOrder.ToImmutableLinkCache();
            var TrueLightLinkCache = TrueLightPlugins.ToImmutableLinkCache();

            //Find all interior cells where True Light.esm is not already the winner
            var cellContexts = state.LoadOrder.PriorityOrder.Cell()
                .WinningContextOverrides(loadOrderLinkCache)
                .Where(static i => i.ModKey != TrueLight)
                .Where(static i => i.Record.Flags.HasFlag(Cell.Flag.IsInteriorCell))
                .Where(static i => !i.Record.MajorFlags.HasFlag(Cell.MajorFlag.Persistent));

            var cellMask = new Cell.TranslationMask(false)
            {
                Lighting = true
            };

            uint patchedCellCount = 0;
            foreach (var winningCellContext in cellContexts)
            {
                if (!TrueLightLinkCache.TryResolve<ICellGetter>(winningCellContext.Record.FormKey, out var TrueLightCellRecord))
                    continue;

                if (TrueLightCellRecord.Lighting == null)
                    continue;

                // If the winning cell record already has the same lighting values as True Light, skip it.
                if (winningCellContext.Record.Equals(TrueLightCellRecord, cellMask))
                    continue;

                winningCellContext.GetOrAddAsOverride(state.PatchMod).Lighting = TrueLightCellRecord.Lighting.DeepCopy();
                patchedCellCount++;
            }

            uint patchedLightCount = 0;
            foreach (var winningLightRecord in state.LoadOrder.PriorityOrder.Light().WinningOverrides())
            {
                if (!TrueLightLinkCache.TryResolve<ILightGetter>(winningLightRecord.FormKey, out var TrueLightRecord))
                    continue;

                if (!loadOrderLinkCache.TryResolve<ILightGetter>(winningLightRecord.FormKey, out var originLightRecord, ResolveTarget.Origin))
                    continue;

                // Forward Light records if the winning record is using vanilla values
                if (winningLightRecord.Equals(originLightRecord) && !winningLightRecord.Equals(TrueLightRecord))
                {
                    state.PatchMod.Lights.DuplicateInAsNewRecord(TrueLightRecord);
                    patchedLightCount++;
                }
            }

            Console.WriteLine($"Patched {patchedCellCount} cells");
            Console.WriteLine($"Patched {patchedLightCount} lights");
        }
    }
}
