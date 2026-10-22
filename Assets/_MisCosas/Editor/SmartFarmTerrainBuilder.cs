using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the Smart Farm terrain inside "New Terrain.asset".
// Menu: Smart Farm > Build all   (or the three steps one by one)
//   1. heights : applies SmartFarmHeightmap.bytes (513 x 513 float32, row = Z, column = X, normalized to Terrain Height)
//   2. layers  : creates the TerrainLayer assets from the YughuesFreeGroundMaterials textures and paints them from
//                SmartFarmSplat.bytes (512 x 512, one layer index per cell)
//   3. water   : adds a "Lake Water" mesh at the lake level, using SmartFarmLakeData
// The layout is described in the vault note "Smart Farm - Terrain design".
public static class SmartFarmTerrainBuilder
{
    const string Dir = "Assets/_MisCosas";
    const string TerrainAssetPath = Dir + "/New Terrain.asset";
    const string HeightDataPath = Dir + "/Editor/SmartFarmHeightmap.bytes";
    const string SplatDataPath = Dir + "/Editor/SmartFarmSplat.bytes";
    const string LayersDir = Dir + "/Layers";
    const string MaterialsDir = Dir + "/Materiales";
    const string TextureDir = "Assets/YughuesFreeGroundMaterials/Textures/";

    const int HeightRes = 513;
    const int AlphaRes = 512;
    const float SizeXZ = 1000f;
    const float SizeY = 140f;

    // ------------------------------------------------------------------------------------------------------------
    // VALLEY SLOPE SETTINGS. Change these and run  Smart Farm > 6 Valley slope  again to try another one.
    // Every run starts from the same snapshot of the terrain (see ValleySnapshotPath), so tries do not stack.
    // ------------------------------------------------------------------------------------------------------------
    const float ValleySlope = 0.05f;        // metres of rise per metre going north. 0.0125 = 1.25 % (about 0.7 degrees)
    const float ValleyAnchorZ = 250f;         // at this Z (metres from the south edge) the floor height equals the next value
    const float ValleyAnchorHeight = 8f;      // floor height at ValleyAnchorZ, in metres
    const float PlaneEndZ = 560f;             // south of this Z the ground is exactly the slope; north of it, it blends into the mountain
    const float BlendEndZ = 720f;             // from this Z north the terrain is left exactly as it was
    const float StreamDepth = 3f;             // the stream channel is cut this deep into the slope
    const float EdgeRise = 22f;               // the west and east edges keep rising by this much, so the void is never visible
    const float EdgeWidth = 90f;              // ...over this distance from the edge
    const float MinHeight = 0.5f;             // the slope is never allowed below this, in metres
    const string ValleySnapshotPath = "_backups/ValleyBefore_heights.bytes";   // relative to the project folder; delete it to take a new snapshot

    struct LayerDef
    {
        public readonly string Name, Texture;
        public readonly float Tile;
        public LayerDef(string name, string texture, float tile) { Name = name; Texture = texture; Tile = tile; }
    }

    // Order matters: it is the layer index stored in SmartFarmSplat.bytes.
    static readonly LayerDef[] LayerDefs =
    {
        new LayerDef("Grass",    "Grass04",        18f), // 0 base
        new LayerDef("DryGrass", "Grass03",        22f), // 1 high ground
        new LayerDef("Rock",     "GroundStones02", 12f), // 2 steep slopes
        new LayerDef("CropSoil", "SoilGravel01",    6f), // 3 the ten plots
        new LayerDef("Path",     "SoilGravel03",    8f), // 4 lanes, hub, pen
        new LayerDef("Shore",    "Ground01",       10f), // 5 lake bed, shore, stream
    };

    [MenuItem("Smart Farm/Build all")]
    public static void BuildAll()
    {
        if (!BuildHeights()) return;
        if (!BuildLayers()) return;
        RestoreSceneObjects();
        Debug.Log("SmartFarm: terrain, layers and lake built. Keep the Terrain GameObject at position (0, 0, 0).");
    }

    // ---------------------------------------------------------------- valley slope

