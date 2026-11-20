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
    public static class TriceratopsModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Triceratops/";
        const string Output = "../";
        enum ColorPart { Skin, Belly, Patch, Horn, Eye, Pupil, Accent, Beak, Rim, Ray }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[10];
            public Shape() { for (int i = 0; i < faces.Length; i++) faces[i] = new List<int>(); }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, ColorPart material)
            {
                var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < .00000001f) return;
                n.Normalize(); int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); normals.Add(n); normals.Add(n); normals.Add(n);
                faces[(int)material].Add(index); faces[(int)material].Add(index + 1); faces[(int)material].Add(index + 2);
            }
            public void Ellipsoid(Vector3 center, Vector3 radius, ColorPart material, bool belly = false, int sides = 10, int rings = 5, bool spots = false)
            {
                Vector3 Point(int row, int side)
                {
                    float latitude = -Mathf.PI / 2 + Mathf.PI * row / rings, angle = Mathf.PI * 2 * side / sides + (row%2==0 ? .07f : -.04f);
                    return center + Vector3.Scale(new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(angle)), radius);
                }
                for (int row = 0; row < rings; row++) for (int side = 0; side < sides; side++)
                {
                    var a = Point(row, side); var b = Point(row + 1, side); var c = Point(row + 1, side + 1); var d = Point(row, side + 1);
                    var mat = belly && row < rings / 2 ? ColorPart.Belly : material;
                    Triangle(a, b, c, mat); Triangle(a, c, d, mat);
                    if(spots && row>=rings/2 && row<rings-1)
                    {
                        if((row*13+side*7)%5==0) Spot(a,b,c,row+side);
                        if((row*5+side*11)%7<2) Spot(a,c,d,row-side);
                    }
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
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.Patch : material;
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
                    if (patterned && Mathf.Sin(angle) > .32f && (i * 7 + j * 3) % 9 < 3) mat = ColorPart.Patch;
                    if (crest && mat == ColorPart.Skin) mat = ColorPart.Accent;
                    var a = Point(i, j); var b = Point(i, j + 1); var c = Point(i + 1, j + 1); var d = Point(i + 1, j);
                    Triangle(a, b, c, mat); Triangle(a, c, d, patterned && mat == ColorPart.Patch && i % 2 == 0 ? ColorPart.Skin : mat);
                }
                for (int j = 0; j < sides; j++)
                {
                    Triangle(centers[0], Point(0, j + 1), Point(0, j), ColorPart.Skin);
                    int n = centers.Length - 1; Triangle(centers[n], Point(n, j), Point(n, j + 1), ColorPart.Skin);
                }
            }
            void Spot(Vector3 a, Vector3 b, Vector3 c, int seed)
            {
                var n=Vector3.Cross(b-a,c-a).normalized;
                float radius=Vector3.Cross(b-a,c-a).magnitude/((b-a).magnitude+(c-b).magnitude+(a-c).magnitude)*.68f;
                var center=(a+b+c)/3+n*.002f;
                var u=(b-a).normalized; var v=Vector3.Cross(n,u);
                for(int j=0;j<6;j++)
                {
                    float angle=(j+seed*.17f)*Mathf.PI/3, next=angle+Mathf.PI/3;
                    Triangle(center,center+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*radius,
                        center+(u*Mathf.Cos(next)+v*Mathf.Sin(next))*radius,ColorPart.Patch);
                }
            }
            public void Frill()
            {
                const int sides=28;
                float[] rings={0,.30f,.71f,.91f,1f};
                Vector3 P(int ring,int j,bool back=false)
                {
                    float angle=j*Mathf.PI*2/sides;
                    float r=rings[ring]*(ring==4 && j%2==1 ? .91f:1);
                    float y=.32f+Mathf.Sin(angle)*r*.77f;
                    return new Vector3(Mathf.Cos(angle)*r*.78f,y,-.22f-y*.29f+(back ? -.065f : (1-r)*.08f));
                }
                for(int i=0;i<rings.Length-1;i++) for(int j=0;j<sides;j++)
                {
                    var mat=i==3 ? ColorPart.Rim : (j%2==0 ? ColorPart.Ray : ColorPart.Accent);
                    var a=P(i,j);var b=P(i+1,j);var c=P(i+1,j+1);var d=P(i,j+1);
                    Triangle(a,b,c,mat);Triangle(a,c,d,mat);
                    a=P(i,j,true);b=P(i+1,j,true);c=P(i+1,j+1,true);d=P(i,j+1,true);
                    Triangle(a,c,b,ColorPart.Skin);Triangle(a,d,c,ColorPart.Skin);
                }
                for(int j=0;j<sides;j++)
                {
                    Triangle(P(4,j),P(4,j,true),P(4,j+1,true),ColorPart.Rim);
                    Triangle(P(4,j),P(4,j+1,true),P(4,j+1),ColorPart.Rim);
                }
                foreach(var center in new[] {new Vector2(0,.90f),new Vector2(-.29f,.77f),new Vector2(.29f,.77f),new Vector2(-.52f,.49f),new Vector2(.52f,.49f)})
                {
                    Vector3 Surface(Vector2 p)
                    {
                        float r=new Vector2(p.x/.78f,(p.y-.32f)/.77f).magnitude;
                        return new Vector3(p.x,p.y,-.22f-p.y*.29f+(1-r)*.08f+.006f);
                    }
                    for(int j=0;j<6;j++)
                    {
                        float a=j*Mathf.PI/3,b=(j+1)*Mathf.PI/3;
                        Triangle(Surface(center),Surface(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.029f),Surface(center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.029f),ColorPart.Patch);
                    }
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
            string[] colors = enemy ? new[] { "#4C3022", "#786550", "#AD8C6B", "#706155", "#E8B332", "#171713", "#A83B0B", "#302C25", "#48240F", "#E28009" }
                : new[] { "#354353", "#6C7780", "#B49D81", "#776D61", "#E8B332", "#171713", "#445466", "#383933", "#8C8171", "#536174" };
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
            var root=new GameObject("TriceratopsModel");root.layer=10;
            var motion=root.AddComponent<TriceratopsMotion>();
            var torso=Bone("Torso",root.transform,Vector3.zero);motion.torso=torso;
            var body=new Shape();
            body.Ellipsoid(new Vector3(0,1.31f,-.13f),new Vector3(.70f,.66f,1.02f),ColorPart.Skin,true,16,9,true);
            body.Ellipsoid(new Vector3(0,1.18f,.58f),new Vector3(.52f,.46f,.51f),ColorPart.Skin,true,12,6);
            Render(torso,body,"Triceratops_Torso",materials);
            var head=Bone("Head",torso,new Vector3(0,1.27f,.87f));motion.head=head;
            var face=new Shape();
            face.Frill();
            face.Loft(new[] {new Vector3(0,.02f,-.15f),new Vector3(0,.03f,.12f),new Vector3(0,-.06f,.39f),new Vector3(0,-.16f,.70f),new Vector3(0,-.21f,.94f)},
                new[] {new Vector2(.30f,.31f),new Vector2(.39f,.36f),new Vector2(.29f,.28f),new Vector2(.21f,.19f),new Vector2(.135f,.13f)},false,12,true);
            face.Ellipsoid(new Vector3(0,-.28f,.47f),new Vector3(.27f,.10f,.48f),ColorPart.Belly,false,10,4);
            face.Tube(new[] {new Vector3(0,-.19f,.88f),new Vector3(0,-.27f,1.04f),new Vector3(0,-.42f,1.08f),new Vector3(0,-.47f,1.02f)},new[] {.145f,.12f,.065f,0f},ColorPart.Beak,8);
            foreach(int side in new[] {-1,1})
            {
                face.Tube(new[] {new Vector3(side*.29f,.28f,.14f),new Vector3(side*.38f,.56f,.34f),new Vector3(side*.49f,.83f,.61f),new Vector3(side*.58f,1.02f,.86f)},
                    new[] {.15f,.105f,.052f,0f},ColorPart.Horn,8);
                face.Ellipsoid(new Vector3(side*.309f,.06f,.35f),new Vector3(.037f,.062f,.068f),ColorPart.Pupil,false,9,5);
                face.Ellipsoid(new Vector3(side*.341f,.065f,.36f),new Vector3(.012f,.037f,.040f),ColorPart.Eye,false,8,4);
                face.Ellipsoid(new Vector3(side*.352f,.066f,.37f),new Vector3(.007f,.027f,.012f),ColorPart.Pupil,false,6,3);
                face.Tube(new[] {new Vector3(side*.30f,.13f,.28f),new Vector3(side*.32f,.13f,.41f)},new[] {.045f,.027f},ColorPart.Accent,6);
                face.Ellipsoid(new Vector3(side*.16f,-.12f,.80f),new Vector3(.013f,.020f,.025f),ColorPart.Pupil,false,6,3);
            }
            face.Tube(new[] {new Vector3(0,.035f,.71f),new Vector3(0,.22f,.78f),new Vector3(0,.38f,.88f)},new[] {.105f,.075f,0f},ColorPart.Horn,7);
            Render(head,face,"Triceratops_Head",materials);
            var tail=Bone("Tail",torso,new Vector3(0,1.15f,-1.0f));motion.tail=tail;
            var tailShape=new Shape();
            tailShape.Loft(new[] {new Vector3(0,-.43f,-1.10f),new Vector3(0,-.38f,-.78f),new Vector3(0,-.25f,-.43f),Vector3.zero},
                new[] {Vector2.zero,new Vector2(.07f,.075f),new Vector2(.14f,.13f),new Vector2(.23f,.25f)},false,10);
            Render(tail,tailShape,"Triceratops_Tail",materials);
            motion.upperLegs=new Transform[4];motion.lowerLegs=new Transform[4];motion.feet=new Transform[4];
            for(int i=0;i<4;i++)
            {
                int side=i%2==0 ? -1:1;bool front=i<2;
                string name=(front ? "Front":"Rear")+(side<0 ? "Left":"Right");
                var upper=Bone(name+"Leg",root.transform,new Vector3(side*.53f,1.12f,front ? .55f:-.69f));
                var leg=new Shape();
                leg.Ellipsoid(new Vector3(side*.03f,-.12f,front ? -.04f:.01f),new Vector3(.245f,.39f,.30f),ColorPart.Skin,false,10,6);
                var kneePosition=new Vector3(side*.015f,-.47f,front ? -.09f:.12f);
                leg.Tube(new[] {new Vector3(0,-.12f,0),kneePosition},new[] {.21f,.15f},ColorPart.Skin,9);
                Render(upper,leg,"Triceratops_"+name+"Upper",materials);
                var lower=Bone("Lower",upper,kneePosition);var shin=new Shape();
                var anklePosition=new Vector3(0,-.50f,front ? .09f:-.15f);
                shin.Tube(new[] {Vector3.zero,new Vector3(0,-.26f,front ? .025f:-.10f),anklePosition},new[] {.17f,.15f,.14f},ColorPart.Skin,8);
                Render(lower,shin,"Triceratops_"+name+"Lower",materials);
                var foot=Bone("Foot",lower,anklePosition);var footShape=new Shape();
                footShape.Ellipsoid(new Vector3(0,-.055f,.06f),new Vector3(.17f,.095f,.22f),ColorPart.Skin,false,10,4);
                for(int toe=0;toe<3;toe++) footShape.Ellipsoid(new Vector3((toe-1)*.091f,-.10f,.205f),new Vector3(.041f,.05f,.061f),ColorPart.Horn,false,6,3);
                Render(foot,footShape,"Triceratops_"+name+"Foot",materials);
                motion.upperLegs[i]=upper;motion.lowerLegs[i]=lower;motion.feet[i]=foot;
            }
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Triceratops Model And Previews...")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TriceratopsModel.prefab")
                && !EditorUtility.DisplayDialog("Обновить модель трицератопса?", "Сгенерированные меши, материалы и модельные префабы в Art/Models/Triceratops будут обновлены. Ручные изменения в них будут заменены; GUID существующих assets сохранятся. Игровая сцена этой командой не пересоздаётся.", "Обновить модель", "Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (bool enemy in new[] { false, true })
            {
                var root = Model(Palette(enemy));
                PrefabUtility.SaveAsPrefabAsset(root, Folder + (enemy ? "TriceratopsEnemyModel.prefab" : "TriceratopsModel.prefab"));
                ExportObj(root, Path.GetFullPath(Output + (enemy ? "Triceratops-enemy-lowpoly.obj" : "Triceratops-lowpoly.obj")));
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Preview();
        }
        public static void CreateAndApply() { Create(); ApplyToGame(); }
        [MenuItem("Prehistoric Frontiers/Art/Apply Triceratops Model To Battle")]
        public static void ApplyToGame()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TriceratopsModel.prefab");
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TriceratopsEnemyModel.prefab");
            if (!player || !enemy) throw new InvalidOperationException("Create the triceratops art assets before applying them.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "Triceratops", "Enemy_Triceratops" })
            {
                string path = "Assets/PrehistoricFrontiers/Prefabs/Units/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Install(root.GetComponent<RTSUnit>(), name == "Triceratops" ? player : enemy); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity", OpenSceneMode.Single);
            var stats = AssetDatabase.LoadAssetAtPath<UnitStats>("Assets/PrehistoricFrontiers/Data/TriceratopsStats.asset");
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
            var existing = unit.GetComponentInChildren<TriceratopsMotion>(true);
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
            var obj = new StringBuilder("# Prehistoric Frontiers stylized triceratops\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "TriceratopsEnemyModel.prefab" : "TriceratopsModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 1.65f;
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
                var positions = new[] { new Vector3(4, 2.7f, 5), new Vector3(5, 1.45f, 0), new Vector3(3, 5.5f, -5), new Vector3(0,1.45f,6), new Vector3(0,1.45f,-6) };
                var names = new[] { "Трицератопс-превью.png", "Трицератопс-профиль.png", "Трицератопс-вид-сверху.png", "Трицератопс-спереди.png", "Трицератопс-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0, 1.18f, -.04f));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Трицератопс-", "Трицератопс-враг-") : names[i])), snapshot.EncodeToPNG());
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
