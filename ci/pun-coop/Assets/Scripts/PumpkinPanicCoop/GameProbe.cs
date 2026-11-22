using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;

namespace PumpkinPanicCoop
{
internal static class GameProbe
{
	internal static void LogSceneAndTargets(CoopLog log)
	{
		Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "Assembly-CSharp");
		if (assembly == null)
		{
			log.LogWarning((object)"Assembly-CSharp not loaded yet.");
			return;
		}
		string[] array = new string[5] { "playerController", "moneyCount", "GameManager2", "plantCrop", "enemyController" };
		foreach (string text in array)
		{
			Type type = assembly.GetType(text, throwOnError: false);
			if (type == null)
			{
				log.LogWarning((object)("Game type not found: " + text));
				continue;
			}
			string text2 = string.Join(", ", Array.ConvertAll(type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), (MethodInfo m) => m.Name));
			log.LogInfo((object)("Game type " + text + ": " + text2));
		}
		MonoBehaviour[] array2 = Object.FindObjectsOfType<MonoBehaviour>();
		foreach (MonoBehaviour val in array2)
		{
			if (!((Object)(object)val == (Object)null))
			{
				string text3 = ((object)val).GetType().FullName ?? ((object)val).GetType().Name;
				switch (text3)
				{
				case "playerController":
				case "enemyController":
				case "moneyCount":
					log.LogInfo((object)("Active gameplay component " + text3 + " on '" + ((Object)((Component)val).gameObject).name + "'"));
					break;
				}
			}
		}
	}
}
}
