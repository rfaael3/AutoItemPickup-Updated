#if RISKOFOPTIONS
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutoItemPickup.Configuration;
using BepInEx;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.Options;
using UnityEngine;

namespace AutoItemPickup;

/// <summary>
///     Exposes the mod's config in the in-game Risk Of Options menu.
///     Only compiled when RiskOfOptions.dll is found at build time, and only called when the
///     Risk Of Options plugin is actually loaded.
///     Changes are written to the .cfg file and the mod reads the values live, so they apply immediately.
///     Distribution settings are host-side; automatic Command interaction uses each player's local settings.
/// </summary>
internal static class RiskOfOptionsCompat
{
    public const string Guid = "com.rune580.riskofoptions";

    // Risk Of Options can only draw simple types (bool, enum, string...). The tier/item lists are custom types,
    // so each one gets a text "proxy" entry that lives in a throwaway in-memory ConfigFile (never saved to disk)
    // and is kept in sync with the real entry in both directions.
    private static ConfigFile proxyFile;
    private static Sprite modIcon;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register(Config config)
    {
        RegisterIcon();
        ModSettingsManager.SetModDescription(
            "Automatically distributes items among players using the host's settings. " +
            "Optional CommandQueue auto-interaction uses each player's local queue and settings.");

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
        ModSettingsManager.AddOption(new CheckBoxOption(config.autoOpenQueuedCommand));
        ModSettingsManager.AddOption(new FloatFieldOption(config.queuedCommandRadius));

        // The filter entries only exist once the item catalog is ready (see ItemFilter), which is
        // later than this method runs, so they are added when the config reports ready.
        if (config.Ready)
            RegisterFilters(config);
        else
            config.OnConfigReady += () => RegisterFilters(config);
    }

    private static void RegisterIcon()
    {
        Texture2D texture = null;
        try
        {
            if (!modIcon)
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AutoItemPickup.icon.png");
                if (stream == null) throw new FileNotFoundException("Embedded mod icon was not found.");
                using var data = new MemoryStream();
                stream.CopyTo(data);
                texture = new Texture2D(2, 2);
                if (!texture.LoadImage(data.ToArray())) throw new InvalidDataException("Could not decode the mod icon.");
                modIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            ModSettingsManager.SetModIcon(modIcon);
        }
        catch (Exception e)
        {
            if (texture && !modIcon) UnityEngine.Object.Destroy(texture);
            AutoItemPickup.Log.LogWarning($"Could not load the Risk Of Options icon; settings remain available.\n{e}");
        }
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
