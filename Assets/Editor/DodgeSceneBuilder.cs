using System.Collections.Generic;
using System.IO;
using DodgeArena;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the whole Dodge Arena scene from code (menu: Dodge Arena > Build Scene),
/// so nothing has to be assembled by hand. Also builds a Windows player for fast,
/// headless training (menu: Dodge Arena > Build Training Player).
/// </summary>
public static class DodgeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/DodgeArena.unity";
    private const string PrefabPath = "Assets/Prefabs/TrainingArea.prefab";
    private const string MaterialFolder = "Assets/Materials";
    private const string PlayerPath = "Builds/DodgeArena.exe";
    private const string BehaviorName = "Dodger";

    private const int AreaColumns = 3;
    private const int AreaRows = 3;
    private const float AreaSpacing = 30f;
    private const float ArenaSize = 20f;
    private const int ObstacleCount = 3;

    [MenuItem("Dodge Arena/Build Scene")]
    public static void BuildScene()
    {
        EnsureTags();
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Prefabs");
        Directory.CreateDirectory(MaterialFolder);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject area = CreateArea();
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            area, PrefabPath, InteractionMode.AutomatedAction);

        // Many copies of the arena = more experience per second during training.
        for (int i = 1; i < AreaColumns * AreaRows; i++)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            copy.name = $"TrainingArea ({i})";
            copy.transform.position = new Vector3((i % AreaColumns) * AreaSpacing, 0f, (i / AreaColumns) * AreaSpacing);
        }

        Camera camera = Camera.main;
        camera.transform.position = new Vector3(0f, 21f, -15f);
        camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[DodgeArena] Scene built: " + ScenePath);
    }

    [MenuItem("Dodge Arena/Build Training Player")]
    public static void BuildTrainingPlayer()
    {
        if (!File.Exists(ScenePath))
        {
            BuildScene();
        }
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = PlayerPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[DodgeArena] Player build result: " + report.summary.result);
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Batch-mode entry point: builds the scene and the player in one go.</summary>
    public static void BuildAllBatch()
    {
        BuildScene();
        BuildTrainingPlayer();
    }

    private static GameObject CreateArea()
    {
        Material floorMat = CreateMaterial("Floor", new Color(0.32f, 0.36f, 0.42f));
        Material wallMat = CreateMaterial("Wall", new Color(0.55f, 0.55f, 0.58f));
        Material agentMat = CreateMaterial("Agent", new Color(0.15f, 0.45f, 0.95f));
        Material targetMat = CreateMaterial("Target", new Color(1f, 0.82f, 0.1f));
        Material ballMat = CreateMaterial("Ball", new Color(0.9f, 0.15f, 0.15f));

        var area = new GameObject("TrainingArea");

        GameObject floor = CreateBox("Floor", area.transform, new Vector3(0f, -0.25f, 0f),
            new Vector3(ArenaSize, 0.5f, ArenaSize), floorMat);
        floor.tag = "Untagged";

        float half = ArenaSize / 2f + 0.25f;
        CreateBox("Wall N", area.transform, new Vector3(0f, 0.5f, half), new Vector3(ArenaSize + 1f, 1f, 0.5f), wallMat).tag = Tags.Wall;
        CreateBox("Wall S", area.transform, new Vector3(0f, 0.5f, -half), new Vector3(ArenaSize + 1f, 1f, 0.5f), wallMat).tag = Tags.Wall;
        CreateBox("Wall E", area.transform, new Vector3(half, 0.5f, 0f), new Vector3(0.5f, 1f, ArenaSize), wallMat).tag = Tags.Wall;
        CreateBox("Wall W", area.transform, new Vector3(-half, 0.5f, 0f), new Vector3(0.5f, 1f, ArenaSize), wallMat).tag = Tags.Wall;

        var obstacles = new Transform[ObstacleCount];
        for (int i = 0; i < ObstacleCount; i++)
        {
            GameObject obstacle = CreateBox($"Obstacle {i}", area.transform, new Vector3(i * 3f - 3f, 0.75f, 3f),
                new Vector3(3f, 1.5f, 1f), wallMat);
            obstacle.tag = Tags.Wall;
            obstacles[i] = obstacle.transform;
        }

        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        target.name = "Target";
        target.tag = Tags.Target;
        target.transform.SetParent(area.transform, false);
        target.transform.localPosition = new Vector3(4f, 0.5f, 4f);
        target.transform.localScale = Vector3.one * 0.8f;
        target.GetComponent<Collider>().isTrigger = true;
        target.GetComponent<Renderer>().sharedMaterial = targetMat;

        var spawner = area.AddComponent<BallSpawner>();
        spawner.ballMaterial = ballMat;

        GameObject agentObject = CreateAgent(area.transform, agentMat);
        var agent = agentObject.GetComponent<DodgeAgent>();
        agent.target = target.transform;
        agent.obstacles = obstacles;
        agent.spawner = spawner;

        return area;
    }

    private static GameObject CreateAgent(Transform parent, Material material)
    {
        GameObject agentObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        agentObject.name = "Agent";
        agentObject.tag = Tags.Agent;
        agentObject.transform.SetParent(parent, false);
        agentObject.transform.localPosition = new Vector3(0f, 0.5f, -4f);
        agentObject.GetComponent<Renderer>().sharedMaterial = material;

        // Small "nose" so the viewer can see which way the agent faces.
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose";
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.transform.SetParent(agentObject.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.2f, 0.55f);
        nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.2f);
        nose.GetComponent<Renderer>().sharedMaterial = material;

        var body = agentObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ
            | RigidbodyConstraints.FreezePositionY;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Agent has [RequireComponent(typeof(BehaviorParameters))], so this also adds BehaviorParameters.
        var agent = agentObject.AddComponent<DodgeAgent>();
        agent.MaxStep = 2000;

        var behavior = agentObject.GetComponent<BehaviorParameters>();
        if (behavior == null)
        {
            behavior = agentObject.AddComponent<BehaviorParameters>();
        }
        behavior.BehaviorName = BehaviorName;
        behavior.BrainParameters.VectorObservationSize = 5;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);

        var rays = agentObject.AddComponent<RayPerceptionSensorComponent3D>();
        rays.SensorName = "DodgeRays";
        rays.DetectableTags = new List<string> { Tags.Target, Tags.Ball, Tags.Wall };
        rays.RaysPerDirection = 8;      // 8 left + 8 right + 1 forward = 17 rays
        rays.MaxRayDegrees = 180f;      // full 360 degree vision: balls can come from behind
        rays.SphereCastRadius = 0.35f;
        rays.RayLength = 22f;
        rays.ObservationStacks = 3;     // stacking lets the network infer ball velocity
        rays.StartVerticalOffset = 0f;
        rays.EndVerticalOffset = 0f;

        var decisions = agentObject.AddComponent<DecisionRequester>();
        decisions.DecisionPeriod = 5;
        decisions.TakeActionsBetweenDecisions = true;

        return agentObject;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = color;
            return existing;
        }
        var material = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureTags()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        foreach (string tag in new[] { Tags.Agent, Tags.Target, Tags.Ball, Tags.Wall })
        {
            bool exists = false;
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