    // Stream centre line in terrain coordinates (x, z), from the lake spillway to the south edge.
    static readonly Vector2[] StreamLine =
    {
        new Vector2(520f, 713.2f), new Vector2(585f, 640f), new Vector2(690f, 550f),
        new Vector2(760f, 380f), new Vector2(800f, 200f), new Vector2(850f, 0f)
    };

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float DistanceToStream(float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i < StreamLine.Length - 1; i++)
        {
            Vector2 a = StreamLine[i], b = StreamLine[i + 1], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    static string SnapshotFullPath()
    {
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), ValleySnapshotPath);
    }

    // Returns the heights (metres) the slope is computed from: the snapshot, taken the first time this runs.
    static float[,] LoadOrTakeSnapshot(TerrainData data, int res, float sizeY)
    {
        string path = SnapshotFullPath();
        var heights = new float[res, res];
        if (!File.Exists(path))
        {
            float[,] current = data.GetHeights(0, 0, res, res);          // normalized 0..1
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = new byte[res * res * sizeof(float)];
            System.Buffer.BlockCopy(current, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"SmartFarm: snapshot of the terrain saved to {path}");
        }

        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length != res * res * sizeof(float))
        {
            Debug.LogError($"SmartFarm: the snapshot {path} does not match the terrain resolution ({res}). Delete it and run again.");
            return null;
        }
        System.Buffer.BlockCopy(raw, 0, heights, 0, raw.Length);
        for (int j = 0; j < res; j++)
        for (int i = 0; i < res; i++)
            heights[j, i] *= sizeY;
        return heights;
    }

    [MenuItem("Smart Farm/6 Valley slope")]
    static void ApplyValleySlope()
    {
        var data = LoadData();
        if (data == null) return;
        int res = data.heightmapResolution;
        Vector3 size = data.size;
        float[,] original = LoadOrTakeSnapshot(data, res, size.y);
        if (original == null) return;

        var result = new float[res, res];
        int clamped = 0;
        for (int j = 0; j < res; j++)
        {
            float z = j / (float)(res - 1) * size.z;
            float keep = Smooth(PlaneEndZ, BlendEndZ, z);                       // 0 = pure slope, 1 = original terrain
            for (int i = 0; i < res; i++)
            {
                float x = i / (float)(res - 1) * size.x;
                float edge = EdgeRise * (1f - Smooth(0f, EdgeWidth, Mathf.Min(x, size.x - x)));
                float plane = ValleyAnchorHeight + ValleySlope * (z - ValleyAnchorZ) + edge;
                float h = Mathf.Lerp(plane, original[j, i], keep);
                // The channel is only cut on the slope itself; near the lake the original spillway is kept as it is.
                h -= StreamDepth * (1f - Smooth(640f, 700f, z)) * (1f - Smooth(5f, 20f, DistanceToStream(x, z)));
                if (h < MinHeight) { h = MinHeight; clamped++; }
                result[j, i] = Mathf.Clamp01(h / size.y);
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Smart Farm valley slope");
        data.SetHeights(0, 0, result);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        float south = ValleyAnchorHeight + ValleySlope * (0f - ValleyAnchorZ);
        float foot = ValleyAnchorHeight + ValleySlope * (PlaneEndZ - ValleyAnchorZ);
        Debug.Log($"SmartFarm: valley slope applied. {ValleySlope * 100f:0.##} % : {south:0.0} m at the south edge, " +
                  $"{ValleyAnchorHeight:0.0} m at Z {ValleyAnchorZ}, {foot:0.0} m at Z {PlaneEndZ}." +
                  (clamped > 0 ? $" {clamped} points were raised to the minimum height {MinHeight} m: use a smaller slope or a higher anchor height." : ""));
    }

    [MenuItem("Smart Farm/6b Restore terrain from before the valley slope")]
    static void RestoreBeforeValleySlope()
    {
        var data = LoadData();
        if (data == null) return;
        string path = SnapshotFullPath();
        if (!File.Exists(path))
        {
            Debug.LogError($"SmartFarm: no snapshot found at {path}. Nothing to restore.");
            return;
        }
        int res = data.heightmapResolution;
        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length != res * res * sizeof(float))
        {
            Debug.LogError("SmartFarm: the snapshot does not match the terrain resolution.");
            return;
        }
        var heights = new float[res, res];
        System.Buffer.BlockCopy(raw, 0, heights, 0, raw.Length);
        Undo.RegisterCompleteObjectUndo(data, "Restore terrain before valley slope");
        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("SmartFarm: terrain restored to how it was before the first valley slope.");
    }

    // A capsule with a camera, placed at the hub. Drives with WASD (see SimplePlayer).
    [MenuItem("Smart Farm/5 Add player (capsule + camera)")]
    static void AddPlayer()
    {
        var old = GameObject.Find("Player");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var terrain = Terrain.activeTerrain;
        Vector3 origin = terrain != null ? terrain.transform.position : Vector3.zero;
        var spot = new Vector3(origin.x + 500f, 0f, origin.z + 330f);          // the hub
        spot.y = terrain != null ? origin.y + terrain.SampleHeight(spot) + 1.5f : 2f;

        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        Undo.RegisterCreatedObjectUndo(player, "Add Smart Farm player");
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.5f;
        player.transform.position = spot;

        // The camera becomes the scene's main camera, so switch the old one off.
        var oldCam = Camera.main;
        if (oldCam != null)
        {
            Undo.RecordObject(oldCam.gameObject, "Disable old main camera");
            oldCam.gameObject.SetActive(false);
        }

        var camGo = new GameObject("Player Camera");
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);              // eye height about 1.7 m above the feet
        camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";

        player.AddComponent<SimplePlayer>();
        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("SmartFarm: player added at the hub. Press Play. WASD moves, mouse looks, Esc frees the cursor. Save the scene with Ctrl+S.");
    }

    // The terrain data, layers and water material are assets and survive a closed project, but the Terrain and
    // "Lake Water" GameObjects live in the scene and are lost if the scene is never saved. This puts them back
    // without touching the heights or the paint, then saves the scene.
    [MenuItem("Smart Farm/4 Restore scene objects (terrain + lake)")]
    static void MenuRestore() { RestoreSceneObjects(); }

    static void RestoreSceneObjects()
    {
        var data = LoadData();
        if (data == null) return;

        if (Terrain.activeTerrain == null)
        {
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            terrainGo.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(terrainGo, "Restore Smart Farm terrain");
        }
        else if (Terrain.activeTerrain.terrainData != data)
        {
            Debug.LogWarning("SmartFarm: the active Terrain in the scene uses a different TerrainData than New Terrain.asset.");
        }

        AddLakeWater();

        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/SmartFarm.unity");
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"SmartFarm: the scene had never been saved, so it was saved as {path}.");
        }
        else
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"SmartFarm: scene saved ({scene.path}).");
        }
    }

    [MenuItem("Smart Farm/1 Build terrain heights")]
    static void MenuHeights() { BuildHeights(); }

    [MenuItem("Smart Farm/2 Build terrain layers")]
    static void MenuLayers() { BuildLayers(); }

    [MenuItem("Smart Farm/3 Add lake water")]
    static void MenuWater() { AddLakeWater(); }

    static TerrainData LoadData()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
        if (data == null) Debug.LogError($"SmartFarm: no TerrainData found at {TerrainAssetPath}");
        return data;
    }

    static byte[] ReadData(string path, int expectedBytes)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"SmartFarm: data file not found at {path}");
            return null;
        }
        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length != expectedBytes)
        {
            Debug.LogError($"SmartFarm: {path} is {raw.Length} bytes, expected {expectedBytes}");
            return null;
        }
        return raw;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------------------------------------------------------------- heights

    static bool BuildHeights()
    {
        var data = LoadData();
        if (data == null) return false;
        byte[] raw = ReadData(HeightDataPath, HeightRes * HeightRes * sizeof(float));
        if (raw == null) return false;

        // heights[y, x]: y runs along world Z, x along world X.
        var heights = new float[HeightRes, HeightRes];
        System.Buffer.BlockCopy(raw, 0, heights, 0, raw.Length);

        Undo.RegisterCompleteObjectUndo(data, "Build Smart Farm heights");
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(SizeXZ, SizeY, SizeXZ);
        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"SmartFarm: heights built ({HeightRes}x{HeightRes}, size {SizeXZ}x{SizeY}x{SizeXZ}).");
        return true;
    }

    // ----------------------------------------------------------------- layers

    static TerrainLayer CreateOrUpdateLayer(LayerDef def)
    {
        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "T_YFGM_" + def.Texture + "_d.tga");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir + "T_YFGM_" + def.Texture + "_n.tga");
        if (diffuse == null)
        {
            Debug.LogError($"SmartFarm: texture T_YFGM_{def.Texture}_d.tga not found in {TextureDir}");
            return null;
        }

        string path = LayersDir + "/" + def.Name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        bool isNew = layer == null;
        if (isNew) layer = new TerrainLayer();

        layer.diffuseTexture = diffuse;
        layer.normalMapTexture = normal;
        layer.tileSize = new Vector2(def.Tile, def.Tile);
        layer.metallic = 0f;
        layer.smoothness = 0f;

        if (isNew) AssetDatabase.CreateAsset(layer, path);
        else EditorUtility.SetDirty(layer);
        return layer;
    }

    static void Blur(float[,,] w, int passes)
    {
        int n = w.GetLength(0), layers = w.GetLength(2);
        var tmp = new float[n, n, layers];
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            for (int k = 0; k < layers; k++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int yy = y + dy, xx = x + dx;
                    if (yy < 0 || yy >= n || xx < 0 || xx >= n) continue;
                    sum += w[yy, xx, k];
                    count++;
                }
                tmp[y, x, k] = sum / count;
            }
            System.Array.Copy(tmp, w, tmp.Length);
        }
    }

    static bool BuildLayers()
    {
        var data = LoadData();
        if (data == null) return false;
        byte[] cls = ReadData(SplatDataPath, AlphaRes * AlphaRes);
        if (cls == null) return false;

        EnsureFolder(LayersDir);
        var layers = new TerrainLayer[LayerDefs.Length];
        for (int i = 0; i < LayerDefs.Length; i++)
        {
            layers[i] = CreateOrUpdateLayer(LayerDefs[i]);
            if (layers[i] == null) return false;
        }

        Undo.RegisterCompleteObjectUndo(data, "Build Smart Farm layers");
        data.terrainLayers = layers;
        data.alphamapResolution = AlphaRes;

        var weights = new float[AlphaRes, AlphaRes, layers.Length];
        for (int y = 0; y < AlphaRes; y++)
        for (int x = 0; x < AlphaRes; x++)
            weights[y, x, cls[y * AlphaRes + x]] = 1f;
        Blur(weights, 2);

        data.SetAlphamaps(0, 0, weights);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"SmartFarm: {layers.Length} terrain layers created in {LayersDir} and painted.");
        return true;
    }

    // ------------------------------------------------------------------ water

    static Material GetLakeMaterial()
    {
        EnsureFolder(MaterialsDir);
        string path = MaterialsDir + "/LakeWater.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", new Color(0.09f, 0.32f, 0.46f, 0.72f));
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.92f);
        mat.SetFloat("_Surface", 1f);                                           // transparent
        mat.SetFloat("_Blend", 0f);                                             // alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", 0f);                                              // visible from underwater too
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetShaderPassEnabled("ShadowCaster", false);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AddLakeWater()
    {
        const string objectName = "Lake Water";
        var terrain = Terrain.activeTerrain;
        Vector3 origin = terrain != null ? terrain.transform.position : Vector3.zero;

        var existing = GameObject.Find(objectName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        float[] outline = SmartFarmLakeData.Outline;
        int n = outline.Length / 2;
        var verts = new Vector3[n + 1];
        var uvs = new Vector2[n + 1];
        var normals = new Vector3[n + 1];
        verts[0] = new Vector3(SmartFarmLakeData.CenterX, SmartFarmLakeData.Level, SmartFarmLakeData.CenterZ);
        for (int i = 0; i < n; i++)
            verts[i + 1] = new Vector3(outline[2 * i], SmartFarmLakeData.Level, outline[2 * i + 1]);
        for (int i = 0; i <= n; i++)
        {
            uvs[i] = new Vector2(verts[i].x / 40f, verts[i].z / 40f);
            normals[i] = Vector3.up;
        }

        // Triangle fan around the centre; make sure the faces point up.
        var tris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % n + 1;
        }
        if (Vector3.Cross(verts[tris[1]] - verts[0], verts[tris[2]] - verts[0]).y < 0f)
        {
            for (int i = 0; i < n; i++)
            {
                int t = tris[i * 3 + 1];
                tris[i * 3 + 1] = tris[i * 3 + 2];
                tris[i * 3 + 2] = t;
            }
        }

        var mesh = new Mesh { name = "LakeSurface" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        var go = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(go, "Add Smart Farm lake water");
        go.transform.position = origin;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetLakeMaterial();
        mr.shadowCastingMode = ShadowCastingMode.Off;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"SmartFarm: lake water added at Y = {SmartFarmLakeData.Level} (terrain origin {origin}).");
    }
}
