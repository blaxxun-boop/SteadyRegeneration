using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;

namespace SteadyRegen;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class SteadyRegen : BaseUnityPlugin
{
	private const string ModName = "Steady Regeneration";
	private const string ModVersion = "1.0.1";
	private const string ModGUID = "org.bepinex.plugins.steadyregeneration";

	public void Awake()
	{
		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
	private static class MakeRegenSmooth
	{
		private static readonly MethodInfo Heal = AccessTools.DeclaredMethod(typeof(Character), nameof(Player.Heal));
		private static readonly FieldInfo RegenTimer = AccessTools.DeclaredField(typeof(Player), nameof(Player.m_foodRegenTimer));

		[UsedImplicitly]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> instructionList = instructions.ToList();
			for (int i = 0; i < instructionList.Count; ++i)
			{
				if (instructionList[i].opcode == OpCodes.Ldc_R4 && instructionList[i].OperandIs(10f))
				{
					yield return new CodeInstruction(OpCodes.Ldc_R4, 0.1f);
				}
				else if (instructionList[i].opcode == OpCodes.Ldc_R4 && instructionList[i].OperandIs(0f) && instructionList[i + 1].opcode == OpCodes.Stfld && instructionList[i + 1].OperandIs(RegenTimer))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldfld, RegenTimer);
					yield return new CodeInstruction(OpCodes.Ldc_R4, 0.1f);
					yield return new CodeInstruction(OpCodes.Sub);
				}
				else if (instructionList[i].opcode == OpCodes.Ldc_I4_1 && instructionList[i + 1].opcode == OpCodes.Call && instructionList[i + 1].OperandIs(Heal))
				{
					yield return new CodeInstruction(OpCodes.Ldc_R4, 0.01f);
					yield return new CodeInstruction(OpCodes.Mul);
					yield return new CodeInstruction(OpCodes.Ldc_I4_0);
				}
				else
				{
					yield return instructionList[i];
				}
			}
		}
	}

	[HarmonyPatch(typeof(GuiBar), nameof(GuiBar.SetValue))]
	private static class DisplaySmoothRegenOnHealthbar
	{
		private static readonly FieldInfo BarValue = AccessTools.DeclaredField(typeof(GuiBar), nameof(GuiBar.m_value));
		private static readonly FieldInfo SmoothFill = AccessTools.DeclaredField(typeof(GuiBar), nameof(GuiBar.m_smoothFill));

		[UsedImplicitly]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> instructionList = instructions.ToList();
			for (int i = 0; i < instructionList.Count; ++i)
			{
				if (instructionList[i].opcode == OpCodes.Ldfld && instructionList[i].OperandIs(SmoothFill))
				{
					for (; i > 0; --i)
					{
						if (instructionList[i].opcode == OpCodes.Ldfld && instructionList[i].OperandIs(BarValue))
						{
							instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Add));
							instructionList.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_R4, 0.5f));
							break;
						}
					}
					break;
				}
			}
			return instructionList;
		}
	}

	[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool))]
	private static class ChangeFoodTooltip
	{
		[UsedImplicitly]
		private static void Postfix(ItemDrop.ItemData __instance, ref string __result)
		{
			__result = __result.Replace("hp/tick", "hp/10s");
		}
	}
}
