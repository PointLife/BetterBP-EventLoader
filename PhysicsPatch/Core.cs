using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BetterBPApiLoader
{
	public class Core : Plugin
	{
		public static Core Instance { get; internal set; }

		public bool InvalidState { get; set; } = false;

		public bool Dont_Save_On_Error { get; set; } = false;

		public Core()
		{
			Instance = this;
			Info = new PluginInfo("BetterBPPhyiscs", "betterbp.patch.physics")
			{
				Description = "Better BP Physics\nby PointLife Dev Team"
			};

			Debug.Log("Loaded Plugin " + Info.Name);
			var harmony = new Harmony("com.pointlife.betterbp.patch.physics");

			var config_path = "./PluginsConfig/BetterBP/Physics/";
			Directory.CreateDirectory(config_path);

			harmony.PatchAll();
		}



		[HarmonyPatch(typeof(ShEntity))]
		[HarmonyPatch(nameof(ShEntity.IgnorePhysics))]
		public static class Patch003
		{
			public static bool Prefix(Collider collider, ShEntity __instance, ref bool __result)
			{
				__result = collider.gameObject.layer == 16;
				return false;
			}
		}

	}
}