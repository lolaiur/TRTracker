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

        // Every method on a fuel host that stores to its fuel field with the (int amount, bool sync)
        // shape. The obfuscator emits renamed copies of SetFuel and the game calls several of them
        // (the book stand's imbue session spends fuel through SetFuel and a clone), so patching
        // SetFuel by name alone lets part of the spending through.
        public static IEnumerable<MethodBase> FindFuelWriters(Type hostType)
        {
            List<MethodBase> found = new List<MethodBase>();
            foreach (MethodInfo method in hostType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.ReturnType != typeof(void)) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2) continue;
                if (parameters[0].ParameterType != typeof(int) || parameters[1].ParameterType != typeof(bool)) continue;

                // Require a store, not just a read: the prefix rewrites the first int argument, so a
                // method that only reads fuel would have an unrelated argument changed.
                if (MethodStoresField(method, hostType.Name, "fuel")) found.Add(method);
            }

            // PatchAll throws on a patch class with no targets, which also skips every patch class
            // after it. SetFuel implements IFuelHost, so its name is stable enough to fall back on.
            if (found.Count == 0)
            {
                MethodInfo setFuel = hostType.GetMethod("SetFuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setFuel != null) found.Add(setFuel);
            }
            return found;
        }

        private static bool MethodStoresField(MethodInfo method, string declaringTypeName, string fieldName)
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

                if (op == OpCodes.Stfld)
                {
                    FieldInfo field = null;
                    try { field = module.ResolveField(BitConverter.ToInt32(il, operandStart)); } catch {}
                    if (field != null &&
                        field.DeclaringType != null &&
                        field.DeclaringType.Name == declaringTypeName &&
                        field.Name == fieldName)
                    {
                        return true;
                    }
                }

                index += operandSize;
            }

            return false;
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

        private static float _nextWaterLogTime;
        internal static void LogWater(string message)
        {
            if (Time.unscaledTime < _nextWaterLogTime) return;
            _nextWaterLogTime = Time.unscaledTime + 5f;
            try { File.AppendAllText(Plugin.LogPath, "[Water] " + message + "\n"); } catch { }
        }

        // One-time diagnostic: count how many methods each dynamic water-patch finder resolves,
        // so the log shows whether the patches actually attached to anything after a game update.
        public static void LogPatchDiagnostics()
        {
            try
            {
                int well = 0;
                foreach (MethodBase m in PatchTargetFinder.FindWellWaterMethods()) well++;
                int crafter = 0;
                foreach (MethodBase m in PatchTargetFinder.FindCrafterReturnBucketMethods()) crafter++;
                File.AppendAllText(Plugin.LogPath, "[Water] Patch targets found: well=" + well + " crafter=" + crafter + "\n");
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Plugin.LogPath, "[Water] diag error: " + ex.Message + "\n"); } catch { }
            }
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

        // The fuel field on each host, resolved once. Harmony's ___fuel injection would be shorter,
        // but a renamed field would then throw inside PatchAll and take every other patch with it;
        // a null FieldInfo here just leaves that one cheat inactive.
        internal static class FuelHosts
        {
            internal static readonly FieldInfo CrafterFuel =
                typeof(Crafter).GetField("fuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            internal static readonly FieldInfo BookStandFuel =
                typeof(MagicBookStand).GetField("fuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Keep fuel from going down: a lower value means the game is spending it. Adding fuel
            // raises the value and passes through untouched.
            internal static void BlockSpend(FieldInfo fuelField, object host, ref int newFuel)
            {
                if (fuelField == null || host == null) return;
                try {
                    int current = (int)fuelField.GetValue(host);
                    if (newFuel < current) newFuel = current;
                } catch {}
            }
        }

        /// <summary>
        /// Crafters keep their fuel while the matching cheat is on. Arcane crafters (Arcane Oven,
        /// Arcane Distillery) burn magic fuel and follow Infinite Magic Fuel; the rest burn coal.
        /// </summary>
        [HarmonyPatch(typeof(Crafter))]
        public static class CrafterPatches
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return PatchTargetFinder.FindFuelWriters(typeof(Crafter));
            }

            [HarmonyPrefix]
            public static void FuelWrite_Prefix(Crafter __instance, ref int __0)
            {
                if (__instance == null) return;
                ConfigEntry<bool> cheat = __instance.arcaneCrafter ? Plugin.InfiniteMagicFuel : Plugin.InfiniteCoal;
                if (cheat == null || !cheat.Value) return;
                FuelHosts.BlockSpend(FuelHosts.CrafterFuel, __instance, ref __0);
            }
        }

        /// <summary>
        /// The Arcane Book Stand spends magic fuel when imbuing spells. It keeps its fuel while
        /// Infinite Magic Fuel is on.
        /// </summary>
        [HarmonyPatch(typeof(MagicBookStand))]
        public static class MagicBookStandPatches
        {
            [HarmonyTargetMethods]
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return PatchTargetFinder.FindFuelWriters(typeof(MagicBookStand));
            }

            [HarmonyPrefix]
            public static void FuelWrite_Prefix(MagicBookStand __instance, ref int __0)
            {
                if (Plugin.InfiniteMagicFuel == null || !Plugin.InfiniteMagicFuel.Value) return;
                FuelHosts.BlockSpend(FuelHosts.BookStandFuel, __instance, ref __0);
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
                    LogWater("Well empty-bucket patch fired (infinite water).");
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
                    CommonReferences commonReferences = TRStatsReflection.FindSingleton<CommonReferences>();
                    if (commonReferences != null &&
                        commonReferences.bucketItem != null &&
                        commonReferences.bucketOfWaterItem != null)
                    {
                        // Compare items - if it's an empty bucket, swap it for water bucket
                        if (__1.item != null &&
                            string.Equals(__1.item.nameId, commonReferences.bucketItem.nameId))
                        {
                            __1.item = commonReferences.bucketOfWaterItem;
                            LogWater("Crafter bucket->water swap fired (infinite water).");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Patch AnimalFeeder.CanFillWithWater (used by pet/cat/dog water bowls and hen houses) so a
        /// water bucket is not consumed when infinite water is enabled. The caller fills the feeder
        /// when this returns true, so we just return true without running the original consume logic.
        /// </summary>
        [HarmonyPatch(typeof(AnimalFeeder), "CanFillWithWater")]
        public static class AnimalFeederWaterPatches
        {
            [HarmonyPrefix]
            public static bool CanFillWithWater_Prefix(ref bool __result)
            {
                if (Plugin.InfiniteWater != null && Plugin.InfiniteWater.Value)
                {
                    // Skip the original: it removes a water bucket and gives an empty one. With
                    // infinite water, let the fill proceed and keep the bucket.
                    __result = true;
                    LogWater("AnimalFeeder water fill: bucket kept (infinite water).");
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Work around a hard crash in the updated game while prewarming mine pieces during Gameplay scene load.
        /// The pool can still create pieces lazily later; this only skips the up-front clone loop.
        /// </summary>
        [HarmonyPatch(typeof(MinePiecePool), "Awake")]
        public static class MinePiecePoolPatches
        {
            private static readonly FieldInfo PoolDictionaryField = FindPoolDictionaryField();
            private static readonly FieldInfo MinePieceIdField = FindMinePieceIdField();

            [HarmonyPrefix]
            public static bool Awake_Prefix(MinePiecePool __instance)
            {
                if (Plugin.SkipMinePiecePoolPrewarm == null || !Plugin.SkipMinePiecePoolPrewarm.Value) return true;
                if (__instance == null) return true;

                try
                {
                    MinePiecePool._instance = __instance;
                    SeedPoolDictionary(__instance);
                    File.AppendAllText(Plugin.LogPath, "[Compat] Skipped MinePiecePool prewarm during save load.\n");
                    return false;
                }
                catch (Exception ex)
                {
                    try { File.AppendAllText(Plugin.LogPath, "[Compat] MinePiecePool prewarm skip failed: " + ex + "\n"); } catch { }
                    return true;
                }
            }

            private static void SeedPoolDictionary(MinePiecePool pool)
            {
                if (PoolDictionaryField == null || pool.poolPieces == null) return;

                Dictionary<int, Queue<MinePiece>> dictionary = PoolDictionaryField.GetValue(pool) as Dictionary<int, Queue<MinePiece>>;
                if (dictionary == null)
                {
                    dictionary = new Dictionary<int, Queue<MinePiece>>();
                    PoolDictionaryField.SetValue(pool, dictionary);
                }

                foreach (MinePiece piece in pool.poolPieces)
                {
                    if (piece == null) continue;
                    int key = GetMinePieceId(piece);
                    if (!dictionary.ContainsKey(key)) dictionary[key] = new Queue<MinePiece>();
                }
            }

            private static FieldInfo FindPoolDictionaryField()
            {
                foreach (FieldInfo field in typeof(MinePiecePool).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(Dictionary<int, Queue<MinePiece>>)) return field;
                }

                return null;
            }

            private static FieldInfo FindMinePieceIdField()
            {
                // _minePieceID is [SerializeField], so its name stays stable through obfuscation passes.
                return typeof(MinePiece).GetField("_minePieceID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            private static int GetMinePieceId(MinePiece piece)
            {
                if (piece == null) return -1;
                if (MinePieceIdField != null)
                {
                    object val = MinePieceIdField.GetValue(piece);
                    if (val is int) return (int)val;
                }
                return 0;
            }
        }
    }
}
