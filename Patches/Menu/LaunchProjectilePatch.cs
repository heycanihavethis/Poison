/*
 * Poison Menu  Patches/Menu/LaunchProjectilePatch.cs
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
using Poison.Extensions;
using static Poison.Menu.Main;
using static Poison.Utilities.AssetUtilities;

namespace Poison.Patches.Menu
{
    [HarmonyPatch(typeof(ProjectileWeapon), nameof(ProjectileWeapon.LaunchProjectile))]
    public class LaunchProjectilePatch
    {
        public static bool enabled;

        public static void Prefix(ProjectileWeapon __instance)
        {
            if (enabled)
            {
                GorillaTagger.Instance.rigidbody.linearVelocity = __instance.GetLaunchVelocity();

                if (dynamicSounds)
                    LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Mods/Fun/AngryBirds/launch.ogg", "Audio/Mods/Fun/AngryBirds/launch.ogg", clip => clip.Play(buttonClickVolume / 10f));
            }
        }
    }
}
