using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PrehistoricFrontiers.Editor
{
    public static class GroundCoverSetup
    {
        const string Root = "Assets/PrehistoricFrontiers/";
        const string Art = Root + "Art/GroundCover/";
        static readonly List<Vector2> groves = new List<Vector2>();
        static readonly List<Bounds> obstacles = new List<Bounds>();
        static readonly List<Vector2> bases = new List<Vector2>();

        public static void ApplyAndBuild()
        {
            EditorSceneManager.OpenScene(Root + "Scenes/Battle.unity", OpenSceneMode.Single);
            Apply();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            FoundationBuilder.Build();
        }
        [MenuItem("Prehistoric Frontiers/Art/Apply Grass and Ground")]
        public static void Apply()
        {
            var ground = GameObject.Find("Ground");
            if (!ground) throw new InvalidOperationException("Open the Battle scene before applying ground cover.");
            var previous = GameObject.Find("Ground cover");
            if (previous) Object.DestroyImmediate(previous);
            groves.Clear(); obstacles.Clear(); bases.Clear();
            foreach (var building in Object.FindObjectsByType<BuildingHealth>())
                bases.Add(new Vector2(building.transform.position.x, building.transform.position.z));
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                var node = t.GetComponent<ResourceNode>();
                if (t.name == "Tree" || node && node.kind == ResourceKind.Wood)
                    groves.Add(new Vector2(t.position.x, t.position.z));
                if (t.name == "Meadow patch") t.gameObject.SetActive(false);
            }
            foreach (var collider in Object.FindObjectsByType<Collider>())
                if (collider.gameObject.layer == 9 || collider.gameObject.layer == 12) obstacles.Add(collider.bounds);
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float wx = (x + .5f) / size * 50 - 25, wz = (y + .5f) / size * 50 - 25;
                float cloud = Mathf.PerlinNoise(wx * .32f + 40, wz * .32f + 40);
                float grain = Mathf.PerlinNoise(wx * 9 + 300, wz * 9 + 300) - .5f;
                var dirt = Color.Lerp(new Color(.43f, .28f, .24f), new Color(.54f, .37f, .30f), cloud);
                var meadow = Color.Lerp(new Color(.24f, .31f, .12f), new Color(.36f, .40f, .16f), cloud);
                pixels[y * size + x] = Color.Lerp(dirt, meadow, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.16f, .74f, Coverage(wx, wz)))) * (1 + grain * .09f);
            }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(Art + "GroundMap.png", texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Art + "GroundMap.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "GroundMap.png");
            importer.isReadable = true; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "GroundMap.png");
            var groundMaterial = Material("Ground", "Prehistoric Frontiers/Ground Surface");
            groundMaterial.mainTexture = map; EditorUtility.SetDirty(groundMaterial);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            var grassMaterial = Material("Grass", "Prehistoric Frontiers/Meadow Grass");
            var root = new GameObject("Ground cover"); root.layer = 11;
            var cover = root.AddComponent<GroundCover>(); cover.groundMap = map; cover.grassMaterial = grassMaterial;
            var random = new System.Random(9241);
            int totalBlades = 0;
            for (int cz = 0; cz < 4; cz++) for (int cx = 0; cx < 4; cx++)
            {
                var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                float left = -24.5f + cx * 12.25f, bottom = -24.5f + cz * 12.25f;
                for (float z = bottom; z < bottom + 12.25f; z += .24f) for (float x = left; x < left + 12.25f; x += .24f)
                {
                    float px = x + Range(random, -.12f, .12f), pz = z + Range(random, -.12f, .12f);
                    float coverStrength = Coverage(px, pz);
                    if (random.NextDouble() > coverStrength * .9f || Blocked(px, pz)) continue;
                    var center = new Vector3(px, .018f, pz);
                    float hue = Mathf.Clamp01((Mathf.PerlinNoise(px * .28f + 14, pz * .28f + 7) - .2f) * 1.5f + Range(random, -.12f, .12f));
                    var low = Color.Lerp(new Color(.19f, .29f, .07f), new Color(.32f, .34f, .09f), hue);
                    var tip = Color.Lerp(new Color(.40f, .55f, .13f), new Color(.64f, .61f, .22f), hue);
                    for (int blade = 0; blade < 7; blade++)
                    {
                        float angle = Range(random, 0, Mathf.PI * 2), height = Range(random, .30f, .72f), width = Range(random, .025f, .052f);
                        var side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * width;
                        var bend = new Vector3(-side.z, 0, side.x).normalized * Range(random, .07f, .20f);
                        var foot = center + new Vector3(Range(random, -.11f, .11f), 0, Range(random, -.11f, .11f));
                        int index = vertices.Count;
                        vertices.Add(foot - side); vertices.Add(foot + side);
                        vertices.Add(foot + Vector3.up * height * .58f + bend * .35f - side * .55f);
                        vertices.Add(foot + Vector3.up * height * .58f + bend * .35f + side * .55f);
                        vertices.Add(foot + Vector3.up * height + bend);
                        colors.Add(low); colors.Add(low); colors.Add(Color.Lerp(low, tip, .65f)); colors.Add(Color.Lerp(low, tip, .65f)); colors.Add(tip);
                        triangles.AddRange(new[] { index, index + 2, index + 1, index + 1, index + 2, index + 3, index + 2, index + 4, index + 3 });
                        totalBlades++;
                    }
                }
                var mesh = new Mesh { name = "Grass patch " + cx + " " + cz, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                var normals = new Vector3[vertices.Count];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
                mesh.normals = normals; mesh.RecalculateBounds();
                string path = Art + "Grass_" + cx + "_" + cz + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                else { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                var chunk = new GameObject(saved.name); chunk.layer = 11; chunk.transform.SetParent(root.transform, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = saved;
                var renderer = chunk.AddComponent<MeshRenderer>(); renderer.sharedMaterial = grassMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("GROUND_COVER_READY / blades: " + totalBlades);
        }
        static float Coverage(float x, float z)
        {
            var point = new Vector2(x, z);
            float grove = 0;
            foreach (var tree in groves) grove = Mathf.Max(grove, 1 - Vector2.Distance(tree, point) / 4);
            float edge = Mathf.InverseLerp(17, 24, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
            float density = Mathf.Clamp01((Mathf.PerlinNoise(x * .16f + 19, z * .16f + 29) - .39f) * 2.2f + grove * .55f + edge * .3f);
            foreach (var building in bases) density *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.7f, 4.5f, Vector2.Distance(point, building)));
            return density;
        }
        static bool Blocked(float x, float z)
        {
            foreach (var bounds in obstacles)
                if (x > bounds.min.x - .15f && x < bounds.max.x + .15f && z > bounds.min.z - .15f && z < bounds.max.z + .15f) return true;
            return false;
        }
        static float Range(System.Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();
        static Material Material(string name, string shader)
        {
            string path = Art + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result) { result = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(result, path); }
            return result;
        }
    }
}
