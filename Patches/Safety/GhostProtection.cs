/*
 * Poison Menu  Patches/Safety/GhostProtection.cs
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
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Poison.Managers;
using static Poison.Patches.PatchHandler;

namespace Poison.Patches.Safety
{
    public class GhostProtection
    {
        public static bool enabled = true;

        public static bool Notify = true;

        [SecurityPatch]
        [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.OnEvent), typeof(EventData))]
        public class OnEventPatch
        {
            private static bool Prefix(EventData photonEvent)
            {
                if (!enabled)
                    return true;

                try
                {
                    if (photonEvent == null || photonEvent.Code != (byte)PunEvent.DestroyPlayer)
                        return true;

                    if (!(photonEvent.CustomData is Hashtable eventData))
                        return true;

                    if (!(eventData[0] is int targetActorNumber))
                        return true;

                    Player localPlayer = PhotonNetwork.LocalPlayer;
                    int localActorNumber = localPlayer == null ? -1 : localPlayer.ActorNumber;
                    int masterActorNumber = PhotonNetwork.CurrentRoom == null ? -1 : PhotonNetwork.CurrentRoom.MasterClientId;

                    bool targetsSelf = targetActorNumber < 0 || targetActorNumber == localActorNumber;
                    bool fromMaster = photonEvent.Sender == masterActorNumber;

                    if (!targetsSelf && fromMaster)
                        return true;

                    Blocked(targetsSelf, photonEvent.Sender, targetActorNumber);
                    return false;
                }
                catch
                {
                    return true;
                }
            }

            private static void Blocked(bool targetsSelf, int sender, int targetActorNumber)
            {
                LogManager.LogWarning($"Blocked a {(targetsSelf ? "ghost" : "isolate")} event (207) from actor {sender} targeting actor {targetActorNumber}.");

                if (!Notify || !PhotonNetwork.InRoom)
                    return;

                try
                {
                    NotificationManager.SendNotification($"<color=grey>[</color><color=green>ANTI-GHOST</color><color=grey>]</color> Blocked an attempt to make {(targetsSelf ? "you" : "another player")} invisible.");
                }
                catch { }
            }
        }
    }
}
