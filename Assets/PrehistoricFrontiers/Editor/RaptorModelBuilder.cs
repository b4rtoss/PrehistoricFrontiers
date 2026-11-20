using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrehistoricFrontiers.Editor
{
    public static class RaptorModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Raptor/";
        const string Output = "../";
        enum ColorPart { Skin, Belly, Back, Claw, Eye, Pupil, Team, Teeth }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[8];
            public Shape() { for (int i = 0; i < faces.Length; i++) faces[i] = new List<int>(); }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, ColorPart material)
            {
                var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < .00000001f) return;
                n.Normalize(); int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); normals.Add(n); normals.Add(n); normals.Add(n);
                faces[(int)material].Add(index); faces[(int)material].Add(index + 1); faces[(int)material].Add(index + 2);
            }
            public void Ellipsoid(Vector3 center, Vector3 radius, ColorPart material, bool belly = false, int sides = 10, int rings = 5)
            {
                Vector3 Point(int row, int side)
                {
                    float latitude = -Mathf.PI / 2 + Mathf.PI * row / rings, angle = Mathf.PI * 2 * side / sides;
                    return center + Vector3.Scale(new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(angle)), radius);
                }
                for (int row = 0; row < rings; row++) for (int side = 0; side < sides; side++)
                {
                    var a = Point(row, side); var b = Point(row + 1, side); var c = Point(row + 1, side + 1); var d = Point(row, side + 1);
                    var mat = belly && row < rings / 2 ? ColorPart.Belly : material;
                    Triangle(a, b, c, mat); Triangle(a, c, d, mat);
                }
            }
            public void Tube(Vector3[] centers, float[] radii, ColorPart material, int sides = 8, bool bands = false)
            {
                var points = new Vector3[centers.Length, sides];
                for (int i = 0; i < centers.Length; i++)
                {
                    var tangent = (centers[Mathf.Min(i + 1, centers.Length - 1)] - centers[Mathf.Max(0, i - 1)]).normalized;
                    var right = Vector3.Cross(Mathf.Abs(tangent.y) > .9f ? Vector3.forward : Vector3.up, tangent).normalized;
                    var up = Vector3.Cross(tangent, right).normalized;
                    for (int j = 0; j < sides; j++) { float angle = j * Mathf.PI * 2 / sides; points[i, j] = centers[i] + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radii[i]; }
                }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.Back : material;
                    Triangle(points[i, j], points[i, next], points[i + 1, next], mat);
                    Triangle(points[i, j], points[i + 1, next], points[i + 1, j], mat);
                }
                for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides, last = centers.Length - 1;
                    Triangle(centers[0], points[0, next], points[0, j], material);
                    Triangle(centers[last], points[last, j], points[last, next], material);
                }
            }
            // Cross sections run from negative to positive Z. Shared rings keep the silhouette continuous.
            public void Loft(Vector3[] centers, Vector2[] radii, bool patterned = false, int sides = 10, bool crest = false)
            {
                Vector3 Point(int i, int j) { float a = j * Mathf.PI * 2 / sides + (patterned && i % 2 == 1 ? .085f : 0); return centers[i] + new Vector3(Mathf.Cos(a) * radii[i].x, Mathf.Sin(a) * radii[i].y, 0); }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    float angle = (j + .5f) * Mathf.PI * 2 / sides;
                    var mat = Mathf.Sin(angle) < -.25f ? ColorPart.Belly : ColorPart.Skin;
                    if (patterned && Mathf.Sin(angle) > .32f && (i * 7 + j * 3) % 9 < 3) mat = ColorPart.Back;
                    if (crest && j == 2 && i >= 1) mat = ColorPart.Team;
                    var a = Point(i, j); var b = Point(i, j + 1); var c = Point(i + 1, j + 1); var d = Point(i + 1, j);
                    Triangle(a, b, c, mat); Triangle(a, c, d, patterned && mat == ColorPart.Back && i % 2 == 0 ? ColorPart.Skin : mat);
                }
                for (int j = 0; j < sides; j++)
                {
                    Triangle(centers[0], Point(0, j + 1), Point(0, j), ColorPart.Skin);
                    int n = centers.Length - 1; Triangle(centers[n], Point(n, j), Point(n, j + 1), ColorPart.Skin);
                }
            }
            public void Neck(Vector3[] centers, Vector2[] radii)
            {
                const int sides = 10;
                Vector3 Point(int i, int j) { float a = j * Mathf.PI * 2 / sides; return centers[i] + new Vector3(Mathf.Cos(a) * radii[i].x, 0, Mathf.Sin(a) * radii[i].y); }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    var mat = Mathf.Sin((j + .5f) * Mathf.PI * 2 / sides) > .25f ? ColorPart.Belly : ColorPart.Skin;
                    var a = Point(i,j); var b = Point(i,j+1); var c = Point(i+1,j+1); var d = Point(i+1,j);
                    Triangle(a,c,b,mat); Triangle(a,d,c,mat);
                }
            }
            // Surface-conforming polygons: dark annuli with skin-coloured centres, never floating decals.
            public void Rosette(Func<float, float, Vector3> surface, float u, float v, float ru, float rv)
            {
                const int n = 7;
                for (int j = 0; j < n; j++)
                {
                    float a = j * Mathf.PI * 2 / n, b = (j+1) * Mathf.PI * 2 / n;
                    Vector3 P(float angle, float scale) => surface(u + Mathf.Cos(angle)*ru*scale, v + Mathf.Sin(angle)*rv*scale);
                    var p = P(a,1); var q = P(b,1); var r = P(b,.43f); var t = P(a,.43f);
                    // Both faces allow the same curved patch helper on either flank and on the back.
                    Triangle(p,q,r,ColorPart.Back); Triangle(p,r,t,ColorPart.Back);
                    Triangle(r,q,p,ColorPart.Back); Triangle(t,r,p,ColorPart.Back);
                }
            }
            public void Spike(Vector3 a, Vector3 b, float radius, ColorPart material) => Tube(new[] { a, b }, new[] { radius, 0f }, material, 5);
            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.subMeshCount = faces.Length;
                for (int i = 0; i < faces.Length; i++) mesh.SetTriangles(faces[i], i);
                mesh.RecalculateBounds(); string path = Folder + name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { existing.Clear(); existing.SetVertices(vertices); existing.SetNormals(normals); existing.subMeshCount = faces.Length; for(int i=0;i<faces.Length;i++) existing.SetTriangles(faces[i],i); existing.RecalculateBounds(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
                if (AssetDatabase.LoadMainAssetAtPath(path)) throw new InvalidOperationException("Wrong asset type: " + path);
                AssetDatabase.CreateAsset(mesh, path); return mesh;
            }
        }
        static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var bone = new GameObject(name).transform; bone.SetParent(parent, false); bone.localPosition = position; bone.gameObject.layer = 10; return bone;
        }
        static void Render(Transform bone, Shape shape, string name, Material[] materials)
        {
            bone.gameObject.AddComponent<MeshFilter>().sharedMesh = shape.Save(name);
            bone.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        static Material[] Palette(bool enemy)
        {
            string[] colors = enemy ? new[] { "#821E16", "#9C6755", "#301B18", "#201D1B", "#F8CC4B", "#131C22", "#F03D13", "#F3E3BE" }
                : new[] { "#626154", "#A3A28E", "#302F25", "#24251F", "#F8CC4B", "#131C22", "#E76717", "#F3E3BE" };
            var result = new Material[colors.Length];
            for (int i = 0; i < result.Length; i++)
            {
                string name = (enemy ? "Enemy_" : "Player_") + ((ColorPart)i).ToString(); string path = Folder + name + ".mat";
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset && !(asset is Material)) throw new InvalidOperationException("Wrong asset type: " + path);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
                ColorUtility.TryParseHtmlString(colors[i], out var color); mat.color = color; mat.SetFloat("_Glossiness", .12f); mat.enableInstancing = true;
                EditorUtility.SetDirty(mat); result[i] = mat;
            }
            return result;
        }
        static GameObject Model(Material[] materials)
        {
            var root = new GameObject("RaptorModel"); root.layer = 10;
            var motion = root.AddComponent<RaptorMotion>();
            var torso = Bone("Torso", root.transform, Vector3.zero); motion.torso = torso;
            var body = new Shape();
            body.Loft(new[] { new Vector3(0,1.01f,-.64f), new Vector3(0,1.02f,-.39f), new Vector3(0,1.01f,-.10f), new Vector3(0,1.03f,.17f), new Vector3(0,1.12f,.35f) },
                new[] { new Vector2(.14f,.16f), new Vector2(.27f,.25f), new Vector2(.30f,.29f), new Vector2(.25f,.27f), new Vector2(.15f,.18f) }, true, 12);
            body.Neck(new[] { new Vector3(0,1.04f,.26f), new Vector3(0,1.20f,.34f), new Vector3(0,1.36f,.33f), new Vector3(0,1.51f,.40f), new Vector3(0,1.57f,.46f) },
                new[] { new Vector2(.20f,.20f), new Vector2(.16f,.18f), new Vector2(.12f,.14f), new Vector2(.12f,.14f), new Vector2(.13f,.14f) });
            foreach (int side in new[] { -1, 1 })
            {
                body.Tube(new[] { new Vector3(side*.23f,1.13f,.22f), new Vector3(side*.32f,.93f,.13f), new Vector3(side*.28f,.85f,.32f) }, new[] { .075f,.053f,.036f }, ColorPart.Skin,7);
                for (int finger = 0; finger < 3; finger++)
                {
                    float x = side*(.245f+finger*.039f), z = .32f + (finger==1 ? .028f : 0);
                    body.Tube(new[] { new Vector3(x,.85f,z), new Vector3(x,.74f,z+.055f), new Vector3(x,.70f,z+.085f) },new[] { .022f,.016f,.012f },ColorPart.Skin,5);
                    body.Tube(new[] { new Vector3(x,.72f,z+.07f), new Vector3(x,.68f,z+.11f),new Vector3(x,.70f,z+.15f) },new[] { .017f,.012f,0f },ColorPart.Claw,5);
                }
            }
            Render(torso, body, "Raptor_Torso", materials);
            var head = Bone("Head", torso, new Vector3(0,1.46f,.42f)); motion.head = head;
            var skull = new Shape();
            skull.Loft(new[] { new Vector3(0,.10f,-.10f), new Vector3(0,.12f,.04f),new Vector3(0,.10f,.22f),new Vector3(0,.055f,.40f),new Vector3(0,.018f,.64f),new Vector3(0,.01f,.70f) },
                new[] {new Vector2(.09f,.11f),new Vector2(.175f,.16f),new Vector2(.18f,.14f),new Vector2(.145f,.09f),new Vector2(.12f,.06f),new Vector2(.08f,.045f)},false,10,true);
            foreach (int side in new[] {-1,1})
            {
                skull.Ellipsoid(new Vector3(side*.174f,.145f,.18f),new Vector3(.018f,.050f,.060f),ColorPart.Pupil,false,8,4);
                skull.Ellipsoid(new Vector3(side*.188f,.15f,.19f),new Vector3(.009f,.032f,.035f),ColorPart.Eye,false,8,4);
                skull.Ellipsoid(new Vector3(side*.197f,.15f,.20f),new Vector3(.005f,.025f,.010f),ColorPart.Pupil,false,6,3);
                skull.Tube(new[] {new Vector3(side*.165f,.207f,.10f),new Vector3(side*.187f,.201f,.23f),new Vector3(side*.14f,.13f,.43f)},new[] {.030f,.026f,.009f},ColorPart.Team,5);
                skull.Ellipsoid(new Vector3(side*.116f,.045f,.61f),new Vector3(.009f,.012f,.022f),ColorPart.Pupil,false,6,3);
                for(int t=0;t<7;t++)
                {
                    float z=.27f+t*.057f, x=side*Mathf.Lerp(.162f,.102f,t/6f);
                    skull.Spike(new Vector3(x,-.018f,z),new Vector3(x*.98f,-.055f,z+.01f),.012f,ColorPart.Teeth);
                }
                // Orange/red V-shaped brow and narrow nasal stripe.
            }
            for(int i=0;i<9;i++)
            {
                float fan=(i-4)/4f;
                skull.Spike(new Vector3(fan*.095f,.23f,.035f),new Vector3(fan*.25f,.31f+(.11f*(1-Mathf.Abs(fan))),-.30f+.07f*Mathf.Abs(fan)),.027f,ColorPart.Claw);
            }
            Render(head,skull,"Raptor_Head",materials);
            var jaw=Bone("Jaw",head,new Vector3(0,-.036f,.075f)); motion.jaw=jaw;
            var mandible=new Shape();
            mandible.Loft(new[] {new Vector3(0,-.005f,.0f),new Vector3(0,-.025f,.16f),new Vector3(0,-.030f,.38f),new Vector3(0,-.024f,.58f),new Vector3(0,-.015f,.625f)},
                new[] {new Vector2(.105f,.050f),new Vector2(.152f,.047f),new Vector2(.132f,.037f),new Vector2(.101f,.025f),new Vector2(.065f,.016f)},false,10);
            Render(jaw,mandible,"Raptor_Jaw",materials);
            var tail=Bone("Tail",torso,new Vector3(0,1.03f,-.55f)); motion.tail=tail;
            var tailMesh=new Shape();
            var tailCenters=new[] {new Vector3(0,.018f,-1.94f),new Vector3(0,.09f,-1.67f),new Vector3(0,.09f,-1.36f),new Vector3(0,.05f,-1.03f),new Vector3(0,.015f,-.66f),new Vector3(0,0,-.30f),Vector3.zero};
            var tailRadii=new[] {Vector2.zero,new Vector2(.024f,.027f),new Vector2(.045f,.041f),new Vector2(.065f,.050f),new Vector2(.092f,.075f),new Vector2(.125f,.12f),new Vector2(.17f,.18f)};
            tailMesh.Loft(tailCenters,tailRadii,false,10);
            Vector3 TailSurface(float z,float angle)
            {
                int i=0; while(i<tailCenters.Length-2 && z>tailCenters[i+1].z) i++;
                float t=Mathf.InverseLerp(tailCenters[i].z,tailCenters[i+1].z,z);
                var c=Vector3.Lerp(tailCenters[i],tailCenters[i+1],t);var r=Vector2.Lerp(tailRadii[i],tailRadii[i+1],t);
                // Intersect the polygonal cross section rather than the circumscribed ellipse.
                float sector=2*Mathf.PI/10, a=Mathf.Repeat(angle,sector)-sector*.5f;
                float polygon=Mathf.Cos(sector*.5f)/Mathf.Cos(a);
                return c+new Vector3(Mathf.Cos(angle)*(r.x*polygon+.006f),Mathf.Sin(angle)*(r.y*polygon+.006f),0);
            }
            for(int i=0;i<7;i++)
            {
                float z=-.18f-i*.235f, radius=.072f-i*.0065f;
                tailMesh.Rosette(TailSurface,z,.15f,radius,.52f);
                tailMesh.Rosette(TailSurface,z,Mathf.PI-.15f,radius,.52f);
                tailMesh.Rosette(TailSurface,z,Mathf.PI*.5f,radius*.8f,.51f);
            }
            Render(tail,tailMesh,"Raptor_Tail",materials);
            foreach (int side in new[] { -1, 1 })
            {
                bool left = side < 0;
                var hip = Bone(left ? "LeftHip" : "RightHip", root.transform, new Vector3(side * .255f, .85f, -.16f));
                var thigh = new Shape(); thigh.Ellipsoid(new Vector3(side*.025f,-.08f,.025f),new Vector3(.16f,.24f,.225f),ColorPart.Skin,false,9,5); thigh.Tube(new[] { Vector3.zero, new Vector3(side * .035f, -.23f, .14f), new Vector3(side * .025f, -.43f, .21f) }, new[] { .145f, .115f, .074f }, ColorPart.Skin, 9);
                Render(hip, thigh, left ? "Raptor_LeftThigh" : "Raptor_RightThigh", materials);
                var knee = Bone("Knee", hip, new Vector3(side * .025f, -.43f, .21f));
                var shin = new Shape(); shin.Tube(new[] { Vector3.zero, new Vector3(0, -.25f, -.20f), new Vector3(0, -.34f, -.24f) }, new[] { .076f, .052f, .045f }, ColorPart.Skin, 7);
                Render(knee, shin, left ? "Raptor_LeftShin" : "Raptor_RightShin", materials);
                var foot = Bone("Foot", knee, new Vector3(0, -.34f, -.24f));
                var toes = new Shape(); toes.Tube(new[] { Vector3.zero, new Vector3(0, -.045f, .16f), new Vector3(0, -.055f, .32f) }, new[] { .052f, .055f, .032f }, ColorPart.Skin, 7);
                for (int toe = 0; toe < 2; toe++)
                {
                    float x = side * (.025f + toe * .07f);
                    toes.Tube(new[] { new Vector3(x, -.035f, .20f), new Vector3(x, -.055f, .37f), new Vector3(x, -.035f, .45f) }, new[] { .03f, .023f, 0f }, ColorPart.Claw, 6);
                }
                toes.Tube(new[] { new Vector3(-side * .09f, -.015f, .18f), new Vector3(-side * .10f, .04f, .32f), new Vector3(-side * .10f, .15f, .35f), new Vector3(-side * .10f, .20f, .29f) }, new[] { .043f, .038f, .022f, 0f }, ColorPart.Claw, 7);
                Render(foot, toes, left ? "Raptor_LeftFoot" : "Raptor_RightFoot", materials);
                if (left) { motion.leftHip = hip; motion.leftKnee = knee; motion.leftFoot = foot; }
                else { motion.rightHip = hip; motion.rightKnee = knee; motion.rightFoot = foot; }
            }
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Raptor Model And Previews...")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RaptorModel.prefab")
                && !EditorUtility.DisplayDialog("Обновить модель раптора?", "Сгенерированные меши, материалы и модельные префабы в Art/Models/Raptor будут обновлены. Ручные изменения в них будут заменены; GUID существующих assets сохранятся. Игровая сцена этой командой не пересоздаётся.", "Обновить модель", "Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (bool enemy in new[] { false, true })
            {
                var root = Model(Palette(enemy));
                PrefabUtility.SaveAsPrefabAsset(root, Folder + (enemy ? "RaptorEnemyModel.prefab" : "RaptorModel.prefab"));
                ExportObj(root, Path.GetFullPath(Output + (enemy ? "Raptor-enemy-lowpoly.obj" : "Raptor-lowpoly.obj")));
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Preview();
        }
        public static void CreateAndApply() { Create(); ApplyToGame(); }
        [MenuItem("Prehistoric Frontiers/Art/Apply Raptor Model To Battle")]
        public static void ApplyToGame()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RaptorModel.prefab");
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RaptorEnemyModel.prefab");
            if (!player || !enemy) throw new InvalidOperationException("Create the raptor art assets before applying them.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "Raptor", "Enemy_Raptor" })
            {
                string path = "Assets/PrehistoricFrontiers/Prefabs/Units/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Install(root.GetComponent<RTSUnit>(), name == "Raptor" ? player : enemy); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity", OpenSceneMode.Single);
            var stats = AssetDatabase.LoadAssetAtPath<UnitStats>("Assets/PrehistoricFrontiers/Data/RaptorStats.asset");
            foreach (var unit in UnityEngine.Object.FindObjectsByType<RTSUnit>())
                if (unit.stats == stats) Install(unit, unit.team == UnitTeam.Player ? player : enemy);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("RAPTOR_ART_APPLIED_WITH_EXISTING_COLLIDERS");
        }
        static void Install(RTSUnit unit, GameObject model)
        {
            var visual = unit.transform.Find("Visual");
            if (!visual || !visual.GetComponent<CapsuleCollider>()) throw new InvalidOperationException("Existing selection collider missing on " + unit.name);
            visual.GetComponent<Renderer>().enabled = false;
            var existing = unit.GetComponentInChildren<RaptorMotion>(true);
            if (existing)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == model) return;
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            var art = (GameObject)PrefabUtility.InstantiatePrefab(model, unit.transform);
            art.transform.localPosition = Vector3.zero; art.transform.localRotation = Quaternion.identity; art.transform.localScale = Vector3.one;
        }
        static void ExportObj(GameObject root, string path)
        {
            var obj = new StringBuilder("# Prehistoric Frontiers stylized raptor\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
            var mtl = new StringBuilder(); var used = new HashSet<Material>(); int offset = 1;
            string F(float x) => x.ToString("0.#####", CultureInfo.InvariantCulture);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh; obj.AppendLine("o " + filter.name);
                foreach (var vertex in mesh.vertices) { var v = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)); obj.AppendLine("v " + F(-v.x) + " " + F(v.y) + " " + F(v.z)); }
                foreach (var normal in mesh.normals) { var n = root.transform.InverseTransformDirection(filter.transform.TransformDirection(normal)).normalized; obj.AppendLine("vn " + F(-n.x) + " " + F(n.y) + " " + F(n.z)); }
                var materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var indices = mesh.GetTriangles(s); if (indices.Length == 0) continue;
                    var mat = materials[s]; obj.AppendLine("usemtl " + mat.name);
                    if (used.Add(mat)) mtl.AppendLine("newmtl " + mat.name + "\nKd " + F(mat.color.r) + " " + F(mat.color.g) + " " + F(mat.color.b) + "\nKa 0.05 0.05 0.05\nKs 0 0 0\nillum 1\n");
                    for (int i = 0; i < indices.Length; i += 3)
                    { int a = indices[i] + offset, b = indices[i + 2] + offset, c = indices[i + 1] + offset; obj.AppendLine("f " + a + "//" + a + " " + b + "//" + b + " " + c + "//" + c); }
                }
                offset += mesh.vertexCount;
            }
            File.WriteAllText(path, obj.ToString()); File.WriteAllText(Path.ChangeExtension(path, ".mtl"), mtl.ToString());
        }
        static void Preview()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(bool enemy in new[] {false,true})
                {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "RaptorEnemyModel.prefab" : "RaptorModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 1.55f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .105f, .13f); camera.nearClipPlane = .05f; camera.farClipPlane = 40;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager.MoveGameObjectToScene(floor, scene); floor.transform.localScale = Vector3.one * 4;
                var ground = new Material(Shader.Find("Standard")) { color = new Color(.17f, .22f, .25f) }; ground.SetFloat("_Glossiness", 0); floor.GetComponent<Renderer>().sharedMaterial = ground;
                for (int i = 0; i < 2; i++)
                {
                    var lightObject = new GameObject("Studio light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
                    var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = i == 0 ? 1 : .35f;
                    light.color = i == 0 ? new Color(1, .94f, .82f) : new Color(.7f, .85f, 1);
                    light.transform.rotation = Quaternion.Euler(i == 0 ? 45 : 30, i == 0 ? -35 : 145, 0); light.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
                }
                var positions = new[] { new Vector3(3.5f, 2.2f, 4), new Vector3(4, .88f, -.67f), new Vector3(3, 4.5f, -5), new Vector3(0,.90f,5), new Vector3(0,.90f,-6) };
                var names = new[] { "Раптор-превью.png", "Раптор-профиль.png", "Раптор-вид-сверху.png", "Раптор-спереди.png", "Раптор-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0, .88f, -.67f));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Раптор-", "Раптор-враг-") : names[i])), snapshot.EncodeToPNG());
                    camera.targetTexture = null; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(snapshot);
                }
                UnityEngine.Object.DestroyImmediate(ground);
                UnityEngine.Object.DestroyImmediate(floor); UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(model);
                foreach(var sceneRoot in scene.GetRootGameObjects()) if(sceneRoot.GetComponent<Light>()) UnityEngine.Object.DestroyImmediate(sceneRoot);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
