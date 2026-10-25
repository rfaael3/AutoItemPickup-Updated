using System.Collections;
using AutoItemPickup.Hooks;
using RoR2;
using UnityEngine;

// Runs the production handler against small game/queue doubles. This reproduces the UI hook
// contract that failed in the user's log; it is not an in-game or networking test.
var plugin = new AutoItemPickup.AutoItemPickup();
var queuePlugin = new CommandQueue.CommandQueue();
BepInEx.Bootstrap.Chainloader.PluginInfos["com.kuberoot.commandqueue"] = new() { Instance = queuePlugin };
var handler = new CommandQueueHandler { Plugin = plugin };
var body = new CharacterBody();
var interactor = new Interactor();
body.gameObject.Components[typeof(Interactor)] = interactor;
LocalUserManager.User = new() { cachedBody = body, cachedMaster = new() };
var prefab = new PickupPickerController { panelPrefab = new() };
RoR2.Artifacts.CommandArtifactManager.commandCubePrefab = prefab.gameObject;
prefab.gameObject.Components[typeof(PickupPickerController)] = prefab;
int panelEffects = 0;
queuePlugin.InstallHooks();
On.RoR2.PickupPickerController.OnDisplayBegin += (orig, self, prompt, user, camera) => {
    orig(self, prompt, user, camera);
    panelEffects++;
    // LookingGlass assumes orig created a panel; Nick's queued path creates none.
    if (!self.PanelCreated) throw new NullReferenceException("LookingGlass missing panel");
};

PickupPickerController Cube(int id = 1, float distance = 0) {
    var c = new PickupPickerController { panelPrefab = prefab.panelPrefab,
        options = new[] { new PickupPickerController.Option { available = true, pickup = new() { pickupIndex = new(id) } } } };
    c.transform.position = new(distance);
    InstanceTracker.GetInstancesList<PickupPickerController>().Add(c);
    return c;
}
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
void Tick(float delta = 1.1f) { Time.time += delta; RoR2Application.Tick(); }

CommandQueue.QueueManager.Choices.Add((ItemTier.Tier1, new(1)));
var reproduced = false;
var broken = Cube();
try { interactor.AttemptInteraction(broken.gameObject); }
catch (NullReferenceException) { reproduced = true; }
Check(reproduced && queuePlugin.Selected == 1, "original Nick + panel-effect path reproduces logged exception after selecting an item");
queuePlugin.Selected = 0; panelEffects = 0;
handler.RegisterHooks();
Run.Start();
Cube(); Tick();
Check(queuePlugin.Selected == 1 && panelEffects == 0, "automatic choice bypasses panel-only effects");
Cube(); Tick(); Cube(); Tick();
Check(queuePlugin.Selected == 3 && panelEffects == 0, "multiple consecutive essences continue selecting");
plugin.ModConfig.autoOpenQueuedCommand.Value = false;
var manual = Cube(); Tick();
Check(queuePlugin.Selected == 3, "Risk Of Options toggle stops automatic interaction");
CommandQueue.QueueManager.Choices.Clear();
interactor.AttemptInteraction(manual.gameObject);
Check(manual.PanelCreated && panelEffects == 1, "manual interaction retains the normal UI chain");
InstanceTracker.GetInstancesList<PickupPickerController>().Remove(manual);
CommandQueue.QueueManager.Choices.Add((ItemTier.Tier1, new(1)));
plugin.ModConfig.autoOpenQueuedCommand.Value = true;
Cube(); Tick();
Check(queuePlugin.Selected == 4, "automatic interaction resumes after re-enabling");
CommandQueue.QueueManager.Choices.Clear();
var empty = Cube(); Tick();
Check(queuePlugin.Selected == 4, "empty queue stays manual");
InstanceTracker.GetInstancesList<PickupPickerController>().Remove(empty);
CommandQueue.QueueManager.Choices.Add((ItemTier.Tier1, new(1)));
var unavailable = Cube(2); Tick();
Check(queuePlugin.Selected == 4, "unavailable choice stays manual");
InstanceTracker.GetInstancesList<PickupPickerController>().Remove(unavailable);
var temp = Cube(); temp.options[0].pickup.isTempItem = true; Tick();
Check(queuePlugin.Selected == 4, "temporary essence stays manual");
InstanceTracker.GetInstancesList<PickupPickerController>().Remove(temp);
var far = Cube(distance: 20); Tick();
Check(queuePlugin.Selected == 4, "radius still limits interaction");
InstanceTracker.GetInstancesList<PickupPickerController>().Remove(far);
Cube(); interactor.FailOnce = true; Tick(); Tick(3);
Check(queuePlugin.Selected == 5, "transient interaction error does not disable future scans");
var busy = new NetworkUIPromptController { currentParticipantMaster = LocalUserManager.User.cachedMaster };
InstanceTracker.GetInstancesList<NetworkUIPromptController>().Add(busy);
Cube(); Tick();
Check(queuePlugin.Selected == 5, "another active selection is not interrupted");
InstanceTracker.GetInstancesList<NetworkUIPromptController>().Clear();
Tick(); Check(queuePlugin.Selected == 6, "scanning resumes after another selection closes");
handler.UnregisterHooks(); Cube(); Tick();
Check(queuePlugin.Selected == 6, "unregister removes update subscription");

