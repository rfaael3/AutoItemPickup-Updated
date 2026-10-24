#if RISKOFOPTIONS
using System;
using System.IO;
using System.Runtime.CompilerServices;
using AutoItemPickup.Configuration;
using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace AutoItemPickup;

/// <summary>
///     Exposes the mod's config in the in-game Risk Of Options menu.
///     Only compiled when RiskOfOptions.dll is found at build time, and only called when the
///     Risk Of Options plugin is actually loaded.
///     Changes are written to the .cfg file and the mod reads the values live, so they apply immediately.
///     NOTE: this mod runs on the host only, so only the host's settings matter.
/// </summary>
internal static class RiskOfOptionsCompat
{
    public const string Guid = "com.rune580.riskofoptions";

    // Risk Of Options can only draw simple types (bool, enum, string...). The tier/item lists are custom types,
    // so each one gets a text "proxy" entry that lives in a throwaway in-memory ConfigFile (never saved to disk)
    // and is kept in sync with the real entry in both directions.
    private static ConfigFile proxyFile;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register(Config config)
    {
        ModSettingsManager.SetModDescription(
            "Automatically distributes items among players. Only the host's settings matter.");

        // General
        ModSettingsManager.AddOption(new ChoiceOption(config.distributionMode));
        ModSettingsManager.AddOption(new CheckBoxOption(config.distributeToDeadPlayers));
        ModSettingsManager.AddOption(new CheckBoxOption(config.printerOverrideTarget));
        ModSettingsManager.AddOption(new CheckBoxOption(config.scrapperOverrideTarget));

        // Items
        ModSettingsManager.AddOption(new CheckBoxOption(config.distributeOnDrop));
        ModSettingsManager.AddOption(new CheckBoxOption(config.distributeOnTeleport));

        // Command
        ModSettingsManager.AddOption(new CheckBoxOption(config.teleportCommandOnDrop));
        ModSettingsManager.AddOption(new CheckBoxOption(config.teleportCommandOnTeleport));
        ModSettingsManager.AddOption(new CheckBoxOption(config.teleportCommandObeyTier));

        // The filter entries only exist once the item catalog is ready (see ItemFilter), which is
        // later than this method runs, so they are added when the config reports ready.
        if (config.Ready)
            RegisterFilters(config);
        else
            config.OnConfigReady += () => RegisterFilters(config);
    }

    private static void RegisterFilters(Config config)
    {
        try
        {
            proxyFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "AutoItemPickup.riskofoptions.unused"), false)
            {
                SaveOnConfigSet = false
            };

            AddFilter(config.OnDrop);
            AddFilter(config.OnTeleport);
        }
        catch (Exception e)
        {
            AutoItemPickup.Log.LogWarning(
                $"Could not add the item filters to the Risk Of Options menu; they still work in the .cfg file.\n{e}");
        }
    }

    private static void AddFilter(ItemFilter filter)
    {
        AddTextOption(filter.TierWhitelistEntry, ItemTierSet.Deserialize);
        AddTextOption(filter.ItemWhitelistEntry, ItemSet.Deserialize);
        AddTextOption(filter.ItemBlacklistEntry, ItemSet.Deserialize);
    }

    private static void AddTextOption<T>(ConfigEntry<T> real, Func<string, T> parse) where T : class
    {
        var definition = real.Definition;
        var proxy = proxyFile.Bind(definition, real.Value?.ToString() ?? "", real.Description);

        var syncing = false;

        // Menu -> real entry (and .cfg file)
        proxy.SettingChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            try
            {
                var parsed = parse(proxy.Value ?? "");
                if (parsed.ToString() != (real.Value?.ToString() ?? ""))
                    real.Value = parsed;
            }
            catch (Exception e)
            {
                AutoItemPickup.Log.LogWarning($"Invalid value for {definition.Section}/{definition.Key}: {e.Message}");
            }
            finally
            {
                syncing = false;
            }
        };

        // Real entry (e.g. .cfg edited while the game is running) -> menu
        real.SettingChanged += (_, _) =>
        {
            if (syncing) return;
            syncing = true;
            try
            {
                var text = real.Value?.ToString() ?? "";
                if (proxy.Value != text)
                    proxy.Value = text;
            }
            finally
            {
                syncing = false;
            }
        };

        ModSettingsManager.AddOption(new StringInputFieldOption(proxy));
    }
}
#endif
