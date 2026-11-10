using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine.SceneManagement;

namespace PhotonJoinFix;

[HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.LoadLevel), typeof(string))]
internal static class HostCleanup
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void BeforeLoad(string levelName, bool __runOriginal)
    {
        if (!__runOriginal || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        Scene source = SceneManager.GetActiveScene();
        if (!IsAirportDeparture(source.name, levelName))
            return;

        try
        {
            RemoveAirportItems(source);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning($"Airport item cleanup could not finish: {exception.Message}");
        }
    }

    internal static bool IsAirportDeparture(string source, string destination)
    {
        return source != null && source.IndexOf("Airport", StringComparison.Ordinal) >= 0
            && destination != null
            && (destination.IndexOf("Island", StringComparison.Ordinal) >= 0
                || destination.IndexOf("Level_", StringComparison.Ordinal) >= 0);
    }

    private static void RemoveAirportItems(Scene airport)
    {
        var room = PhotonNetwork.CurrentRoom;
        var roots = new List<PhotonView>();
        var seen = new HashSet<int>();
        foreach (PhotonView view in PhotonNetwork.PhotonViewCollection)
        {
            if (view == null || !view.isRuntimeInstantiated || !view.IsRoomView
                || view.InstantiationId <= 0 || !seen.Add(view.InstantiationId))
                continue;

            PhotonView root = PhotonNetwork.GetPhotonView(view.InstantiationId);
            if (IsCleanupCandidate(root, airport))
                roots.Add(root);
        }

        int removed = 0;
        foreach (PhotonView root in roots)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient
                || !ReferenceEquals(PhotonNetwork.CurrentRoom, room)
                || SceneManager.GetActiveScene() != airport)
                break;
            if (!IsCleanupCandidate(root, airport))
                continue;

            int id = root.ViewID;
            string itemName = root.gameObject.name;
            try
            {
                PhotonNetwork.Destroy(root.gameObject);
                if (PhotonNetwork.GetPhotonView(id) == null)
                    removed++;
                else
                    Plugin.Log.LogWarning($"Airport item '{itemName}' (view {id}) was not removed.");
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning($"Could not remove airport item '{itemName}' (view {id}): {exception.Message}");
            }
        }

        if (roots.Count > 0 && PhotonNetwork.InRoom
            && ReferenceEquals(PhotonNetwork.CurrentRoom, room))
        {
            PhotonNetwork.SendAllOutgoingCommands();
            Plugin.Log.LogInfo($"Removed {removed} airport room item(s) and their cached spawn events before expedition load.");
        }
    }

    private static bool IsCleanupCandidate(PhotonView root, Scene airport)
    {
        if (root == null || !root.isRuntimeInstantiated || !root.IsRoomView || !root.IsMine
            || root.InstantiationId <= 0 || root.ViewID != root.InstantiationId
            || !ReferenceEquals(PhotonNetwork.GetPhotonView(root.ViewID), root)
            || root.gameObject.scene != airport)
            return false;

        Item item = root.GetComponent<Item>();
        return item != null && item.itemState == ItemState.Ground
            && item.holderCharacter == null && item.wearerCharacter == null
            && root.GetComponentInParent<Character>() == null
            && root.GetComponentInParent<Player>() == null;
    }
}
