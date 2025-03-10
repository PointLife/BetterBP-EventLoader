﻿using BrokeProtocol.API;
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
			Info = new PluginInfo("BetterBPApiLoader", "betterbp.api.loader")
			{
				Description = "Better Event Loader\nby PointLife Dev Team"
			};

			Debug.Log("Loaded Plugin " + Info.Name);
			var harmony = new Harmony("com.pointlife.betterbp.api.loader");

			var config_path = "./PluginsConfig/BetterBP/ApiLoader/";
			Directory.CreateDirectory(config_path);

			var config_path_dont_save_on_error = Path.Combine(config_path, "dont_save_on_error");

			if (!File.Exists(config_path_dont_save_on_error))
			{
				File.WriteAllText(config_path_dont_save_on_error, "0");
			}
			var dont_save_on_error_data = File.ReadAllText(config_path_dont_save_on_error);
			if (dont_save_on_error_data.StartsWith("1"))
			{
				Dont_Save_On_Error = true;
				Debug.LogError("Not Saving when Error is cought enabled!");
			}
			else
			{
				Debug.LogWarning("Not Saving when Error is cought disabled!");
			}

			var type = typeof(PluginData);
			var constructor = AccessTools.Constructor(type, new Type[] { typeof(Plugin), typeof(List<IScript>) });

			var postfix = typeof(PatchPluginDataConstructor).GetMethod(nameof(PatchPluginDataConstructor.Postfix));

			harmony.Patch(constructor, postfix: new HarmonyMethod(postfix));


			harmony.PatchAll();
		}

		[HarmonyPatch(typeof(SvPlayer))]
		[HarmonyPatch(nameof(SvPlayer.Save))]
		public static class Patch
		{
			public static bool Prefix()
			{
				if (Core.Instance.Dont_Save_On_Error)
				{
					if (Core.Instance.InvalidState)
					{
						InterfaceHandler.SendTextToAll("<color=red>NOT SAVING SERVER DATA DUE TO ERROR</color>", 300);
						return false;

					}


				}
				return true;

			}
		}

		[HarmonyPatch(typeof(SvManager))]
		[HarmonyPatch(nameof(SvManager.SaveAll))]
		public static class PatchSaveAll
		{
			public static bool Prefix()
			{
				if (Core.Instance.Dont_Save_On_Error)
				{
					if (Core.Instance.InvalidState)
					{
						InterfaceHandler.SendTextToAll("<color=red>NOT SAVING SERVER DATA DUE TO ERROR</color>", 300);
						return false;
					}
				}
				return true;
			}
		}

		public static Plugin CurrentPluginData;

		public static class PatchPluginDataConstructor
		{
			public static void Postfix(Plugin plugin, List<IScript> scripts)
			{
				Debug.Log("Current Plugin Data " + plugin.Info.Name);
				CurrentPluginData = plugin;
			}
		}



		public static Dictionary<DelegateContainer, Dictionary<Delegate, string>> DelegatePlugin = new();

		[HarmonyPatch(typeof(DelegateContainer))]
		[HarmonyPatch(nameof(DelegateContainer.Add))]
		class SourceHandler_Add_Patch
		{
			static bool Prefix(DelegateContainer __instance, Delegate method, ExecutionMode executionMode)
			{
				if (!DelegatePlugin.ContainsKey(__instance))
				{
					DelegatePlugin.Add(__instance, new Dictionary<Delegate, string>());
				}
				DelegatePlugin[__instance].Add(method, CurrentPluginData.Info.Name);

				switch (executionMode)
				{
					case ExecutionMode.PreEvent:
						__instance.preDelegates.Add(method);
						break;
					case ExecutionMode.PostEvent:
						__instance.postDelegates.Add(method);
						break;
					case ExecutionMode.Override:
						for (var i = __instance.delegates.Count - 1; i >= 0; i--)
						{
							if (__instance.delegates[i].Item2 != ExecutionMode.Event)
								__instance.delegates.RemoveAt(i);
						}
						__instance.delegates.Add((method, executionMode));
						break;
					default: // Additive and Event
						__instance.delegates.Add((method, executionMode));
						break;
				}

				return false;
			}
		}

		public static string FindConstantName<T>(Type containingType, T value)
		{
			EqualityComparer<T> @default = EqualityComparer<T>.Default;
			foreach (FieldInfo fieldInfo in containingType.GetFields(BindingFlags.Static | BindingFlags.Public))
			{
				bool flag = fieldInfo.FieldType == typeof(T) && @default.Equals(value, (T)((object)fieldInfo.GetValue(null)));
				if (flag)
				{
					return fieldInfo.Name;
				}
			}
			return null;
		}

		[HarmonyPatch(typeof(SourceHandler))]
		[HarmonyPatch("Add")]
		public static class DebugEvents
		{
			public static void Prefix(int eventID, Delegate method, ExecutionMode executionMode)
			{
				Debug.Log(string.Format("Registering Event {0} : {1} : {2} with Methode {3} with ParCount {4}!", new object[]
				{
					eventID,
					Core.FindConstantName<int>(typeof(GameSourceEvent), eventID),
					executionMode,
					method.Method.Name,
					method.Method.GetParameters().Count<ParameterInfo>()
				}));
			}
		}

		[HarmonyPatch(typeof(EventsHandler))]
		[HarmonyPatch(nameof(EventsHandler.Exec))]
		class EventsHandler_Exec_Patch
		{
			static bool Prefix(string eventName, params object[] args)
			{
				Delegate @delegate;

				if (EventsHandler.RegisteredEvents.TryGetValue(eventName, out @delegate))
				{
					try
					{

						@delegate.Method.Invoke(@delegate.Target, args);
					}
					catch (Exception ex)
					{
						LogError(@delegate, ex, eventName, args);
					}
				}
				return false;
			}

			public static void LogError(Delegate del, Exception exception, string eventName, params object[] args)
			{
				Core.Instance.InvalidState = true;

				var sb = new StringBuilder();

				sb.AppendLine($"========================== Advanced Event Error Tracker ==========================");
				sb.AppendLine();
				sb.AppendLine($"PluginName: {"NOT YET SUPPORTED FOR CUSTOMEVENTS"}");
				sb.AppendLine($"Methode Name: {del.Method.Name}");
				sb.AppendLine($"ExecutionMode: {"CUSTOMEVENT"}");
				sb.AppendLine($"EventID: {eventName}");
				sb.AppendLine($"Event Name: {eventName}");
				sb.AppendLine();
				sb.AppendLine($"Full Signature: {del.Method.FullDescription()}");
				sb.AppendLine($"Assembly FullName: {del.Method.DeclaringType.Assembly.FullName}");
				sb.AppendLine($"Assembly Location: {del.Method.DeclaringType.Assembly.Location}");
				sb.AppendLine($"InvocationList: {del.GetInvocationList().Length}");
				sb.AppendLine();
				sb.AppendLine($"Methode Args: {del.Method.GetParameters().Join(x => x.Name.ToString())}");
				sb.AppendLine($"Type Methode: {del.Method.GetParameters().Join(x => x.ParameterType.ToString())}");
				sb.AppendLine($"Type Event  : {args.Join(x => x.GetType().ToString())}");
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendLine("Exception:");
				sb.AppendLine(exception.ToString());
				sb.AppendLine(exception.Message);
				sb.AppendLine(exception.StackTrace);
				sb.AppendLine();
				//sb.AppendLine($"===================================================================================");

				sb.AppendLine($"==========================     by PointLife Dev Team     ==========================");

				Debug.LogError(sb.ToString());
			}
		}


		[HarmonyPatch(typeof(DelegateContainer))]
		[HarmonyPatch(nameof(DelegateContainer.Execute))]
		class SourceHandler_Exec_Patch
		{
			static bool Prefix(ref bool __result, DelegateContainer __instance, params object[] args)
			{
				foreach (var d in __instance.preDelegates)
				{


					try
					{
						if (d.Method.Invoke(d.Target, args) is bool value && !value)
						{
							__result = false;
							return false;
						}
					}
					catch (Exception ex)
					{
						LogError(__instance, d, ex, args);
					}
				}

				foreach (var d in __instance.delegates)
				{
					//LogError(__instance, d.Item1, new Exception(), args);

					try
					{
						if (d.Item1.Method.Invoke(d.Item1.Target, args) is bool value && !value)
						{
							__result = false;
							return false;
						}
					}
					catch (Exception ex)
					{
						LogError(__instance, d.Item1, ex, args);
					}
				}

				foreach (var d in __instance.postDelegates)
				{
					try
					{
						if (d.Method.Invoke(d.Target, args) is bool value && !value)
						{
							__result = false;
							return false;
						}
					}
					catch (Exception ex)
					{
						LogError(__instance, d, ex, args);
					}
				}
				__result = true;
				return false;
			}


			public static void LogError(DelegateContainer __instance, Delegate del, Exception exception, params object[] args)
			{
				Core.Instance.InvalidState = true;

				var sb = new StringBuilder();

				sb.AppendLine($"========================== Advanced Event Error Tracker ==========================");
				sb.AppendLine();
				sb.AppendLine($"Timestamp {System.DateTime.UtcNow}");

				sb.AppendLine();

				try
				{
					sb.AppendLine($"PluginName: {DelegatePlugin[__instance][del]}");
				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"PluginName: Error");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();

				}

				sb.AppendLine($"Methode Name: {del.Method.Name}");
				try
				{
					sb.AppendLine($"ExecutionMode: {del.Method.GetCustomAttribute<TargetAttribute>().ExecutionMode}");
					sb.AppendLine($"EventID: {del.Method.GetCustomAttribute<TargetAttribute>().EventID}");
					sb.AppendLine($"Event Name: {FindConstantName(typeof(GameSourceEvent), del.Method.GetCustomAttribute<TargetAttribute>().EventID)}");
				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"ExecutionMode: Error\nEventID: Error\nEvent Name: Error");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();


				}
				sb.AppendLine();
				try
				{

					sb.AppendLine($"Full Signature: {del.Method.FullDescription()}");
					sb.AppendLine($"Assembly FullName: {del.Method.DeclaringType.Assembly.FullName}");
					sb.AppendLine($"Assembly Location: {del.Method.DeclaringType.Assembly.Location}");
					sb.AppendLine($"InvocationList: {del.GetInvocationList().Length}");
				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"Full Signature: Error\nAssembly FullName: Error\nAssembly Location: Error\nInvocationList: Error");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();


				}

				sb.AppendLine();
				try
				{
					sb.AppendLine($"Methode Args: {del.Method.GetParameters().Join(x => x.Name.ToString())}");
				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"Methode Args: Error\nAssembly FullName: Error\nAssembly Location: Error\nInvocationList: Error");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();


				}
				try
				{
					sb.AppendLine($"Type Methode: {del.Method.GetParameters().Join(x => x.ParameterType.ToString())}");
					sb.AppendLine($"Type Event  : {args.Join(x => x.GetType().ToString())}");

				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"Type Methode: Error\nType Event: Error\n");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();


				}
				sb.AppendLine();

				try
				{

					sb.AppendLine("Advanced Argument Report:");

					int count = 0;
					foreach (var arg in args)
					{
						sb.Append($"Arg {count++}: ");
						sb.Append($" {arg.GetType().Name}: {arg.ToString()}: ");

						if (arg is ShPlayer player)
						{
							sb.Append($"Player: {player.username} {player.Position} {player.Place.Index}");
						}
						if (arg is ShApartment apartment)
						{
							sb.Append($"Apartment: {apartment.GetPlaceIndex}");
						}
						if (arg is Place place)
						{
							sb.Append($"Place: {place.GetIndex}");
						}
						

						sb.AppendLine();

					}
				}
				catch (Exception e)
				{
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine($"Advanced Argument Report: Error");

					sb.AppendLine(e.ToString());
					sb.AppendLine(e.Message);
					sb.AppendLine(e.StackTrace);
					sb.AppendLine("=?=?=?=?=?=");

					sb.AppendLine();
					sb.AppendLine();


				}
				sb.AppendLine();
				sb.AppendLine();
				sb.AppendLine("Event Exception:");
				sb.AppendLine(exception.ToString());
				sb.AppendLine(exception.Message);
				sb.AppendLine(exception.StackTrace);
				sb.AppendLine();
				//sb.AppendLine($"===================================================================================");

				sb.AppendLine($"==========================     by PointLife Dev Team     ==========================");

				Debug.LogError(sb.ToString());
			}


		}

	}
}