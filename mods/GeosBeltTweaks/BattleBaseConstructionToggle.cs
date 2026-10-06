namespace GeosBeltTweaks
{
    // Whether the drones of the Battlefield Analysis Bases on a planet build. Repairs are unaffected.
    internal static class BattleBaseConstructionToggle
    {
        internal static bool AnyEnabled(PlanetFactory factory)
        {
            var modules = factory.constructionSystem.constructionModules;
            for (int i = 1; i < modules.cursor; i++)
            {
                if (IsBattleBase(factory, modules.buffer[i], i) && modules.buffer[i].droneConstructEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        // Turns building off for every base if any build, otherwise turns it on for all.
        internal static void Toggle(PlanetFactory factory, Player player)
        {
            bool enable = !AnyEnabled(factory);
            var modules = factory.constructionSystem.constructionModules;
            for (int i = 1; i < modules.cursor; i++)
            {
                var module = modules.buffer[i];
                if (IsBattleBase(factory, module, i) && module.droneConstructEnabled != enable)
                {
                    module.droneConstructEnabled = enable;
                    // A forced search with building off clears the base's pending build targets.
                    module.SearchBuildTargets(factory, player, forced: true);
                }
            }
        }

        // Matches the live-module check in ConstructionSystem.UpdateModules.
        private static bool IsBattleBase(PlanetFactory factory, ConstructionModuleComponent? module, int id)
        {
            return module != null
                && module.id == id
                && module.entityId > 0
                && factory.entityPool[module.entityId].id == module.entityId;
        }
    }
}
