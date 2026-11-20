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
    public static class ClinicModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Clinic/";
        const string Output = "../";
        enum ColorPart { Grass, Rock, Water, Fall, Foam, Trunk, Red, Orange, Gold, Dark, GrassLight, RockLight, WaterLight }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[13];
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
                    var mat = belly && row < rings / 2 ? ColorPart.Rock : material;
                    if(frontLight && Mathf.Sin((side+.5f)*Mathf.PI*2/sides)>.55f) mat=ColorPart.Rock;
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
                    for (int j = 0; j < sides; j++) { float angle = j * Mathf.PI * 2 / sides + (material == ColorPart.Red ? i*.13f : 0); points[i, j] = centers[i] + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radii[i]; }
                }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.Water : material;
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
            public void Disc(Vector3 center,float radius,ColorPart material,bool water=false)
            {
                const int n=28;
                Vector3 P(int j,float scale) {float a=j*Mathf.PI*2/n;return center+new Vector3(Mathf.Cos(a)*radius*scale,0,Mathf.Sin(a)*radius*scale);}
                for(int j=0;j<n;j++)
                {
                    var mat=water && j%4==0 ? ColorPart.WaterLight:material;
                    Triangle(center,P(j+1,.55f),P(j,.55f),mat);
                    Triangle(P(j,.55f),P(j+1,.55f),P(j+1,1),material);
                    Triangle(P(j,.55f),P(j+1,1),P(j,1),water && j%5==0 ? ColorPart.WaterLight:material);
                }
            }
            public void Sheet()
            {
                for(int i=0;i<3;i++)
                {
                    float t=i/3f,u=(i+1)/3f;
                    Vector3 P(float f,int side) => new Vector3(.32f+side*Mathf.Lerp(.23f,.16f,f),.27f+f*1.30f,Mathf.Lerp(-.71f,-1.03f,f));
                    var a=P(t,-1);var b=P(t,1);var c=P(u,1);var d=P(u,-1);
                    Triangle(a,b,c,ColorPart.Fall);Triangle(a,c,d,ColorPart.WaterLight);
                    var back=Vector3.back*.06f;Triangle(a+back,c+back,b+back,ColorPart.Fall);Triangle(a+back,d+back,c+back,ColorPart.Fall);
                    Triangle(a,d,d+back,ColorPart.Fall);Triangle(a,d+back,a+back,ColorPart.Fall);
                    Triangle(b,b+back,c+back,ColorPart.Fall);Triangle(b,c+back,c,ColorPart.Fall);
                }
            }
            public void Leaf(Vector3 center,float radius,ColorPart material)
            {
                const int n=10;
                Vector3 P(int j) {float a=j*Mathf.PI*2/n;float scale=j%2==0 ? 1:.52f;return center+new Vector3(Mathf.Cos(a)*radius*scale,0,Mathf.Sin(a)*radius*scale);}
                for(int j=0;j<n;j++) Triangle(center,P(j+1),P(j),material);
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
            string[] colors={"#607B28","#626A75","#119F99","#64DAD5","#D2EEEA","#684126","#D51C12","#E96008","#DDA34C","#452512","#91A52F","#949AA0","#33B9B0"};
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
            var root=new GameObject("ClinicModel");root.layer=13;var visual=root.AddComponent<ClinicVisual>();
            var terrain=Bone("Grass island",root.transform,Vector3.zero);var baseShape=new Shape();
            baseShape.Tube(new[] {new Vector3(0,0,0),new Vector3(0,.11f,0)},new[] {2.06f,2.06f},ColorPart.Grass,24);
            baseShape.Disc(new Vector3(0,.115f,0),2.06f,ColorPart.GrassLight);
            Render(terrain,baseShape,"Clinic_Island",materials);
            var pond=Bone("Pond",root.transform,Vector3.zero);var water=new Shape();water.Disc(new Vector3(0,.19f,0),1.75f,ColorPart.Water,true);
            Render(pond,water,"Clinic_Pond",materials);
            var rockBone=Bone("Stone border and waterfall arch",root.transform,Vector3.zero);var rocks=new Shape();
            for(int j=0;j<25;j++)
            {
                float a=j*Mathf.PI*2/25,radius=1.78f+(j%3)*.02f;
                rocks.Ellipsoid(new Vector3(Mathf.Cos(a)*radius,.23f+(j%2)*.035f,Mathf.Sin(a)*radius),new Vector3(.24f+(j%3)*.018f,.20f+(j%4)*.025f,.24f),j%4==0 ? ColorPart.RockLight:ColorPart.Rock,false,7,4);
            }
            for(int j=0;j<9;j++)
            {
                float a=j*Mathf.PI/8;
                rocks.Ellipsoid(new Vector3(.32f+Mathf.Cos(a)*.57f,.78f+Mathf.Sin(a)*.80f,-1.20f),new Vector3(.27f,.32f,.28f),j%3==0 ? ColorPart.RockLight:ColorPart.Rock,false,8,5);
            }
            rocks.Ellipsoid(new Vector3(-.25f,.55f,-1.17f),new Vector3(.28f,.44f,.28f),ColorPart.Rock,false,8,5);
            rocks.Ellipsoid(new Vector3(.90f,.55f,-1.17f),new Vector3(.28f,.44f,.28f),ColorPart.Rock,false,8,5);
            Render(rockBone,rocks,"Clinic_Rocks",materials);
            var fall=Bone("Waterfall",root.transform,Vector3.zero);var sheet=new Shape();sheet.Sheet();Render(fall,sheet,"Clinic_Waterfall",materials);
            var source=Bone("Curved golden spring",root.transform,Vector3.zero);var gold=new Shape();
            gold.Curve(new[] {new Vector3(-.48f,.18f,.28f),new Vector3(-.48f,.48f,.28f),new Vector3(-.37f,.75f,.28f),new Vector3(-.15f,.87f,.28f),new Vector3(-.03f,.85f,.28f)},new[] {.095f,.09f,.085f,.075f,.075f},ColorPart.Gold,20,9);
            gold.Ellipsoid(new Vector3(-.005f,.845f,.28f),new Vector3(.008f,.052f,.052f),ColorPart.Dark,false,8,4);
            Render(source,gold,"Clinic_GoldenSpring",materials);
            var trees=Bone("Autumn trees",root.transform,Vector3.zero);var trunks=new Shape();var foliage=new Shape();
            var locations=new[] {new Vector3(-1.30f,.10f,-.90f),new Vector3(-1.57f,.10f,.27f),new Vector3(1.31f,.10f,-.88f),new Vector3(1.57f,.10f,.20f)};
            for(int i=0;i<locations.Length;i++)
            {
                var p=locations[i];float height=i%2==0 ? 1.48f:1.18f;
                trunks.Tube(new[] {p,p+new Vector3(.03f,height*.55f,0),p+new Vector3(-.04f,height,0)},new[] {.12f,.085f,.055f},ColorPart.Trunk,7);
                trunks.Tube(new[] {p+new Vector3(0,height*.55f,0),p+new Vector3(.23f,height*.90f,.05f)},new[] {.065f,.025f},ColorPart.Trunk,6);
                foliage.Ellipsoid(p+new Vector3(-.09f,height+.10f,0),new Vector3(.42f,.43f,.36f),ColorPart.Red,false,7,4);
                foliage.Ellipsoid(p+new Vector3(.22f,height-.02f,.12f),new Vector3(.32f,.33f,.29f),ColorPart.Orange,false,7,4);
                foliage.Ellipsoid(p+new Vector3(-.12f,height+.37f,-.08f),new Vector3(.34f,.34f,.31f),ColorPart.Red,false,7,4);
                foliage.Ellipsoid(p+new Vector3(-.25f,height-.02f,.08f),new Vector3(.30f,.30f,.28f),ColorPart.Red,false,7,4);
                foliage.Ellipsoid(p+new Vector3(.12f,height+.20f,-.21f),new Vector3(.29f,.30f,.28f),ColorPart.Red,false,7,4);
            }
            Render(trees,trunks,"Clinic_Trunks",materials);var crowns=Bone("Red orange crowns",root.transform,Vector3.zero);Render(crowns,foliage,"Clinic_Foliage",materials);
            var details=Bone("Grass and floating leaves",root.transform,Vector3.zero);var plants=new Shape();
            for(int j=0;j<8;j++)
            {
                float a=j*Mathf.PI*2/8;var p=new Vector3(Mathf.Cos(a)*1.94f,.12f,Mathf.Sin(a)*1.94f);
                for(int blade=0;blade<3;blade++) plants.Spike(p,p+new Vector3((blade-1)*.10f,.20f+blade*.02f,.055f),.065f,ColorPart.GrassLight);
                float angle=a+.27f,radius=.85f+(j%3)*.15f;
                plants.Leaf(new Vector3(Mathf.Cos(angle)*radius,.202f,Mathf.Sin(angle)*radius),.09f,j%2==0 ? ColorPart.Red:ColorPart.Orange);
            }
            Render(details,plants,"Clinic_Plants",materials);
            var foam=Bone("Waterfall foam",root.transform,new Vector3(.32f,.23f,-.70f));visual.foam=foam;var bubbles=new Shape();
            for(int j=0;j<10;j++) {float a=j*Mathf.PI*2/10;bubbles.Ellipsoid(new Vector3(Mathf.Cos(a)*.23f,.025f,Mathf.Sin(a)*.12f),new Vector3(.10f,.07f,.085f),ColorPart.Foam,false,6,3);}
            Render(foam,bubbles,"Clinic_Foam",materials);
            visual.ripples=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var ripple=Bone(i==0 ? "Spring ripples":"Fall ripples",root.transform,i==0 ? new Vector3(-.48f,.207f,.28f):new Vector3(.32f,.207f,-.70f));var rings=new Shape();
                for(int j=0;j<3;j++)
                {
                    var points=new Vector3[29];var widths=new float[29];float radius=.20f+j*.14f;
                    for(int k=0;k<29;k++) {float a=k*Mathf.PI*2/28;points[k]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);widths[k]=.005f;}
                    rings.Tube(points,widths,ColorPart.Fall,4);
                }
                Render(ripple,rings,i==0 ? "Clinic_SpringRipples":"Clinic_FallRipples",materials);visual.ripples[i]=ripple;
            }
            visual.droplets=new Transform[3];
            for(int i=0;i<3;i++)
            {
                float t=(i+1)/4f;var drop=Bone("Flow "+i,root.transform,new Vector3(.32f+(i-1)*.065f,.31f+t*1.20f,Mathf.Lerp(-.73f,-1.01f,t)));var shape=new Shape();
                shape.Ellipsoid(Vector3.zero,new Vector3(.014f,.085f,.014f),ColorPart.Foam,false,5,3);Render(drop,shape,"Clinic_Flow"+i,materials);visual.droplets[i]=drop;
            }
            foreach(var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=13;
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Clinic Model And Previews...")]
        static void CreateFromMenu()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ClinicModel.prefab") && !EditorUtility.DisplayDialog("Обновить модель лечебницы?","Модельные assets будут обновлены с сохранением GUID. Ручные изменения сгенерированной модели заменятся.","Обновить","Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var root=Model(Palette(false));
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"ClinicModel.prefab");ExportObj(root,Path.GetFullPath(Output+"Clinic-lowpoly.obj"));UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();Preview();
        }
        public static void CreateAndApply() {Create();ApplyToGame();}
        [MenuItem("Prehistoric Frontiers/Art/Apply Clinic Model To Battle")]
        public static void ApplyToGame()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ClinicModel.prefab");if(!model) throw new InvalidOperationException("Generate clinic art first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity",OpenSceneMode.Single);
            var system=UnityEngine.Object.FindAnyObjectByType<ConstructionSystem>();if(!system) throw new InvalidOperationException("Construction system missing.");
            system.clinicModelPrefab=model;EditorUtility.SetDirty(system);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("CLINIC_MODEL_ASSIGNED_TO_CONSTRUCTION");
        }
        static void ExportObj(GameObject root, string path)
        {
            var obj = new StringBuilder("# Prehistoric Frontiers low poly healing pond\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "ClinicModel.prefab" : "ClinicModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 2.55f;
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
                var positions = new[] { new Vector3(4,4.4f,6),new Vector3(6,2.9f,0),new Vector3(0,7,.01f),new Vector3(0,3.2f,6),new Vector3(0,3.2f,-6) };
                var names = new[] { "Лечебница-превью.png", "Лечебница-профиль.png", "Лечебница-вид-сверху.png", "Лечебница-спереди.png", "Лечебница-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0,.82f,0));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Лечебница-", "Лечебница-враг-") : names[i])), snapshot.EncodeToPNG());
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
