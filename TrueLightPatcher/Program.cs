using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace TrueLightPatcher
{
    public class Program
    {
        static ModKey TrueLight { get; } = ModKey.FromFileName("True Light.esm");
        static ModKey dial { get; } = ModKey.FromNameAndExtension("dial.esp");

        static ModKey[] TrueLightAddons { get; } = new[]
        {
            ModKey.FromNameAndExtension("True Light - Creation Club.esp"),
            ModKey.FromNameAndExtension("True Light - USSEP Patch.esp"),
            ModKey.FromNameAndExtension("TL Bulbs ISL.esp"),
            ModKey.FromNameAndExtension("TL - WSU Patch.esp"),
            ModKey.FromNameAndExtension("TL - Default.esp"),
            ModKey.FromNameAndExtension("TL - Bright.esp"),
            ModKey.FromNameAndExtension("TL - Even Brighter.esp"),
            ModKey.FromNameAndExtension("TL - Fixed Vanilla.esp"),
            ModKey.FromNameAndExtension("TL - Nightmare.esp"),
            ModKey.FromNameAndExtension("True Light - Shadows and Ambient.esp"),
        };

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "TrueLightPatcher.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // True Light master required
            if (state.LoadOrder.TryGetValue(TrueLight) is not { Mod: not null } trueLightListing)
            {
                Console.Error.WriteLine("'True Light.esm' cannot be found. Make sure you have installed True Light.");
                return;
            }

            // Build the list of True Light plugins
            var trueLightPlugins = new List<ISkyrimModGetter> { trueLightListing.Mod };
            foreach (var mk in TrueLightAddons)
            {
                if (state.LoadOrder.TryGetValue(mk) is { Mod: not null } listing)
                    trueLightPlugins.Add(listing.Mod);
            }

            var loadOrderCache = state.LoadOrder.ToImmutableLinkCache();
            var trueLightCache = trueLightPlugins.ToImmutableLinkCache();

            // Iterate all Cell winners
            var cellWinners = state.LoadOrder.PriorityOrder
                .Cell()
                .WinningContextOverrides(loadOrderCache);

            // Predicates
            static bool IsWSU(ModKey mk) =>
                mk.FileName.String.Contains("WSU - ", StringComparison.OrdinalIgnoreCase);

            static bool IsWindowShadows(ModKey mk)
            {
                var s = mk.FileName.String;
                return s.Contains("Window Shadows", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("Windows Shadows", StringComparison.OrdinalIgnoreCase);
            }

            uint patchedCells = 0;

            foreach (var winCtx in cellWinners)
            {
                var fk = winCtx.Record.FormKey;

                var contexts = fk.ToLink<ICellGetter>()
                    .ResolveAllContexts<ISkyrimMod, ISkyrimModGetter, ICell, ICellGetter>(loadOrderCache)
                    .ToList();
                if (contexts.Count == 0) continue;

                var touchingKeys = contexts.Select(c => c.ModKey).ToList();

                bool touchesDial = touchingKeys.Contains(dial);
                bool touchesWSU = touchingKeys.Any(IsWSU);
                bool touchesWS = contexts.Skip(1).Any(c => IsWindowShadows(c.ModKey)); // losers only
                bool touchesTL = touchingKeys.Contains(TrueLight)
                                  || touchingKeys.Any(mk => TrueLightAddons.Contains(mk));

                // If the record isn't touched by any of the requested families, skip
                if (!(touchesDial || touchesWSU || touchesWS || touchesTL))
                    continue;

                // 1) Highest-priority WSU (winner-first)
                var wsuCtx = contexts.FirstOrDefault(c => IsWSU(c.ModKey));

                // 2) Otherwise a Window Shadows entry that is overridden by the winner
                var wsCtx = (wsuCtx is null)
                    ? contexts.Skip(1).FirstOrDefault(c => IsWindowShadows(c.ModKey))
                    : null;

                // 3) Otherwise TL fallback
                ICellGetter? tlCell = null;
                if (wsuCtx is null && wsCtx is null)
                {
                    if (!trueLightCache.TryResolve<ICellGetter>(fk, out tlCell) || tlCell.Lighting is null)
                        continue; // nothing to do
                }

                ICell patchCell;
                if (wsuCtx is not null)
                {
                    // Copy ALL subrecords from WSU version
                    patchCell = wsuCtx.GetOrAddAsOverride(state.PatchMod);
                }
                else if (wsCtx is not null)
                {
                    // Copy ALL subrecords from Window Shadows version
                    patchCell = wsCtx.GetOrAddAsOverride(state.PatchMod);
                }
                else
                {
                    // Fallback
                    patchCell = winCtx.GetOrAddAsOverride(state.PatchMod);
                    patchCell.Lighting = tlCell!.Lighting!.DeepCopy();
                }

                // After the main forward, if dial.esp touches this record,
                // override ONLY DATA-Flags (Cell.Flags) and XCCM (Cell.Climate)
                if (touchesDial)
                {
                    var dialCtx = contexts.First(c => c.ModKey == dial);
                    patchCell.Flags = dialCtx.Record.Flags;
                    if (dialCtx.Record.SkyAndWeatherFromRegion is { IsNull: false })
                        patchCell.SkyAndWeatherFromRegion.SetTo(dialCtx.Record.SkyAndWeatherFromRegion);
                    else
                        patchCell.SkyAndWeatherFromRegion.Clear();

                }

                patchedCells++;
            }

            uint patchedLights = 0;
            var trueLightAllModsCache = trueLightPlugins.ToImmutableLinkCache();
            foreach (var winningLight in state.LoadOrder.PriorityOrder.Light().WinningOverrides())
            {
                if (!trueLightAllModsCache.TryResolve<ILightGetter>(winningLight.FormKey, out var tlLight))
                    continue;

                if (!loadOrderCache.TryResolve<ILightGetter>(winningLight.FormKey, out var _,
                        ResolveTarget.Origin))
                    continue;

                state.PatchMod.Lights.DuplicateInAsNewRecord(tlLight);
                patchedLights++;
            }

            Console.WriteLine($"Patched {patchedCells} cells");
            Console.WriteLine($"Patched {patchedLights} lights");
        }
    }
}
