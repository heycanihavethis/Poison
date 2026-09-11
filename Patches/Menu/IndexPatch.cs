/*
 * Poison Menu  Patches/Menu/IndexPatch.cs
 * A community driven mod menu for Gorilla Tag with over 1000+ mods
 *
 * Copyright (C) 2026  Poison Software
 * https://github.com/heycanihavethis/Poison
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using HarmonyLib;
using Poison.Mods;

namespace Poison.Patches.Menu
{
    [HarmonyPatch(typeof(SnowballThrowable), nameof(SnowballThrowable.GetRandomModelIndex))]
    public class IndexPatch
    {
        public static bool enabled;

        public static bool Prefix(SnowballThrowable __instance, ref int __result)
        {
            if (enabled)
            {
                if (__instance.localModels.Count == 0)
                {
                    __result = -1;
                    return false;
                }

                __instance.randModelIndex = Projectiles.targetProjectileIndex % __instance.localModels.Count;
                __result = Projectiles.targetProjectileIndex;

                return false;
            }

            return true;
        }
    }
}
