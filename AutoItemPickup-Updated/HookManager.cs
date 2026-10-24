using System;
using System.Collections.Generic;

namespace AutoItemPickup.Hooks;

public class HookManager
{
    public readonly Dictionary<Type, AbstractHookHandler> HookHandlers = new();

    public readonly AutoItemPickup Plugin;

    public HookManager(AutoItemPickup pluginInstance)
    {
        Plugin = pluginInstance;

        RegisterHandler<CreatePickupDropletHandler>();
        RegisterHandler<PrinterTargetHandler>();
        RegisterHandler<ScrapperTargetHandler>();
        RegisterHandler<CommandTargetHandler>();
        RegisterHandler<CommandHandler>();
        RegisterHandler<ItemHandler>();
    }

    public void RegisterHandler<T>() where T : AbstractHookHandler, new()
    {
        var instance = new T();
        instance.Init(this);
        HookHandlers[typeof(T)] = instance;
    }

    public T GetHandler<T>() where T : AbstractHookHandler
    {
        return (T)HookHandlers[typeof(T)];
    }

    /// <summary>
    ///     Registers every handler independently. A handler whose hooks no longer match the game
    ///     (renamed/removed method, changed IL) is logged and skipped, so one breakage does not
    ///     stop the whole plugin from loading.
    /// </summary>
    public void RegisterHooks()
    {
        foreach (var handler in HookHandlers.Values)
            try
            {
                handler.RegisterHooks();
            }
            catch (Exception e)
            {
                AutoItemPickup.Log.LogError(
                    $"[{handler.Name}] Failed to register hooks; this feature is disabled. " +
                    $"The game was probably updated.\n{e}");

                // Remove whatever was registered before the failure
                try
                {
                    handler.UnregisterHooks();
                }
                catch (Exception)
                {
                    // Nothing else to do
                }
            }
    }

    public void UnregisterHooks()
    {
        foreach (var handler in HookHandlers.Values)
            try
            {
                handler.UnregisterHooks();
            }
            catch (Exception e)
            {
                AutoItemPickup.Log.LogWarning($"[{handler.Name}] Failed to unregister hooks:\n{e}");
            }
    }
}
