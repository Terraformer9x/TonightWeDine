using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using System.IO;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TonightWeDine;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony harmony = new(PluginInfo.PLUGIN_GUID);
    public static Plugin Instance;

    private void Awake()
    {
        Instance ??= this;

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        TonightWeDineConfig.Bind(base.Config);
        Assets.LoadAssets();

        harmony.PatchAll(typeof(DineOverridePatch));
        if (Chainloader.PluginInfos.ContainsKey("butterystancakes.lethalcompany.chameleon"))
        {
            harmony.PatchAll(typeof(ChameleonFancyDoorPatch));
        }

        SceneManager.sceneUnloaded += delegate
        {
            DineOverride.executed = false;
            DineOverride.fancyExecuted = false;
        };
    }

    public static void Log(string msg) => Instance.Logger.LogInfo(msg);
    public static void LogError(string msg) => Instance.Logger.LogError(msg);
    public static void LogDebug(string msg) => Instance.Logger.LogDebug(msg);
}

class DineOverridePatch
{
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
    [HarmonyPriority(900)]
    [HarmonyPostfix]
    static void FinishGeneratingNewLevel_DineOverridePatch()
    {
        if (StartOfRound.Instance.currentLevel.name == "DineLevel")
        {
            if (DineOverride.executed) return;
            DineOverride.executed = true;

            if (TonightWeDineConfig.dineLayout.Value >= 0)
            {
                Plugin.LogDebug($"Dine's layout is \"" + TonightWeDineConfig.dineLayout.Value + "\"");
                DineOverride.DineSceneOverride((int)TonightWeDineConfig.dineLayout.Value);
            }
            Plugin.LogDebug("Fixing Dine's pits");
            DineOverride.DineFixes();
        }
    }
}
class ChameleonFancyDoorPatch
{
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
    [HarmonyPriority(300)]
    [HarmonyPostfix]
    static void FancyDoorsPatch()
    {
        if (StartOfRound.Instance.currentLevel.name == "DineLevel" && TonightWeDineConfig.dineLayout.Value >= 0)
        {
            if (DineOverride.fancyExecuted) return;
            DineOverride.fancyExecuted = true;

            DineOverride.FancyDoorOverride();
        }
    }
}

internal static class Assets
{
    internal static AssetBundle assetBundle;
    internal static Mesh newSurfaceMesh, metalWallMesh;
    internal static GameObject treeNearFire, leavesNearFire;

    internal static void LoadAssets()
    {
        try
        {
            assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "tonightwedine"));
            newSurfaceMesh = assetBundle.LoadAsset<Mesh>("DineHybridTerrain");
            metalWallMesh = assetBundle.LoadAsset<Mesh>("Cube.001_0");
            treeNearFire = assetBundle.LoadAsset<GameObject>("NearFireTree");
            leavesNearFire = assetBundle.LoadAsset<GameObject>("NearFireLeaves");
            assetBundle.Unload(false);
        }
        catch 
        {
            Plugin.LogError("Failed to load mod assets!");
            return;
        }
    }
}

internal static class DineOverride
{
    internal static bool executed;
    internal static bool fancyExecuted;

