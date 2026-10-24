using AutoItemPickup.Configuration;
using MonoMod.Cil;

namespace AutoItemPickup.Hooks;

public abstract class AbstractHookHandler
{
    internal HookManager hookManager;

    protected AutoItemPickup Plugin => hookManager.Plugin;
    protected Config ModConfig => Plugin.ModConfig;

    public virtual string Name => GetType().Name;

    public void Init(HookManager _hookManager)
    {
        hookManager = _hookManager;
    }

    //TODO: Figure out if those can be automated by storing reference to hook and method
    public abstract void RegisterHooks();

    public abstract void UnregisterHooks();

    /// <summary>
    ///     Logs that an IL patch could not find its injection point, instead of throwing.
    ///     The method stays unpatched and only the feature that depends on it is lost.
    /// </summary>
    protected void LogPatchFailed(ILContext il, string what)
    {
        AutoItemPickup.Log.LogError(
            $"[{Name}] Could not find {what} in {il.Method.FullName}. " +
            "The game was probably updated and this feature is disabled until the mod is updated.");
    }
}
