using HarmonyLib;
using Zenject;
using UnityEngine;

[HarmonyPatch(typeof(StandardGameplayInstaller), nameof(StandardGameplayInstaller.InstallBindings))]
internal static class DebugGameplayInstaller
{
    [HarmonyPrefix]
    private static void Prefix(StandardGameplayInstaller __instance)
    {
        // Use reflection to look at the private setup data fields in the installer
        var fields = typeof(StandardGameplayInstaller).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            var value = field.GetValue(__instance);
            if (value == null)
            {
                Debug.LogError($"[NoodleDebug] CRITICAL: Field {field.Name} is NULL in StandardGameplayInstaller!");
            }
        }
    }
}