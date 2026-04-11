using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace TRStats
{
    internal static class PatchTargetFinder
    {
        private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
        private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

        static PatchTargetFinder()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                OpCode op = (OpCode)field.GetValue(null);
                ushort value = (ushort)op.Value;
                if (value < 0x100) OneByteOpCodes[value] = op;
                else if ((value & 0xff00) == 0xfe00) TwoByteOpCodes[value & 0xff] = op;
            }
        }

        public static IEnumerable<MethodBase> FindWellWaterMethods()
        {
            foreach (MethodInfo method in typeof(Well).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.ReturnType != typeof(bool)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(int)) continue;

                if (MethodReferences(method, "CommonReferences", "bucketItem") &&
                    MethodReferences(method, "CommonReferences", "bucketOfWaterItem"))
                {
                    yield return method;
                }
            }
        }

        public static IEnumerable<MethodBase> FindCrafterReturnBucketMethods()
        {
            foreach (MethodInfo method in typeof(Crafter).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.ReturnType != typeof(void)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2) continue;
                if (parameters[0].ParameterType != typeof(int) || parameters[1].ParameterType != typeof(ItemAmount)) continue;

                if (MethodReferences(method, "PlayerInventory", "AddItems"))
                {
                    yield return method;
                }
            }
        }

        private static bool MethodReferences(MethodInfo method, string declaringTypeName, string memberName)
        {
            MethodBody body = method.GetMethodBody();
            if (body == null) return false;

            byte[] il = body.GetILAsByteArray();
            Module module = method.Module;
            int index = 0;

            while (index < il.Length)
            {
                OpCode op;
                byte value = il[index++];
                if (value == 0xfe) op = TwoByteOpCodes[il[index++]];
                else op = OneByteOpCodes[value];

                int operandStart = index;
                int operandSize = GetOperandSize(op.OperandType, il, operandStart);

                if (op.OperandType == OperandType.InlineField ||
                    op.OperandType == OperandType.InlineMethod ||
                    op.OperandType == OperandType.InlineTok)
                {
                    MemberInfo member = null;
                    try {
                        int token = BitConverter.ToInt32(il, operandStart);
                        member = op.OperandType == OperandType.InlineField
                            ? (MemberInfo)module.ResolveField(token)
                            : module.ResolveMember(token);
                    } catch {}

                    if (member != null &&
                        member.DeclaringType != null &&
                        member.DeclaringType.Name == declaringTypeName &&
                        member.Name == memberName)
                    {
                        return true;
                    }
                }

                index += operandSize;
            }

            return false;
        }

        private static int GetOperandSize(OperandType operandType, byte[] il, int index)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + (BitConverter.ToInt32(il, index) * 4);
                default:
                    return 0;
            }
        }
    }

    /// <summary>
    /// Harmony patches for modifying game stats and modifiers
    /// </summary>
    public static class Patches
    {
        // Additional config entries for patches
        public static ConfigEntry<float> EmployeeWorkAvoidanceMultiplier;
        public static ConfigEntry<float> CustomerPatienceMultiplier;

        public static void InitializeConfig(ConfigFile config)
        {
            EmployeeWorkAvoidanceMultiplier = config.Bind("Employees", "WorkAvoidanceMultiplier", 1.0f,
                new ConfigDescription("Employee work avoidance time multiplier (lower = less slacking)",
                    new AcceptableValueRange<float>(0.1f, 3.0f)));

            CustomerPatienceMultiplier = config.Bind("Customers", "PatienceMultiplier", 1.0f,
                new ConfigDescription("Customer patience multiplier (higher = more patient)",
                    new AcceptableValueRange<float>(0.5f, 5.0f)));
        }

        /// <summary>
        /// Patch tavern reputation perks bonus calculation
        /// </summary>
        [HarmonyPatch(typeof(TavernReputation))]
        public static class TavernReputationPatches
        {
            [HarmonyPatch("GetPerksBonus")]
            [HarmonyPostfix]
            public static void GetPerksBonus_Postfix(ref int __result)
            {
                // Add our extra customer capacity to the perks bonus
                if (Plugin.ExtraCustomerCapacity != null)
                {
                    __result += Plugin.ExtraCustomerCapacity.Value;
                }
            }
        }

        /// <summary>
        /// Patch money/price calculations
        /// </summary>
        [HarmonyPatch(typeof(Money))]
        public static class MoneyPatches
        {
            [HarmonyPatch("GetPlusPricesSatisfaction")]
            [HarmonyPostfix]
            public static void GetPlusPricesSatisfaction_Postfix(ref float __result)
            {
                // Add our price modifier to satisfaction-based price bonus
                if (Plugin.PriceModifier != null)
                {
                    __result += Plugin.PriceModifier.Value;
                }
            }
        }

        /// <summary>
        /// Patch employee work avoidance behavior
        /// </summary>
        [HarmonyPatch(typeof(Employee))]
        public static class EmployeePatches
        {
            // Patch employee initialization to modify work avoidance times
            [HarmonyPatch("Start")]
            [HarmonyPostfix]
            public static void Start_Postfix(Employee __instance)
            {
                if (EmployeeWorkAvoidanceMultiplier != null)
                {
                    float mult = EmployeeWorkAvoidanceMultiplier.Value;
                    __instance.avoidWorkMinTime *= mult;
                    __instance.avoidWorkMaxTime *= mult;
                    __instance.avoidWorkRateMin *= mult;
                    __instance.avoidWorkRateMax *= mult;
                }
            }
        }

        /// <summary>
        /// Patch Fireplace to prevent fuel consumption when infinite coal is enabled
        /// </summary>
        [HarmonyPatch(typeof(Fireplace))]
        public static class FireplacePatches
        {
            // Store the fuel value before Update runs
            private static float _savedFuel;

            [HarmonyPatch("Update")]
            [HarmonyPrefix]
            public static void Update_Prefix(Fireplace __instance)
            {
                if (Plugin.InfiniteCoal != null && Plugin.InfiniteCoal.Value)
                {
                    _savedFuel = __instance.currentFuel;
                }
            }

            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            public static void Update_Postfix(Fireplace __instance)
            {
                if (Plugin.InfiniteCoal != null && Plugin.InfiniteCoal.Value)
                {
                    // Restore fuel to what it was before Update
                    if (__instance.currentFuel < _savedFuel)
                    {
                        __instance.currentFuel = _savedFuel;
                    }
                }
            }
        }

        /// <summary>
        /// Patch Crafter (brewing tanks, etc.) to prevent fuel consumption when infinite coal is enabled
        /// </summary>
        [HarmonyPatch(typeof(Crafter))]
        public static class CrafterPatches
        {
            [HarmonyPatch("SetFuel")]
            [HarmonyPrefix]
            public static bool SetFuel_Prefix(Crafter __instance, ref int __0)
            {
                if (Plugin.InfiniteCoal != null && Plugin.InfiniteCoal.Value)
                {
                    // Get current fuel via property
                    int currentFuel = __instance.AHCDANNFGPG;

                    // If new fuel would be less than current (consumption), prevent it
                    if (__0 < currentFuel)
                    {
                        __0 = currentFuel;
                    }
                }
                return true; // Continue with original method
            }
        }

        /// <summary>
        /// Patch Well to prevent water bucket consumption when infinite water is enabled
        /// </summary>
        [HarmonyPatch(typeof(Well))]
        public static class WellPatches
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return PatchTargetFinder.FindWellWaterMethods();
            }

            [HarmonyPrefix]
            public static bool EmptyBuckets_Prefix(ref bool __result)
            {
                if (Plugin.InfiniteWater != null && Plugin.InfiniteWater.Value)
                {
                    // Skip emptying buckets, just return true to indicate success
                    __result = true;
                    return false; // Skip original method
                }
                return true; // Run original method
            }
        }

        /// <summary>
        /// Patch Crafter to return water buckets instead of empty buckets when infinite water is enabled
        /// AHGOCDAALHF is called when returning empty buckets to player after using water in crafting
        /// </summary>
        [HarmonyPatch(typeof(Crafter))]
        public static class CrafterWaterPatches
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return PatchTargetFinder.FindCrafterReturnBucketMethods();
            }

            [HarmonyPrefix]
            public static void ReturnBucket_Prefix(ref ItemAmount __1)
            {
                if (Plugin.InfiniteWater != null && Plugin.InfiniteWater.Value)
                {
                    // Check if the item being returned is an empty bucket
                    if (CommonReferences.GOKBJFAMHMJ != null &&
                        CommonReferences.GOKBJFAMHMJ.bucketItem != null &&
                        CommonReferences.GOKBJFAMHMJ.bucketOfWaterItem != null)
                    {
                        // Compare items - if it's an empty bucket, swap it for water bucket
                        if (__1.item != null &&
                            string.Equals(__1.item.nameId, CommonReferences.GOKBJFAMHMJ.bucketItem.nameId))
                        {
                            __1.item = CommonReferences.GOKBJFAMHMJ.bucketOfWaterItem;
                        }
                    }
                }
            }
        }
    }
}
