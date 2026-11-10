using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace PhotonJoinFix;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "Rombusz.PhotonJoinFix";
    public const string Name = "PhotonJoinFix";
    public const string Version = "1.3.1";
    internal static ManualLogSource Log = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        ClientBundleLoader.Runner = this;
        _harmony = new Harmony(Guid);
        bool protectionsInstalled = true;
        try
        {
            _harmony.CreateClassProcessor(typeof(HostCleanup)).Patch();
        }
        catch (Exception exception)
        {
            protectionsInstalled = false;
            Log.LogError($"Host cleanup could not be installed: {exception.GetBaseException().Message}");
        }
        try
        {
            ClientSceneGuard.Install(_harmony);
        }
        catch (Exception exception)
        {
            protectionsInstalled = false;
            Log.LogError($"Client scene protection could not be installed: {exception.GetBaseException().Message}");
        }
        try
        {
            ClientBundleLoader.ValidateGameBindings();
            _harmony.CreateClassProcessor(typeof(ClientBundleLoader)).Patch();
            var target = AccessTools.Method(typeof(Photon.Pun.PhotonNetwork), "LoadLevel", new[] { typeof(string) });
            var prefix = AccessTools.Method(typeof(ClientBundleLoader), "BeforeLoad");
            if (Harmony.GetPatchInfo(target)?.Prefixes.Any(p => p.owner == Guid && p.PatchMethod == prefix) != true)
                throw new InvalidOperationException("The bundle-loading prefix is absent from PhotonNetwork.LoadLevel(string).");
        }
        catch (Exception exception)
        {
            protectionsInstalled = false;
            Log.LogError($"Client map bundle loading protection could not be installed: {exception.GetBaseException().Message}");
        }
        SceneManager.sceneUnloaded += ClientSceneGuard.ResetScene;
        if (protectionsInstalled)
            Log.LogMessage($"Plugin {Name} {Version} is loaded!");
        else
            Log.LogWarning($"Plugin {Name} {Version} loaded with incomplete protections. See the installation errors above.");
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= ClientSceneGuard.ResetScene;
        ClientBundleLoader.Cancel();
        ClientBundleLoader.Runner = null;
        _harmony?.UnpatchSelf();
        ClientSceneGuard.Reset();
    }
}