namespace UnityEngine {
    public class Object { public static implicit operator bool(Object obj) => obj is not null; }
    public struct Vector3(float x) { public float x = x; public float sqrMagnitude => x * x; public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x); }
    public class Transform { public Vector3 position; }
    public class GameObject : Object { public Dictionary<Type, object> Components = new(); public T GetComponent<T>() => Components.TryGetValue(typeof(T), out var c) ? (T)c : default; }
    public class Component : Object {
        public GameObject gameObject = new(); public Transform transform = new(); public bool isActiveAndEnabled = true;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        public void StartCoroutine(IEnumerator e) { while (e.MoveNext()) { } }
    }
    public static class Time { public static float time; }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a,b); public static float Max(float a, float b) => Math.Max(a,b); }
}
namespace BepInEx { public class BaseUnityPlugin : Component { } public class PluginInfo { public BaseUnityPlugin Instance; } }
namespace BepInEx.Bootstrap { public static class Chainloader { public static Dictionary<string,BepInEx.PluginInfo> PluginInfos = new(); } }
namespace AutoItemPickup {
    public class Setting<T>(T value) { public T Value = value; }
    public class TestConfig { public Setting<bool> autoOpenQueuedCommand = new(true); public Setting<float> queuedCommandRadius = new(8); }
    public class TestLog { public void LogWarning(string text) { } public void LogInfo(string text) { } public void LogError(string text) { } }
    public class AutoItemPickup : BepInEx.BaseUnityPlugin { public static TestLog Log = new(); public TestConfig ModConfig = new(); }
}
namespace AutoItemPickup.Hooks { public abstract class AbstractHookHandler { public AutoItemPickup Plugin; protected TestConfig ModConfig => Plugin.ModConfig; public abstract void RegisterHooks(); public abstract void UnregisterHooks(); } }
namespace RoR2 {
    public enum ItemTier { Tier1 }
    public readonly record struct PickupIndex(int value) { public static PickupIndex none => new(-1); }
    public struct UniquePickup { public PickupIndex pickupIndex; public bool isTempItem, isRental; }
    public enum Interactability { Available, Disabled }
    public class CharacterMaster : Component { }
    public class HealthComponent : Component { public bool alive = true; }
    public class InputBank : Component { public Vector3 aimOrigin; }
    public class CharacterBody : Component { public HealthComponent healthComponent = new(); public InputBank inputBank = new(); public Vector3 corePosition; }
    public class LocalUser { public CharacterBody cachedBody; public CharacterMaster cachedMaster; }
    public class CameraRigController : Component { }
    public static class LocalUserManager { public static LocalUser User; public static LocalUser GetFirstLocalUser() => User; }
    public static class InstanceTracker { static class Items<T> { public static List<T> Values = new(); } public static List<T> GetInstancesList<T>() => Items<T>.Values; }
    public class Run : Component { public static Run instance; public static event Action<Run> onRunStartGlobal; public static void Start() { instance = new(); onRunStartGlobal?.Invoke(instance); } }
    public static class RoR2Application { public static event Action onUpdate; public static void Tick() => onUpdate?.Invoke(); }
    public class NetworkUIPromptController : Component { public CharacterMaster currentParticipantMaster; }
    public class PickupPickerController : Component {
        public struct Option { public UniquePickup pickup; public bool available; }
        public GameObject panelPrefab; public float cutoffDistance = 12; public Option[] options; public bool PanelCreated;
        public PickupPickerController() { gameObject.Components[typeof(PickupPickerController)] = this; }
        public Interactability GetInteractability(Interactor interactor) => Interactability.Available;
    }
    public class Interactor : Component {
        public bool FailOnce;
        public void AttemptInteraction(GameObject obj) {
            if (FailOnce) { FailOnce = false; throw new Exception("transient network failure"); }
            var c = obj.GetComponent<PickupPickerController>();
            On.RoR2.PickupPickerController.Display(c, new(), LocalUserManager.User, new());
        }
    }
}
namespace RoR2.Artifacts { public static class CommandArtifactManager { public static GameObject commandCubePrefab; } }
namespace On.RoR2 {
    public static class PickupPickerController {
        public delegate void orig_OnDisplayBegin(global::RoR2.PickupPickerController self, NetworkUIPromptController prompt, LocalUser user, CameraRigController camera);
        public delegate void hook_OnDisplayBegin(orig_OnDisplayBegin orig, global::RoR2.PickupPickerController self, NetworkUIPromptController prompt, LocalUser user, CameraRigController camera);
        public static event hook_OnDisplayBegin OnDisplayBegin;
        public static void Display(global::RoR2.PickupPickerController c, NetworkUIPromptController p, LocalUser u, CameraRigController cam) {
            orig_OnDisplayBegin chain = (self, prompt, user, camera) => self.PanelCreated = true;
            foreach (hook_OnDisplayBegin hook in OnDisplayBegin.GetInvocationList()) { var prior = chain; chain = (self,prompt,user,camera) => hook(prior,self,prompt,user,camera); }
            chain(c,p,u,cam);
        }
    }
}
namespace CommandQueue {
    public static class QueueManager { public static List<(ItemTier,PickupIndex)> Choices = new(); public static IEnumerable<(ItemTier,PickupIndex)> PeekAllUnreserved() => Choices; }
    public class CommandQueue : BepInEx.BaseUnityPlugin {
        public int Selected;
        public void InstallHooks() {
            global::On.RoR2.PickupPickerController.OnDisplayBegin += (orig, self, prompt, user, camera) => {
                foreach (var (tier, pickup) in QueueManager.PeekAllUnreserved())
                    if (self.options.Any(o => o.available && o.pickup.pickupIndex == pickup)) {
                        self.StartCoroutine(SubmitQueuedChoiceNextFrame(self, tier, pickup, prompt, user, camera)); return;
                    }
                orig(self, prompt, user, camera);
            };
        }
        private static IEnumerator SubmitQueuedChoiceNextFrame(PickupPickerController self, ItemTier tier, PickupIndex pickup, NetworkUIPromptController prompt, LocalUser user, CameraRigController camera) {
            yield return null;
            ((CommandQueue)BepInEx.Bootstrap.Chainloader.PluginInfos["com.kuberoot.commandqueue"].Instance).Selected++;
            InstanceTracker.GetInstancesList<PickupPickerController>().Remove(self);
        }
    }
}
