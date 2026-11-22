using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

namespace PumpkinPanicCoop.Editor
{
    public static class GeneratePhotonServerSettings
    {
        public static void ValidateScripts()
        {
            CreateOrUpdate();
            Debug.Log("[PumpkinPanicCoop] Fast validation completed: Unity loaded the project and compiled available scripts.");
        }

        public static void BuildWebGL()
        {
            CreateOrUpdate();
            Debug.Log("[PumpkinPanicCoop] Starting WebGL build in the same Unity Editor process.");
            Buildalon.Editor.BuildPipeline.UnityPlayerBuildTools.StartCommandLineBuild();
        }

        public static void CreateOrUpdate()
        {
            string appId = Environment.GetEnvironmentVariable("PHOTON_APP_ID");
            if (string.IsNullOrEmpty(appId))
                throw new Exception("PHOTON_APP_ID environment variable is missing.");

            const string photonFolder = "Assets/Photon";
            const string punFolder = photonFolder + "/PhotonUnityNetworking";
            const string folder = punFolder + "/Resources";
            const string assetPath = folder + "/PhotonServerSettings.asset";
            if (!AssetDatabase.IsValidFolder(photonFolder))
                AssetDatabase.CreateFolder("Assets", "Photon");
            if (!AssetDatabase.IsValidFolder(punFolder))
                AssetDatabase.CreateFolder(photonFolder, "PhotonUnityNetworking");
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(punFolder, "Resources");

            ServerSettings settings = AssetDatabase.LoadAssetAtPath<ServerSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<ServerSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            if (settings.AppSettings == null)
                settings.AppSettings = new AppSettings();
            settings.AppSettings.AppIdRealtime = appId.Trim();
            settings.AppSettings.UseNameServer = true;
            settings.AppSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.WebSocketSecure;
            settings.PunLogging = PunLogLevel.Full;
            settings.EnableSupportLogger = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PumpkinPanicCoop] PhotonServerSettings created/configured for PUN WebGL build with full PUN logging and SupportLogger enabled.");
        }
    }
}
