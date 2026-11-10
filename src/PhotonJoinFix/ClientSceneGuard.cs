using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using InstantiateParameters = Photon.Pun.InstantiateParameters;

namespace PhotonJoinFix
{
    internal static class ClientSceneGuard
    {
        private sealed class PreservedView
        {
            internal readonly PhotonView View;
            internal readonly int SceneHandle;
            internal bool DestroyWarningLogged;

            internal PreservedView(PhotonView view)
            {
                View = view;
                SceneHandle = view.gameObject.scene.handle;
            }
        }

        private static readonly Dictionary<int, PreservedView> Preserved = new Dictionary<int, PreservedView>();
        private static Room? _room;

        internal static void Install(Harmony harmony)
        {
            var instantiate = AccessTools.Method(typeof(PhotonNetwork), "NetworkInstantiate",
                new[] { typeof(InstantiateParameters), typeof(bool), typeof(bool) });
            var onEvent = AccessTools.Method(typeof(PhotonNetwork), "OnEvent", new[] { typeof(EventData) });
            if (instantiate == null || onEvent == null)
                throw new MissingMethodException("The required Photon instantiation/event methods were not found.");

            var instantiatePrefix = AccessTools.Method(typeof(ClientSceneGuard), nameof(BeforeInstantiate));
            harmony.Patch(instantiate, prefix: new HarmonyMethod(instantiatePrefix));
            try
            {
                harmony.Patch(onEvent, prefix: new HarmonyMethod(typeof(ClientSceneGuard), nameof(BeforeEvent)));
            }
            catch
            {
                harmony.Unpatch(instantiate, instantiatePrefix);
                throw;
            }
        }

        internal static void Reset()
        {
            Preserved.Clear();
            _room = null;
        }

        internal static void ResetScene(Scene scene)
        {
            if (Preserved.Count == 0)
                return;

            var removed = new List<int>();
            foreach (var entry in Preserved)
            {
                if (entry.Value.View == null || entry.Value.SceneHandle == scene.handle)
                    removed.Add(entry.Key);
            }
            foreach (int id in removed)
                Preserved.Remove(id);
        }

        private static void CheckRoom()
        {
            Room? current = PhotonNetwork.CurrentRoom;
            if (ReferenceEquals(current, _room))
                return;

            Preserved.Clear();
            _room = current;
        }

        private static bool BeforeInstantiate(InstantiateParameters parameters, bool instantiateEvent, ref GameObject? __result)
        {
            if (!instantiateEvent || parameters.viewIDs == null)
                return true;

            CheckRoom();
            bool collision = false;
            foreach (int id in parameters.viewIDs)
            {
                if (id <= 0)
                    continue;

                PhotonView? existing = PhotonNetwork.GetPhotonView(id);
                if (existing == null || existing.isRuntimeInstantiated)
                    continue;

                collision = true;
                if (Preserved.TryGetValue(id, out var saved) && ReferenceEquals(saved.View, existing))
                    continue;

                Preserved[id] = new PreservedView(existing);
                Plugin.Log.LogWarning($"Skipped incoming '{parameters.prefabName}': view ID {id} belongs to scene object '{existing.name}'. Preserving the scene object.");
            }

            if (!collision)
                return true;

            __result = null;
            return false;
        }

        private static bool BeforeEvent(EventData photonEvent)
        {
            if (photonEvent.Code != 204)
                return true;

            CheckRoom();
            if (!(photonEvent.CustomData is Hashtable data) || !(data[(byte)0] is int id) ||
                !Preserved.TryGetValue(id, out var saved))
                return true;

            PhotonView? current = PhotonNetwork.GetPhotonView(id);
            if (current == null || current.isRuntimeInstantiated || !ReferenceEquals(current, saved.View))
            {
                Preserved.Remove(id);
                return true;
            }

            if (!saved.DestroyWarningLogged)
            {
                saved.DestroyWarningLogged = true;
                Plugin.Log.LogWarning($"Ignored ambiguous destroy for colliding view ID {id}; preserved scene object '{current.name}'.");
            }
            return false;
        }
    }
}
