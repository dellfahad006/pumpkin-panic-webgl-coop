using System;
using System.IO;
using Photon.Realtime;
using Photon.Pun;
using Photon.Pun;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace PumpkinPanicCoop.Editor
{
    public static class GeneratePhotonServerSettings
    {
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
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PumpkinPanicCoop] PhotonServerSettings created/configured for PUN WebGL build.");
        }
    }
}
