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
    public static class EnvironmentModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Environment/LowPoly/";
        const string Output = "../";
        enum ColorPart { Bark,BarkLight,Orange,OrangeLight,OrangeDark,Green,GreenLight,GreenDark,Rock,RockLight,Berry }
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
            public void Tube(Vector3[] centers, float[] radii, ColorPart material, int sides = 8, bool bands = false)
            {
                var points = new Vector3[centers.Length, sides];
                for (int i = 0; i < centers.Length; i++)
                {
                    var tangent = (centers[Mathf.Min(i + 1, centers.Length - 1)] - centers[Mathf.Max(0, i - 1)]).normalized;
                    var right = Vector3.Cross(Mathf.Abs(tangent.y) > .9f ? Vector3.forward : Vector3.up, tangent).normalized;
                    var up = Vector3.Cross(tangent, right).normalized;
                    for (int j = 0; j < sides; j++) { float angle = j * Mathf.PI * 2 / sides + i*.065f; points[i, j] = centers[i] + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radii[i]; }
                }
                for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.BarkLight : material;
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
            public void Ico(Vector3 center,Vector3 radius,ColorPart material,int seed,int subdivisions=1)
            {
                float t=(1+Mathf.Sqrt(5))/2;
                var v=new[] {new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
                int[] f={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
                int face=0;
                Vector3 P(Vector3 p)
                {
                    p.Normalize();float jitter=.94f+.06f*Mathf.Abs(Mathf.Sin(Vector3.Dot(p,new Vector3(12.97f,37.23f,19.11f))+seed*3.17f));
                    return center+Vector3.Scale(p* jitter,radius);
                }
                void Face(Vector3 a,Vector3 b,Vector3 c)
                {
                    var mat=material;float top=(a.normalized.y+b.normalized.y+c.normalized.y)/3;int n=face++;
                    if(material==ColorPart.Orange) mat=top>.1f && n%3==0 ? ColorPart.OrangeLight:(n%7==0 ? ColorPart.OrangeDark:material);
                    if(material==ColorPart.Green) mat=top>.1f && n%3==0 ? ColorPart.GreenLight:(n%7==0 ? ColorPart.GreenDark:material);
                    if(material==ColorPart.Rock && top>.1f && n%3==0) mat=ColorPart.RockLight;
                    Triangle(P(a),P(b),P(c),mat);
                }
                for(int i=0;i<f.Length;i+=3)
                {
                    var a=v[f[i]].normalized;var b=v[f[i+1]].normalized;var c=v[f[i+2]].normalized;
                    if(subdivisions==0) {Face(a,b,c);continue;}
                    var ab=(a+b).normalized;var bc=(b+c).normalized;var ca=(c+a).normalized;
                    Face(a,ab,ca);Face(b,bc,ab);Face(c,ca,bc);Face(ab,bc,ca);
                }
            }
            public void PineLayer(float y,float height,float radius,int seed)
            {
                const int sides=9;
                float[] levels={0,.12f,.62f,1};float[] widths={.91f,1,.48f,0};
                Vector3 P(int row,int j)
                {
                    float a=j*Mathf.PI*2/sides+seed*.17f+row*.06f;
                    float r=widths[row]*radius*(1+.035f*Mathf.Sin(j*2.1f+seed));
                    return new Vector3(Mathf.Cos(a)*r+.08f*levels[row],y+levels[row]*height,Mathf.Sin(a)*r);
                }
                for(int row=0;row<3;row++) for(int j=0;j<sides;j++)
                {
                    var a=P(row,j);var b=P(row+1,j);var c=P(row+1,j+1);var d=P(row,j+1);
                    var mat=(j+row+seed)%5==0 ? ColorPart.GreenLight:ColorPart.Green;
                    Triangle(a,b,c,mat);Triangle(a,c,d,(j+seed)%4==0 ? ColorPart.GreenDark:mat);
                }
                for(int j=0;j<sides;j++) Triangle(new Vector3(0,y,0),P(0,j),P(0,j+1),ColorPart.GreenDark);
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
        static Material[] Palette()
        {
            string[] colors={"#76502C","#946239","#DA5B04","#EE9404","#B93306","#2E6E23","#4A8A29","#214B22","#64717B","#87949B","#C62522"};
            var result=new Material[colors.Length];
            for(int i=0;i<colors.Length;i++)
            {
                string path=Folder+((ColorPart)i)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!mat) {mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
                ColorUtility.TryParseHtmlString(colors[i],out var color);mat.color=color;mat.SetFloat("_Glossiness",.04f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);result[i]=mat;
            }
            return result;
        }
        static string Name(EnvironmentArtKind kind,int variant) => kind+"_"+variant;
        static GameObject Model(EnvironmentArtKind kind,int variant,Material[] materials)
        {
            var root=new GameObject(Name(kind,variant));root.layer=11;var marker=root.AddComponent<EnvironmentArt>();marker.kind=kind;marker.variant=variant;var mesh=new Shape();
            if(kind==EnvironmentArtKind.Deciduous || kind==EnvironmentArtKind.Conifer)
            {
                mesh.Curve(new[] {Vector3.zero,new Vector3(.04f,1.1f,0),new Vector3(-.06f,2.3f,.02f),new Vector3(.03f,2.9f,0)},new[] {.29f,.23f,.15f,.07f},ColorPart.Bark,9,7);
                if(kind==EnvironmentArtKind.Deciduous)
                {
                    foreach(int side in new[] {-1,1}) mesh.Curve(new[] {new Vector3(0,1.55f,0),new Vector3(side*.35f,2.12f,0),new Vector3(side*.66f,2.72f,.07f)},new[] {.13f,.09f,.04f},ColorPart.BarkLight,6,6);
                    var centers=new[] {new Vector3(0,3.22f,-.12f),new Vector3(-.62f,2.80f,.08f),new Vector3(.62f,3.03f,.04f),new Vector3(-.25f,2.15f,.40f),new Vector3(.05f,3.86f,-.10f)};
                    var radii=new[] {new Vector3(.77f,.88f,.72f),new Vector3(.73f,.72f,.64f),new Vector3(.65f,.73f,.62f),new Vector3(.60f,.65f,.59f),new Vector3(.57f,.61f,.56f)};
                    for(int i=0;i<centers.Length;i++) mesh.Ico(centers[i]+new Vector3(Mathf.Sin(variant+i)*.10f,variant*.035f,Mathf.Cos(variant+i)*.08f),radii[i],ColorPart.Orange,variant*11+i);
                }
                else {mesh.PineLayer(1.02f,1.75f,1.18f,variant);mesh.PineLayer(1.77f,1.85f,.98f,variant+2);mesh.PineLayer(2.55f,1.88f,.74f,variant+4);}
            }
            else if(kind==EnvironmentArtKind.Rock)
            {
                mesh.Ico(new Vector3(0,.50f,0),new Vector3(.50f,.50f,.48f),ColorPart.Rock,17+variant*7);
                mesh.Ico(new Vector3(.24f,.20f,.16f),new Vector3(.24f,.20f,.23f),ColorPart.Rock,23+variant);
                mesh.Ico(new Vector3(-.24f,.15f,.12f),new Vector3(.23f,.15f,.21f),ColorPart.Rock,30+variant);
            }
            else
            {
                mesh.Curve(new[] {Vector3.zero,new Vector3(0,.43f,.02f),new Vector3(.10f,.73f,0)},new[] {.045f,.03f,0f},ColorPart.Bark,5,5);
                var centers=new[] {new Vector3(-.34f,.43f,.02f),new Vector3(.29f,.48f,-.12f),new Vector3(.02f,.42f,.32f)};
                for(int i=0;i<centers.Length;i++)
                {
                    mesh.Ico(centers[i],new Vector3(.51f,.43f,.48f),ColorPart.Green,40+variant*5+i);
                    for(int j=0;j<6;j++) {float a=j*Mathf.PI/3+variant*.37f;mesh.Ico(centers[i]+new Vector3(Mathf.Cos(a)*.39f,.22f+(.04f*(j%2)),Mathf.Sin(a)*.36f),Vector3.one*.083f,ColorPart.Berry,variant+j,0);}
                }
            }
            root.AddComponent<MeshFilter>().sharedMesh=mesh.Save(Name(kind,variant));root.AddComponent<MeshRenderer>().sharedMaterials=materials;return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Environment Models And Previews...")]
        static void CreateFromMenu() {if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Deciduous_0.prefab") && !EditorUtility.DisplayDialog("Обновить модели окружения?","Сгенерированные меши и материалы будут обновлены с сохранением GUID. Сцена изменяется отдельной командой.","Обновить","Отмена")) return;Create();}
        public static void Create()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var materials=Palette();
            foreach(EnvironmentArtKind kind in Enum.GetValues(typeof(EnvironmentArtKind))) for(int i=0;i<3;i++) {var root=Model(kind,i,materials);PrefabUtility.SaveAsPrefabAsset(root,Folder+Name(kind,i)+".prefab");ExportObj(root,Path.GetFullPath(Output+"Environment-"+Name(kind,i)+".obj"));UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Preview();
        }
        public static void CreateAndApply() {Create();ApplyToGame();}
        static void Install(Transform parent,EnvironmentArtKind kind,int variant,Vector3 position,Vector3 scale,float yaw,bool resource)
        {
            foreach(var old in parent.GetComponentsInChildren<EnvironmentArt>(true)) if(old.transform!=parent) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+Name(kind,variant)+".prefab");if(!prefab) throw new InvalidOperationException("Generate environment assets first.");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);model.name="Environment model";model.transform.localPosition=position;model.transform.localScale=scale;model.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var marker=model.GetComponent<EnvironmentArt>();marker.resource=resource;PrefabUtility.RecordPrefabInstancePropertyModifications(marker);PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);EditorUtility.SetDirty(marker);
        }
        [MenuItem("Prehistoric Frontiers/Art/Apply Random Environment To Battle")]
        public static void ApplyToGame()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity",OpenSceneMode.Single);
            var original=new Dictionary<Collider,Matrix4x4>();foreach(var col in UnityEngine.Object.FindObjectsByType<Collider>()) original[col]=col.transform.localToWorldMatrix;
            var random=new System.Random(20261120);int trees=0,stones=0,resources=0;
            foreach(var obj in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if(!obj || obj.name!="Tree") continue;var trunk=obj.Find("Trunk");var canopy=obj.Find("Canopy");if(!trunk || !canopy) continue;
                trunk.GetComponent<Renderer>().enabled=false;canopy.GetComponent<Renderer>().enabled=false;
                var kind=random.Next(2)==0 ? EnvironmentArtKind.Deciduous:EnvironmentArtKind.Conifer;float scale=.96f+(float)random.NextDouble()*.07f;
                var pos=obj.InverseTransformPoint(new Vector3(trunk.position.x,0,trunk.position.z));Install(obj,kind,random.Next(3),pos,Vector3.one*scale,(float)random.NextDouble()*360,false);trees++;
            }
            foreach(string name in new[] {"Central basalt","Basalt outcrop","Western stone"})
            {
                var host=GameObject.Find(name);if(!host) continue;host.GetComponent<Renderer>().enabled=false;
                Install(host.transform,EnvironmentArtKind.Rock,random.Next(3),Vector3.down*.5f,Vector3.one,(float)random.NextDouble()*360,false);stones++;
            }
            foreach(var node in UnityEngine.Object.FindObjectsByType<ResourceNode>())
            {
                foreach(var renderer in node.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
                var kind=node.kind==ResourceKind.Food ? EnvironmentArtKind.BerryBush:(random.Next(2)==0 ? EnvironmentArtKind.Deciduous:EnvironmentArtKind.Conifer);
                Install(node.transform,kind,random.Next(3),Vector3.zero,Vector3.one*(node.kind==ResourceKind.Wood ? .80f:1),(float)random.NextDouble()*360,true);resources++;
            }
            var after=UnityEngine.Object.FindObjectsByType<Collider>();if(after.Length!=original.Count) throw new InvalidOperationException("Environment art must not add colliders.");
            foreach(var pair in original) if(!pair.Key || pair.Key.transform.localToWorldMatrix!=pair.Value) throw new InvalidOperationException("Original collider moved while applying art.");
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("ENVIRONMENT_ART_APPLIED trees="+trees+" rocks="+stones+" resources="+resources+" / colliders unchanged="+original.Count);
        }
        static void ExportObj(GameObject root, string path)
        {
            var obj = new StringBuilder("# Prehistoric Frontiers low poly environment\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var camObject=new GameObject("Art camera");SceneManager.MoveGameObjectToScene(camObject,scene);var camera=camObject.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=2.8f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.16f,.20f);camera.nearClipPlane=.05f;camera.farClipPlane=40;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.localScale=Vector3.one*4;var ground=new Material(Shader.Find("Standard")){color=new Color(.19f,.25f,.26f)};floor.GetComponent<Renderer>().sharedMaterial=ground;
                for(int i=0;i<2;i++) {var lightObject=new GameObject("Studio light");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=i==0 ? 1:.4f;light.transform.rotation=Quaternion.Euler(i==0 ? 45:30,i==0 ? -35:145,0);light.shadows=i==0 ? LightShadows.Soft:LightShadows.None;}
                foreach(EnvironmentArtKind kind in Enum.GetValues(typeof(EnvironmentArtKind)))
                {
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+Name(kind,0)+".prefab"),scene);
                    bool tree=kind==EnvironmentArtKind.Deciduous || kind==EnvironmentArtKind.Conifer;camera.orthographicSize=tree ? 2.8f:1.05f;var targetPoint=new Vector3(0,tree ? 2.15f:.5f,0);camera.transform.position=targetPoint+new Vector3(4,2,6);camera.transform.LookAt(targetPoint);
                    var active=RenderTexture.active;var target=new RenderTexture(1200,900,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(Path.GetFullPath(Output+"Environment-"+kind+"-preview.png"),pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=active;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(model);
                }
                UnityEngine.Object.DestroyImmediate(ground);
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
