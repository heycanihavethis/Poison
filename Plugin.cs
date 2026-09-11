/*
 * Poison Menu  Plugin.cs
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

using Poison.Managers;
using Poison.Menu;
using UnityEngine;

namespace Poison
{
    public static class Plugin
    {
        // For SharpMonoInjector usage :3
        // Don't merge these methods, it just doesn't work
        public static void Inject()
        {
            var go = new GameObject("Poison");
            go.AddComponent<Injector>();
        }

        public static void InjectDontDestroy()
        {
            var go = new GameObject("Poison");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Injector>();
        }

        private sealed class Injector : MonoBehaviour
        {
            private void Awake()
            {
                LogManager.SetLogger((Level level, string msg) =>
                {
                    switch (level)
                    {
                        case Level.Error:
                            Debug.LogError(msg);
                            break;
                        case Level.Warning:
                            Debug.LogWarning(msg);
                            break;
                        default:
                            Debug.Log(msg);
                            break;
                    }
                });

                Bootstrapper.Initialize();
            }

            private void OnDestroy() =>
                Main.UnloadMenu();
        }
    }
}