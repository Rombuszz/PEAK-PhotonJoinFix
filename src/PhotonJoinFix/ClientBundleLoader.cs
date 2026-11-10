using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PhotonJoinFix
{
    [HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.LoadLevel), new[] { typeof(string) })]
    internal static class ClientBundleLoader
    {
        private sealed class PendingLoad
        {
            internal readonly Room Room;
            internal readonly string Scene;
            internal readonly object? RoomScene;
            internal readonly bool QueueWasRunning;

            internal PendingLoad(Room room, string scene)
            {
                Room = room;
                Scene = scene;
                RoomScene = room.CustomProperties["curScn"];
                QueueWasRunning = PhotonNetwork.IsMessageQueueRunning;
            }
        }

        internal static MonoBehaviour? Runner;
        private static PendingLoad? _pending;
        private static bool _resuming;
        private static readonly FieldInfo? LoadedBundleField = AccessTools.Field(typeof(MapBundleService.MapBundleData), "m_assetBundle");

        internal static void ValidateGameBindings()
        {
            if (LoadedBundleField == null || LoadedBundleField.FieldType != typeof(AssetBundle))
                throw new MissingFieldException("MapBundleData.m_assetBundle is unavailable; cannot verify map loading safely.");
        }

        private static bool IsJoiningOrJoinedGuest(Room? room)
            => room != null && !PhotonNetwork.IsMasterClient
                && (PhotonNetwork.NetworkClientState == ClientState.Joining
                    || PhotonNetwork.NetworkClientState == ClientState.Joined);

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool BeforeLoad(string levelName)
        {
            if (_resuming)
                return true;

            Room? room = PhotonNetwork.CurrentRoom;
            if (_pending != null && ReferenceEquals(room, _pending.Room) && levelName == _pending.Scene)
                return false;

            Cancel();

            if (!IsJoiningOrJoinedGuest(room) || room == null
                || !TryGetMapIndex(levelName, out int index))
                return true;

            if (Runner == null)
            {
                Plugin.Log.LogError($"Cannot prepare '{levelName}': the bundle loader is unavailable. Rejoin after restarting the game.");
                return false;
            }

            var pending = new PendingLoad(room, levelName);
            _pending = pending;

            PhotonNetwork.IsMessageQueueRunning = false;
            try
            {
                Runner.StartCoroutine(LoadBundleThenResume(pending, index));
            }
            catch (Exception exception)
            {
                Fail(pending, exception.GetBaseException().Message);
            }
            return false;
        }

        private static bool TryGetMapIndex(string scene, out int index)
        {
            index = -1;
            return scene != null && scene.StartsWith("Level_", StringComparison.Ordinal)
                && int.TryParse(scene.Substring(6), NumberStyles.None, CultureInfo.InvariantCulture, out index)
                && index >= 0;
        }

        private static bool IsCurrent(PendingLoad pending)
        {
            return ReferenceEquals(_pending, pending) && IsJoiningOrJoinedGuest(PhotonNetwork.CurrentRoom)
                && ReferenceEquals(PhotonNetwork.CurrentRoom, pending.Room)
                && Equals(pending.Room.CustomProperties["curScn"], pending.RoomScene);
        }

        private static IEnumerator LoadBundleThenResume(PendingLoad pending, int index)
        {
            MapBundleService.MapBundleData? bundle;
            try
            {
                var service = GameHandler.GetService<MapBundleService>();
                bundle = service?.LoadBundlesNearestIndex(index);
            }
            catch (Exception exception)
            {
                Fail(pending, exception.GetBaseException().Message);
                yield break;
            }

            if (bundle == null)
            {
                Fail(pending, "No installed map bundle contains this map index.");
                yield break;
            }

            Plugin.Log.LogInfo($"Preparing the map bundle for '{pending.Scene}' before Photon loads the scene.");
            float deadline = Time.realtimeSinceStartup + 120f;
            while (bundle.Loading)
            {
                if (!IsCurrent(pending))
                {
                    Cancel(pending);
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(pending, "Timed out waiting for the map bundle.");
                    yield break;
                }
                yield return null;
            }

            if (!IsCurrent(pending))
            {
                Cancel(pending);
                yield break;
            }

            try
            {
                if (!ContainsLoadedScene(bundle, pending.Scene))
                {
                    Fail(pending, "The map bundle failed to load or does not contain the requested scene.");
                    yield break;
                }
            }
            catch (Exception exception)
            {
                Fail(pending, "Could not verify the loaded map bundle: " + exception.GetBaseException().Message);
                yield break;
            }

            _pending = null;
            _resuming = true;
            try
            {
                Plugin.Log.LogInfo($"Map bundle ready for '{pending.Scene}'; resuming Photon scene loading.");
                PhotonNetwork.LoadLevel(pending.Scene)
            }
            catch (Exception exception)
            {
                PhotonNetwork.IsMessageQueueRunning = pending.QueueWasRunning;
                Plugin.Log.LogError($"Photon could not load '{pending.Scene}': {exception.GetBaseException().Message}");
            }
            finally
            {
                _resuming = false;
            }
        }

        private static bool ContainsLoadedScene(MapBundleService.MapBundleData bundle, string scene)
        {
            ValidateGameBindings();
            var asset = LoadedBundleField!.GetValue(bundle) as AssetBundle;
            if (asset == null) return false;
            foreach (string path in asset.GetAllScenePaths())
                if (string.Equals(Path.GetFileNameWithoutExtension(path), scene, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static void Fail(PendingLoad pending, string reason)
        {
            Plugin.Log.LogError($"Cannot prepare '{pending.Scene}': {reason} Leave and rejoin after correcting the map-file problem.");
            Cancel(pending);
        }

        private static void Cancel(PendingLoad pending)
        {
            if (!ReferenceEquals(_pending, pending))
                return;
            _pending = null;
            PhotonNetwork.IsMessageQueueRunning = pending.QueueWasRunning;
        }

        internal static void Cancel()
        {
            if (_pending != null)
                Cancel(_pending);
        }
    }
}
