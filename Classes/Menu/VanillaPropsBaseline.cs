/*
 * Poison Menu  Classes/Menu/VanillaPropsBaseline.cs
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

using ExitGames.Client.Photon;
using Photon.Pun;
using Poison.Managers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Valve.Newtonsoft.Json;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Poison.Classes.Menu
{
    public static class VanillaPropsBaseline
    {
        private const string FileName = "VanillaProps.json";
        private static string FilePath => Path.Combine(PluginInfo.BaseDirectory, FileName);
        private static readonly string[] DefaultKeys =
        {
            "didtutorial",
            "platform",
            "mothershipid",
            "gtfcexpirationtime"
        };

        private static HashSet<string> _vanillaKeys = new HashSet<string>();
        public static bool IsLoaded => _vanillaKeys.Count > 0;

        public static bool Capture()
        {
            try
            {
                Hashtable props = PhotonNetwork.LocalPlayer.CustomProperties;
                if (props == null || props.Count == 0)
                {
                    LogManager.LogError("Cannot capture vanilla props: LocalPlayer.CustomProperties is empty. Join a room first.");
                    return false;
                }

                var keys = new List<string>();
                foreach (DictionaryEntry entry in props)
                {
                    string key = (entry.Key?.ToString() ?? "").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(key))
                        keys.Add(key);
                }

                _vanillaKeys = new HashSet<string>(keys);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(keys, Formatting.Indented));
                LogManager.Log($"Captured {_vanillaKeys.Count} vanilla custom property keys to {FilePath}.");
                return true;
            }
            catch (System.Exception e)
            {
                LogManager.LogError($"Failed to capture vanilla props: {e.Message}");
                return false;
            }
        }

        public static bool Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var keys = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FilePath));
                    if (keys != null && keys.Count > 0)
                    {
                        _vanillaKeys = new HashSet<string>(DefaultKeys);
                        _vanillaKeys.UnionWith(keys);
                        LogManager.Log($"Loaded {_vanillaKeys.Count} vanilla custom property keys (defaults + {FilePath}).");
                        return true;
                    }
                }

                _vanillaKeys = new HashSet<string>(DefaultKeys);
                LogManager.Log($"Loaded {_vanillaKeys.Count} default vanilla custom property keys.");
                return true;
            }
            catch (System.Exception e)
            {
                LogManager.LogError($"Failed to load vanilla props baseline: {e.Message}");
                _vanillaKeys = new HashSet<string>(DefaultKeys);
                return true;
            }
        }

        public static IEnumerable<string> FindUnknownKeys(Hashtable props)
        {
            if (_vanillaKeys.Count == 0 || props == null) yield break;
            foreach (DictionaryEntry entry in props)
            {
                string key = (entry.Key?.ToString() ?? "").ToLowerInvariant().Trim();
                if (string.IsNullOrEmpty(key)) continue;
                if (!_vanillaKeys.Contains(key))
                    yield return key;
            }
        }
    }
}