    internal static void DineSceneOverride(int dineVersion)
    {
        if (dineVersion < 0) return;

        GameObject.Find("/Environment/NeonLightsSingle")?.SetActive(false);
        Transform dineFireExitBuilding = GameObject.Find("/Environment/Map/CementFacility1/Cube.002")?.transform;
        Transform dineFireExitDoor = GameObject.Find("/Environment/FireExitDoorContainer/FireExitDoor")?.transform;
        Transform dineEntranceTeleportB = GameObject.Find("/Environment/Teleports/EntranceTeleportB")?.transform;

        switch (dineVersion)
        {
            case 0: //V50

                //Move fire exit
                if (dineFireExitBuilding != null)
                {
                    dineFireExitBuilding.localPosition = new Vector3(21.4799995f, 1.69000006f, 54.770000f);
                    dineFireExitBuilding.localRotation = new Quaternion(0.0436081327f, -0.706019938f, -0.705609918f, -0.0418210514f);
                    dineFireExitBuilding.localScale = new Vector3(0.683414221f, 0.899168074f, 0.683414221f);
                }

                if (dineFireExitDoor != null)
                {
                    dineFireExitDoor.localPosition = new Vector3(-73.695549f, 9.62242985f, 48.685997f);
                    dineFireExitDoor.localRotation = new Quaternion(0f, 0.857689738f, 0f, -0.514167666f);
                }

                if (dineEntranceTeleportB != null)
                {
                    dineEntranceTeleportB.localPosition = new Vector3(137.394073f, -15.564064f, 46.2363853f);
                    dineEntranceTeleportB.localRotation = new Quaternion(0f, 0.998192191f, 0f, 0.0601029396f);
                    dineEntranceTeleportB.localScale = new Vector3(1.93347573f, 3.46227288f, 0.532179296f);
                }
                break;

            case 1: //Hybrid

                //Change surface mesh to make the path to fire climbable
                GameObject dineSurface = GameObject.Find("/Environment/NewDineTerrainCutdown");
                if (dineSurface != null)
                {
                    dineSurface.GetComponent<MeshFilter>().mesh = Assets.newSurfaceMesh;
                    dineSurface.GetComponent<MeshCollider>().sharedMesh = Assets.newSurfaceMesh;
                }

                //Add a wind trigger in front of the fire exit to prevent overlapping ambience
                Transform dineWindTriggers = GameObject.Find("/Environment/ReverbTriggers (1)/WindTriggers")?.transform;
                GameObject dineShipWindTrigger = GameObject.Find("/Environment/ReverbTriggers (1)/WindTriggers/shipWindTrigger2");
                if (dineShipWindTrigger != null && dineWindTriggers != null)
                {
                    GameObject dineFireExitWindTrigger = GameObject.Instantiate(dineShipWindTrigger, dineWindTriggers);
                    dineFireExitWindTrigger.transform.localPosition = new Vector3(-193.034271f, 7.07456207f, -83.6474533f);
                    dineFireExitWindTrigger.transform.localRotation = new Quaternion(0f, 0f, 0f, 1f);
                    dineFireExitWindTrigger.transform.localScale = new Vector3(12.9657545f, 6.90071535f, 11.5199661f);
                }

                //Add some flavor foliage to make the space near fire not feel empty
                if (RoundManager.Instance.mapPropsContainer == null) RoundManager.Instance.mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
                if (RoundManager.Instance.mapPropsContainer != null)
                {
                    GameObject.Instantiate(Assets.treeNearFire, RoundManager.Instance.mapPropsContainer.transform);
                    GameObject.Instantiate(Assets.leavesNearFire, RoundManager.Instance.mapPropsContainer.transform);
                }

                //Move fire exit
                if (dineFireExitBuilding != null)
                {
                    dineFireExitBuilding.localPosition = new Vector3(-15.267663f, 5.13949013f, 29.4419918f);
                    dineFireExitBuilding.localRotation = new Quaternion(-0.233415499f, -0.667712033f, -0.66668123f, 0.234976724f);
                    dineFireExitBuilding.localScale = new Vector3(0.524179399f, 0.646127939f, 0.478014588f);
                }

                if (dineFireExitDoor != null)
                {
                    dineFireExitDoor.localPosition = new Vector3(-218.790543f, 32.1971855f, -177.656601f);
                    dineFireExitDoor.localRotation = new Quaternion(0f, 0.599288702f, 0f, -0.800532997f);
                }

                if (dineEntranceTeleportB != null)
                {
                    dineEntranceTeleportB.localPosition = new Vector3(-28.0669994f, 1.40999985f, -70.1849976f);
                    dineEntranceTeleportB.localRotation = new Quaternion(0f, 0.943722844f, 0f, -0.330737323f);
                    dineEntranceTeleportB.localScale = new Vector3(1.93347573f, 3.46227288f, 0.532179296f);
                }
                break;
        }
        
        //Move main entrance
        GameObject dineEntranceMetalWall1 = GameObject.Find("/Environment/Map/Cube.001 (16)");
        if (dineEntranceMetalWall1 != null)
        {
            dineEntranceMetalWall1.GetComponent<MeshFilter>().mesh = Assets.metalWallMesh;
            dineEntranceMetalWall1.transform.localPosition = new Vector3(179.340607f, -20.2499943f, -22.0727997f);
            dineEntranceMetalWall1.transform.localScale = new Vector3(1f, 1.40002608f, 1.14519989f);
        }

        BoxCollider dineEntranceMetalWall2 = GameObject.Find("/Environment/Map/Cube.001 (8)")?.GetComponent<BoxCollider>();
        if (dineEntranceMetalWall2 != null)
        {
            dineEntranceMetalWall2.center = new Vector3(-0.140106767f, -2.72914982f, -0.260226041f);
            dineEntranceMetalWall2.size = new Vector3(0.533142745f, 6.80501652f, 4.4800849f);
        }

        BoxCollider dineEntranceMetalWall3 = GameObject.Find("/Environment/Map/Cube.001 (4)")?.GetComponent<BoxCollider>();
        if (dineEntranceMetalWall3 != null)
        {
            dineEntranceMetalWall3.center = new Vector3(-0.140108287f, -1.05176842f, -0.260226041f);
            dineEntranceMetalWall3.size = new Vector3(0.533142745f, 10.1597633f, 4.4800849f);
        }

        Transform dinePlane = GameObject.Find("/Environment/Plane")?.transform;
        if (dinePlane != null)
        {
            dinePlane.localPosition = new Vector3(178.842026f, -20.4050007f, -16.5081291f);
            dinePlane.localRotation = new Quaternion(0.524920106f, -0.473770887f, -0.473770201f, -0.524920762f);
        }

        Transform dineSteelDoorFake1 = GameObject.Find("/Environment/SteelDoorFake")?.transform;
        if (dineSteelDoorFake1 != null)
        {
            dineSteelDoorFake1.localPosition = new Vector3(178.729004f, -20.3400002f, -15.1700001f);
            dineSteelDoorFake1.localRotation = new Quaternion(0.0381978266f, -0.706074417f, -0.706074357f, -0.0381968804f);
        }

        Transform dineSteelDoorFake2 = GameObject.Find("/Environment/SteelDoorFake (1)")?.transform;
        if (dineSteelDoorFake2 != null)
        {
            dineSteelDoorFake2.localPosition = new Vector3(179.060455f, -20.3400021f, -18.1193371f);
            dineSteelDoorFake2.localRotation = new Quaternion(0.0395424701f, -0.706000686f, -0.70599997f, -0.0395425074f);
        }

        Transform dineDoorFrame = GameObject.Find("/Environment/DoorFrame (1)")?.transform;
        if (dineDoorFrame != null)
        {
            dineDoorFrame.localPosition = new Vector3(178.901993f, -23.8069992f, -16.6560001f);
            dineDoorFrame.localRotation = new Quaternion(0.0393358283f, -0.70601213f, -0.706011534f, -0.0393358618f);
        }

        Transform dineEntranceTeleportA = GameObject.Find("/Environment/Teleports/EntranceTeleportA")?.transform;
        if (dineEntranceTeleportA != null)
        {
            dineEntranceTeleportA.localPosition = new Vector3(178.678314f, -21.2327003f, -16.6628532f);
            dineEntranceTeleportA.localRotation = new Quaternion(0f, 0.99843365f, 0f, 0.0559489727f);
        }

        Transform dineScanNode = GameObject.Find("/Environment/ScanNodes/ScanNode")?.transform;
        if (dineScanNode != null)
        {
            dineScanNode.localPosition = new Vector3(175.699997f, -24.0100002f, -19.5f);
            dineScanNode.localRotation = new Quaternion(0.707106829f, -0.707106829f, -2.43634008e-06f, 2.43634008e-06f);
            dineScanNode.localScale = new Vector3(9.13990879f, 138.734482f, 4.0328393f);
        }

        GameObject dineEnvironment = GameObject.Find("/Environment");
        if (dineEnvironment != null)
        {
            dineEnvironment.GetComponent<NavMeshSurface>().RemoveData();
            dineEnvironment.GetComponent<NavMeshSurface>().BuildNavMesh();
        }
    }

    internal static void FancyDoorOverride()
    {
        Transform fancyDoors = GameObject.Find("/Environment/MapPropsContainer/WideDoorFrame(Clone)")?.transform;
        if (fancyDoors != null)
        {
            Plugin.LogDebug("Found Chameleon's fancy door, moving them");
            fancyDoors.transform.position = new Vector3(-156.552994f, -15.1260004f, 12.1549997f);
            fancyDoors.transform.rotation = new Quaternion(-0.706181169f, -0.0361691155f, -0.0361691155f, 0.706181169f);
        }
    }

    internal static void DineFixes()
    {
        Transform dineKillTrigger1 = GameObject.Find("/Environment/Map/KillTrigger (1)")?.transform;
        if (dineKillTrigger1 != null)
        {
            dineKillTrigger1.localScale = new Vector3(349.604065f, 0.690639973f, 181.372696f);
        }
        Transform dineKillTrigger2 = GameObject.Find("/Environment/Map/KillTrigger (4)")?.transform;
        if (dineKillTrigger2 != null)
        {
            dineKillTrigger2.localScale = new Vector3(42.2234726f, 0.690639973f, 81.233429f);
        }
    }
}