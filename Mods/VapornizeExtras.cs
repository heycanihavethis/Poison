/*
 * Vapornize — Mods/VapornizeExtras.cs
 * New local-only utility features for the Vapornize fork.
 *
 * Based on Poison Menu; original copyright and license notices are retained
 * in the upstream source files. Licensed under GPL-3.0-or-later.
 */

using Photon.Pun;
using Poison.Managers;

namespace Poison.Mods
{
    public static class VapornizeExtras
    {
        // Displays room information locally. This does not send RPCs or modify room state.
        public static void SessionSnapshot()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                NotificationManager.SendNotification(
                    "<color=grey>[</color><color=yellow>VAPORNIZE</color><color=grey>]</color> You are not currently in a room.");
                return;
            }

            string roomName = PhotonNetwork.CurrentRoom.Name;
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            string gameMode = "Unknown";

            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("gameMode"))
                gameMode = PhotonNetwork.CurrentRoom.CustomProperties["gameMode"]?.ToString() ?? "Unknown";

            NotificationManager.SendNotification(
                $"<color=grey>[</color><color=purple>SESSION SNAPSHOT</color><color=grey>]</color> " +
                $"Room: {roomName} | Players: {playerCount} | Mode: {gameMode}");
        }
    }
}
