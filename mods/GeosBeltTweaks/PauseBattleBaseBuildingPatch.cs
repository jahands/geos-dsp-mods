using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace GeosBeltTweaks
{
    // While Enabled and the belt tool is open, Battlefield Analysis Bases on the local planet do not build.
    [HarmonyPatch]
    internal static class PauseBattleBaseBuildingPatch
    {
        internal static bool Enabled;

        private static PlanetFactory? _pausedFactory;

        // Every method that reads ConstructionModuleComponent.droneConstructEnabled.
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.GameTick));
            yield return AccessTools.Method(typeof(ConstructionModuleComponent), "PlaceItems");
            yield return AccessTools.Method(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.SearchBuildTargets));
            yield return AccessTools.Method(typeof(ConstructionSystem), "UpdateDrones");
            yield return AccessTools.Method(typeof(ConstructionSystem), nameof(ConstructionSystem.AddBuildTargetToModules));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var codes = new List<CodeInstruction>(instructions);
            var constructEnabled = AccessTools.Field(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.droneConstructEnabled));
            var canBuild = AccessTools.Method(typeof(PauseBattleBaseBuildingPatch), nameof(CanBuild));
            bool found = false;
            foreach (var code in codes)
            {
                if (code.LoadsField(constructEnabled))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = canBuild;
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException($"GeosBeltTweaks BAB building pause: droneConstructEnabled not read in {original.DeclaringType.Name}.{original.Name}.");
            }

            return codes;
        }

        private static bool CanBuild(ConstructionModuleComponent module)
        {
            var paused = _pausedFactory?.constructionSystem.constructionModules.buffer;
            return module.droneConstructEnabled && (paused == null || module.id >= paused.Length || paused[module.id] != module);
        }

        internal static void Update()
        {
            var player = GameMain.mainPlayer;
            var factory = Enabled && player?.controller.actionBuild.activeTool is BuildTool_Path ? player.factory : null;
            if (factory == _pausedFactory)
            {
                return;
            }

            var previous = _pausedFactory;
            _pausedFactory = factory;
            if (player == null)
            {
                return;
            }

            // A forced search drops a paused base's build targets and refills them once it resumes.
            if (previous != null && previous.gameData == GameMain.data)
            {
                SearchBuildTargets(previous, player);
            }

            if (factory != null)
            {
                SearchBuildTargets(factory, player);
            }
        }

        private static void SearchBuildTargets(PlanetFactory factory, Player player)
        {
            var modules = factory.constructionSystem.constructionModules;
            for (int i = 1; i < modules.cursor; i++)
            {
                var module = modules.buffer[i];
                if (module != null && module.id == i && module.entityId > 0 && factory.entityPool[module.entityId].id == module.entityId)
                {
                    module.SearchBuildTargets(factory, player, forced: true);
                }
            }
        }
    }
}
