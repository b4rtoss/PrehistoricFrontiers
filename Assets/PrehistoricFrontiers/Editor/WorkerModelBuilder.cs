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
    public static class WorkerModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Worker/";
        const string Output = "../";
        enum ColorPart { Coat, Belly, FurLight, Claw, Eye, Pupil, FurMid, Hoof, FurDark, Ear, Tip }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[11];
            public Shape() { for (int i = 0; i < faces.Length; i++) faces[i] = new List<int>(); }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, ColorPart material)
            {
                var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < .00000001f) return;
                n.Normalize(); int index = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); normals.Add(n); normals.Add(n); normals.Add(n);
                faces[(int)material].Add(index); faces[(int)material].Add(index + 1); faces[(int)material].Add(index + 2);
            }
            public void Ellipsoid(Vector3 center, Vector3 radius, ColorPart material, bool belly = false, int sides = 10, int rings = 5, bool frontLight = false)
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
                    if(frontLight && Mathf.Sin((side+.5f)*Mathf.PI*2/sides)>.55f) mat=ColorPart.Belly;
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
                    for (int j = 0; j < sides; j++) { float angle = j * Mathf.PI * 2 / sides + (material == ColorPart.FurMid ? i*.13f : 0); points[i, j] = centers[i] + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radii[i]; }
                }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.FurLight : material;
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
            public void Curve(Vector3[] controls, float[] radii, ColorPart material, int samples=30, int sides=9)
            {
                var centers=new Vector3[samples+1];var widths=new float[samples+1];
                for(int i=0;i<=samples;i++)
                {
                    float t=i*(controls.Length-1f)/samples;int k=Mathf.Min(Mathf.FloorToInt(t),controls.Length-2);float f=t-k;
                    var a=controls[Mathf.Max(0,k-1)];var b=controls[k];var c=controls[k+1];var d=controls[Mathf.Min(k+2,controls.Length-1)];
                    centers[i]=.5f*((2*b)+(-a+c)*f+(2*a-5*b+4*c-d)*f*f+(-a+3*b-3*c+d)*f*f*f);
                    widths[i]=Mathf.Lerp(radii[k],radii[k+1],f);
                }
                Tube(centers,widths,material,sides);
            }
            public void Tuft(Vector3 top, Vector3 tip, Vector3 across, Vector3 outward, float width, ColorPart material)
            {
                var u=across.normalized*width;var n=outward.normalized;
                var a=top-u*.5f;var b=top+u*.5f;var c=Vector3.Lerp(top,tip,.53f)+u*.40f+n*.025f;
                var d=tip+n*.025f;var e=Vector3.Lerp(top,tip,.53f)-u*.40f+n*.025f;
                var ridge=Vector3.Lerp(top,tip,.35f)+n*.06f;
                void Face(Vector3 x,Vector3 y,Vector3 z,ColorPart mat) { if(Vector3.Dot(Vector3.Cross(y-x,z-x),n)<0) Triangle(z,y,x,mat);else Triangle(x,y,z,mat); }
                Face(a,b,ridge,material);Face(b,c,ridge,material);Face(c,d,ridge,material);Face(d,e,ridge,material);Face(e,a,ridge,material);
                Triangle(a,c,b,ColorPart.FurDark);Triangle(a,e,d,ColorPart.FurDark);Triangle(a,d,c,ColorPart.FurDark);
            }
            public void Pelt(Vector3 a,Vector3 b,Vector3 c,Vector3 tip,Vector3 e,Vector3 outward,ColorPart material)
            {
                var n=outward.normalized;var ridge=(a+b)*.30f+(c+e)*.20f+n*.055f;
                void Face(Vector3 x,Vector3 y,Vector3 z) {if(Vector3.Dot(Vector3.Cross(y-x,z-x),n)<0) Triangle(z,y,x,material);else Triangle(x,y,z,material);}
                Face(a,b,ridge);Face(b,c,ridge);Face(c,tip,ridge);Face(tip,e,ridge);Face(e,a,ridge);
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
            string[] colors={"#74533F","#AE9271","#85654F","#29221B","#AC7C36","#201A14","#583E2F","#5B4030","#3D2A21","#654B3B","#241A15"};
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
            var root=new GameObject("WorkerModel");root.layer=10;var motion=root.AddComponent<WorkerMotion>();
            var torso=Bone("Torso",root.transform,Vector3.zero);motion.torso=torso;var body=new Shape();
            body.Ellipsoid(new Vector3(0,.94f,0),new Vector3(.245f,.36f,.19f),ColorPart.Coat,false,12,7,true);
            body.Ellipsoid(new Vector3(0,1.20f,0),new Vector3(.31f,.23f,.20f),ColorPart.Coat,false,12,6,true);
            body.Ellipsoid(new Vector3(0,.67f,-.005f),new Vector3(.22f,.17f,.16f),ColorPart.Coat,false,10,5);
            body.Ellipsoid(new Vector3(0,1.17f,-.095f),new Vector3(.255f,.40f,.18f),ColorPart.FurMid,false,12,6);
            body.Ellipsoid(new Vector3(0,1.49f,-.08f),new Vector3(.24f,.23f,.16f),ColorPart.FurMid,false,10,5);
            for(int j=0;j<3;j++)
            {
                float y=1.56f-j*.25f,w=j==2 ? .40f:.54f,z=j==0 ? -.19f:-.27f,tipZ=j==0 ? -.28f : (j==1 ? -.24f:-.16f);
                body.Pelt(new Vector3(-w*.32f,y,z),new Vector3(w*.32f,y,z),new Vector3(w*.52f,y-.19f,z-.025f),new Vector3(0,y-.46f,tipZ),new Vector3(-w*.52f,y-.19f,z-.025f),Vector3.back,j%2==0 ? ColorPart.FurMid:ColorPart.FurDark);
            }
            Render(torso,body,"Worker_Torso",materials);
            var head=Bone("Head",torso,new Vector3(0,1.47f,.04f));motion.head=head;var face=new Shape();
            face.Ellipsoid(new Vector3(0,.06f,.02f),new Vector3(.215f,.22f,.20f),ColorPart.Coat,false,12,6);
            face.Ellipsoid(new Vector3(0,0,.22f),new Vector3(.165f,.10f,.18f),ColorPart.FurLight,false,10,5);
            face.Ellipsoid(new Vector3(0,-.075f,.20f),new Vector3(.14f,.05f,.14f),ColorPart.Belly,false,10,4);
            face.Ellipsoid(new Vector3(0,.035f,.38f),new Vector3(.070f,.039f,.025f),ColorPart.Tip,false,8,4);
            foreach(int side in new[] {-1,1})
            {
                face.Ellipsoid(new Vector3(side*.139f,.106f,.177f),new Vector3(.043f,.028f,.033f),ColorPart.Pupil,false,8,4);
                face.Ellipsoid(new Vector3(side*.145f,.108f,.203f),new Vector3(.024f,.017f,.008f),ColorPart.Eye,false,8,4);
                face.Ellipsoid(new Vector3(side*.145f,.109f,.211f),new Vector3(.009f,.014f,.004f),ColorPart.Pupil,false,6,3);
                face.Tube(new[] {new Vector3(side*.10f,.141f,.19f),new Vector3(side*.18f,.142f,.154f)},new[] {.026f,.016f},ColorPart.FurMid,6);
                face.Tube(new[] {new Vector3(0,-.017f,.39f),new Vector3(side*.07f,-.062f,.345f),new Vector3(side*.124f,-.050f,.28f)},new[] {.009f,.008f,.003f},ColorPart.Tip,5);
                face.Spike(new Vector3(side*.145f,.21f,-.027f),new Vector3(side*.205f,.39f,-.037f),.096f,ColorPart.FurMid);
                face.Spike(new Vector3(side*.151f,.24f,.010f),new Vector3(side*.201f,.363f,-.008f),.047f,ColorPart.Ear);
                for(int j=0;j<3;j++) face.Tuft(new Vector3(side*.18f,.15f-j*.10f,-.045f),new Vector3(side*.23f,-.10f-j*.09f,-.07f),Vector3.forward,new Vector3(side,0,.1f),.20f,ColorPart.FurMid);
            }
            Render(head,face,"Worker_Head",materials);
            var tail=Bone("Tail",torso,new Vector3(0,.61f,-.15f));motion.tail=tail;var tailShape=new Shape();
            tailShape.Curve(new[] {Vector3.zero,new Vector3(0,-.07f,-.23f),new Vector3(0,-.13f,-.47f),new Vector3(0,-.23f,-.66f),new Vector3(0,-.30f,-.71f)},new[] {.09f,.14f,.17f,.13f,0f},ColorPart.FurMid,14,8);
            for(int j=0;j<3;j++) tailShape.Tuft(new Vector3(0,-.02f-j*.06f,-.15f-j*.16f),new Vector3(0,-.23f-j*.07f,-.35f-j*.17f),Vector3.right,Vector3.up,.25f,ColorPart.FurDark);
            Render(tail,tailShape,"Worker_Tail",materials);
            motion.upperArms=new Transform[2];motion.forearms=new Transform[2];
            motion.upperLegs=new Transform[2];motion.lowerLegs=new Transform[2];motion.feet=new Transform[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0 ? -1:1;string name=side<0 ? "Left":"Right";
                var arm=Bone(name+"Arm",torso,new Vector3(side*.28f,1.23f,0));var upper=new Shape();
                upper.Ellipsoid(new Vector3(side*.047f,-.12f,0),new Vector3(.135f,.185f,.135f),ColorPart.Coat,false,10,5);
                upper.Tube(new[] {Vector3.zero,new Vector3(side*.11f,-.28f,.01f)},new[] {.10f,.08f},ColorPart.Coat,8);
                Render(arm,upper,"Worker_"+name+"Arm",materials);
                var forearm=Bone("Forearm",arm,new Vector3(side*.11f,-.28f,.01f));var hand=new Shape();
                hand.Ellipsoid(new Vector3(0,-.14f,.025f),new Vector3(.115f,.18f,.12f),ColorPart.FurMid,false,10,5);
                hand.Tuft(new Vector3(side*.08f,-.03f,.015f),new Vector3(side*.13f,-.29f,.035f),Vector3.forward,new Vector3(side,0,0),.22f,ColorPart.FurMid);
                hand.Tuft(new Vector3(0,-.07f,.11f),new Vector3(0,-.30f,.14f),Vector3.right,Vector3.forward,.19f,ColorPart.FurDark);
                hand.Ellipsoid(new Vector3(0,-.30f,.06f),new Vector3(.095f,.10f,.075f),ColorPart.Coat,false,8,4);
                for(int finger=0;finger<3;finger++)
                {
                    float x=(finger-1)*.053f;
                    hand.Curve(new[] {new Vector3(x,-.34f,.09f),new Vector3(x,-.42f,.12f),new Vector3(x,-.45f,.18f)},new[] {.025f,.019f,0f},ColorPart.Claw,6,5);
                }
                hand.Curve(new[] {new Vector3(-side*.07f,-.25f,.08f),new Vector3(-side*.125f,-.30f,.12f),new Vector3(-side*.11f,-.36f,.16f)},new[] {.03f,.022f,0f},ColorPart.Claw,6,5);
                Render(forearm,hand,"Worker_"+name+"Forearm",materials);
                motion.upperArms[i]=arm;motion.forearms[i]=forearm;
                var hip=Bone(name+"Hip",root.transform,new Vector3(side*.14f,.66f,0));var thigh=new Shape();
                thigh.Ellipsoid(new Vector3(0,-.10f,0),new Vector3(.14f,.24f,.16f),ColorPart.Coat,false,10,5);
                Render(hip,thigh,"Worker_"+name+"Thigh",materials);
                var knee=Bone("Knee",hip,new Vector3(0,-.30f,.03f));var shin=new Shape();
                shin.Tube(new[] {Vector3.zero,new Vector3(0,-.16f,-.015f),new Vector3(0,-.28f,-.015f)},new[] {.10f,.088f,.08f},ColorPart.Coat,8);
                Render(knee,shin,"Worker_"+name+"Shin",materials);
                var foot=Bone("Foot",knee,new Vector3(0,-.28f,-.015f));var footShape=new Shape();
                footShape.Ellipsoid(new Vector3(0,-.02f,.055f),new Vector3(.11f,.06f,.16f),ColorPart.Coat,false,10,4);
                for(int toe=0;toe<4;toe++) footShape.Curve(new[] {new Vector3((toe-1.5f)*.046f,-.035f,.16f),new Vector3((toe-1.5f)*.046f,-.04f,.215f),new Vector3((toe-1.5f)*.046f,-.07f,.235f)},new[] {.022f,.017f,0f},ColorPart.Claw,5,5);
                Render(foot,footShape,"Worker_"+name+"Foot",materials);motion.upperLegs[i]=hip;motion.lowerLegs[i]=knee;motion.feet[i]=foot;
            }
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Worker Model And Previews...")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "WorkerModel.prefab")
                && !EditorUtility.DisplayDialog("Обновить модель рабочего?", "Сгенерированные меши, материалы и модельные префабы в Art/Models/Worker будут обновлены. Ручные изменения в них будут заменены; GUID существующих assets сохранятся. Игровая сцена этой командой не пересоздаётся.", "Обновить модель", "Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (bool enemy in new[] { false })
            {
                var root = Model(Palette(enemy));
                PrefabUtility.SaveAsPrefabAsset(root, Folder + (enemy ? "WorkerEnemyModel.prefab" : "WorkerModel.prefab"));
                ExportObj(root, Path.GetFullPath(Output + (enemy ? "Worker-enemy-lowpoly.obj" : "Worker-lowpoly.obj")));
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Preview();
        }
        public static void CreateAndApply() { Create(); ApplyToGame(); }
        [MenuItem("Prehistoric Frontiers/Art/Apply Worker Model To Battle")]
        public static void ApplyToGame()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"WorkerModel.prefab");
            if(!player) throw new InvalidOperationException("Generate the worker model before applying it.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string path="Assets/PrehistoricFrontiers/Prefabs/Units/Worker.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {Install(root.GetComponent<RTSUnit>(),player);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
            var scene = EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity", OpenSceneMode.Single);
            var stats = AssetDatabase.LoadAssetAtPath<UnitStats>("Assets/PrehistoricFrontiers/Data/WorkerStats.asset");
            foreach (var unit in UnityEngine.Object.FindObjectsByType<RTSUnit>())
                if (unit.stats == stats) Install(unit, player);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("WORKER_ART_APPLIED_WITH_EXISTING_COLLIDERS");
        }
        static void Install(RTSUnit unit, GameObject model)
        {
            var visual = unit.transform.Find("Visual");
            if (!visual || !visual.GetComponent<CapsuleCollider>()) throw new InvalidOperationException("Existing selection collider missing on " + unit.name);
            visual.GetComponent<Renderer>().enabled = false;
            var existing = unit.GetComponentInChildren<WorkerMotion>(true);
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
            var obj = new StringBuilder("# Prehistoric Frontiers stylized worker\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                foreach(bool enemy in new[] {false})
                {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "WorkerEnemyModel.prefab" : "WorkerModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 1.12f;
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
                var positions = new[] { new Vector3(3,1.9f,4),new Vector3(4,1.05f,-.15f),new Vector3(3,4,-4),new Vector3(0,1.05f,5),new Vector3(0,1.05f,-5) };
                var names = new[] { "Рабочий-превью.png", "Рабочий-профиль.png", "Рабочий-вид-сверху.png", "Рабочий-спереди.png", "Рабочий-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0,.93f,-.10f));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Рабочий-", "Рабочий-враг-") : names[i])), snapshot.EncodeToPNG());
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
