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
    public static class MammothModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Mammoth/";
        const string Output = "../";
        enum ColorPart { Coat, Trunk, FurLight, Tusk, Eye, Pupil, FurMid, Hoof, FurDark, Ear, Tip }
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
            public void Ellipsoid(Vector3 center, Vector3 radius, ColorPart material, bool belly = false, int sides = 10, int rings = 5)
            {
                Vector3 Point(int row, int side)
                {
                    float latitude = -Mathf.PI / 2 + Mathf.PI * row / rings, angle = Mathf.PI * 2 * side / sides + (row%2==0 ? .07f : -.04f);
                    return center + Vector3.Scale(new Vector3(Mathf.Cos(latitude) * Mathf.Cos(angle), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(angle)), radius);
                }
                for (int row = 0; row < rings; row++) for (int side = 0; side < sides; side++)
                {
                    var a = Point(row, side); var b = Point(row + 1, side); var c = Point(row + 1, side + 1); var d = Point(row, side + 1);
                    var mat = belly && row < rings / 2 ? ColorPart.Trunk : material;
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
            string[] colors=enemy ? new[] {"#3B2015","#632D14","#784018","#D3B17A","#895315","#21180E","#592B14","#5F3B28","#2C1B14","#714A34","#29150F"}
                : new[] {"#B18B54","#AF7B54","#DAB57C","#D3B17A","#895315","#21180E","#C59B61","#9D7854","#94683D","#BA8863","#3D2415"};
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
            var root=new GameObject("MammothModel");root.layer=10;var motion=root.AddComponent<MammothMotion>();
            var torso=Bone("Torso",root.transform,Vector3.zero);motion.torso=torso;
            var body=new Shape();var bodyCenter=new Vector3(0,2.0f,-.20f);
            body.Ellipsoid(bodyCenter,new Vector3(.96f,1.12f,1.43f),ColorPart.Coat,false,16,9);
            Vector3 Surface(float latitude,float z,int side)
            {
                // Leave room for the chord between facets: the fur shell must stay outside the torso.
                float r=Mathf.Sqrt(Mathf.Max(.05f,1-Mathf.Pow((z+.20f)/1.65f,2)));
                return new Vector3(side*Mathf.Cos(latitude)*(1.12f*r+.03f),2+Mathf.Sin(latitude)*1.28f*r,z);
            }
            foreach(int side in new[] {-1,1}) for(int row=0;row<4;row++) for(int j=0;j<5;j++)
            {
                float z=-1.19f+j*.48f+(row%2)*.09f, latitude=1.22f-row*.47f;
                float end=latitude-.84f,mid=Mathf.Lerp(latitude,end,.54f),width=.70f;
                body.Pelt(Surface(latitude,z-width*.5f,side),Surface(latitude,z+width*.5f,side),Surface(mid,z+width*.43f,side),Surface(end,z+.025f,side),Surface(mid,z-width*.43f,side),new Vector3(side,.2f,0),
                    (row+j)%3==0 ? ColorPart.FurLight : ((row+j)%3==1 ? ColorPart.FurMid : ColorPart.Coat));
            }
            Vector3 Rear(float x,float y)
            {
                float r=Mathf.Sqrt(Mathf.Max(.025f,1-Mathf.Pow(x/1.12f,2)-Mathf.Pow((y-2)/1.30f,2)));
                return new Vector3(x,y,-.20f-r*1.60f-.10f);
            }
            for(int row=0;row<3;row++) for(int j=-1;j<=1;j++)
            {
                float y=2.90f-row*.54f;float x=j*.46f;
                body.Pelt(Rear(x-.30f,y),Rear(x+.30f,y),Rear(x+.26f,y-.40f),Rear(x,y-.80f),Rear(x-.26f,y-.40f),Vector3.back,
                    (row+j)%2==0 ? ColorPart.FurLight:ColorPart.FurMid);
            }
            for(int row=0;row<2;row++) for(int j=-1;j<=1;j++)
            {
                Vector3 Front(float x,float y) {var p=Rear(x,y);p.z=-.40f-p.z;return p;}
                float y=2.35f-row*.50f,x=j*.43f;
                body.Pelt(Front(x-.27f,y),Front(x+.27f,y),Front(x+.23f,y-.37f),Front(x,y-.75f),Front(x-.23f,y-.37f),Vector3.forward,
                    j%2==0 ? ColorPart.FurLight:ColorPart.FurMid);
            }
            Render(torso,body,"Mammoth_Torso",materials);
            var head=Bone("Head",torso,new Vector3(0,2.55f,1.05f));motion.head=head;var skull=new Shape();
            skull.Ellipsoid(new Vector3(0,.05f,.10f),new Vector3(.58f,.62f,.59f),ColorPart.FurMid,false,12,7);
            skull.Tube(new[] {new Vector3(0,.15f,.10f),new Vector3(0,.51f,.06f),new Vector3(0,.98f,-.02f),new Vector3(0,1.12f,-.02f),new Vector3(0,1.25f,-.02f)},new[] {.43f,.39f,.51f,.49f,0f},ColorPart.FurMid,10);
            foreach(int side in new[] {-1,1})
            {
                skull.Ellipsoid(new Vector3(side*.43f,.25f,.46f),new Vector3(.087f,.064f,.075f),ColorPart.Pupil,false,8,4);
                skull.Ellipsoid(new Vector3(side*.472f,.25f,.513f),new Vector3(.038f,.040f,.034f),ColorPart.Eye,false,8,4);
                skull.Ellipsoid(new Vector3(side*.484f,.25f,.539f),new Vector3(.016f,.027f,.014f),ColorPart.Pupil,false,6,3);
                skull.Tuft(new Vector3(side*.43f,.37f,.44f),new Vector3(side*.46f,.21f,.56f),Vector3.right,Vector3.forward,.21f,ColorPart.FurDark);
                for(int j=0;j<3;j++) skull.Tuft(new Vector3(side*(.30f+j*.10f),.01f-j*.035f,.48f-j*.11f),new Vector3(side*(.28f+j*.12f),-.55f,.36f-j*.08f),Vector3.right,new Vector3(side*.5f,0,1),.24f,j%2==0 ? ColorPart.FurMid:ColorPart.FurLight);
                // Outer curled tusk with small barbs along its outer arc.
                skull.Curve(new[] {new Vector3(side*.36f,-.10f,.49f),new Vector3(side*.73f,-.36f,.92f),new Vector3(side*1.19f,-.25f,1.10f),new Vector3(side*1.39f,.16f,1.15f),new Vector3(side*1.20f,.59f,1.18f),new Vector3(side*.89f,.56f,1.24f),new Vector3(side*.77f,.27f,1.29f)},
                    new[] {.14f,.14f,.115f,.092f,.070f,.040f,0f},ColorPart.Tusk,38,9);
                for(int j=0;j<6;j++)
                {
                    float angle=-.40f+j*.51f;var n=new Vector3(side*Mathf.Cos(angle),Mathf.Sin(angle),0);
                    var start=new Vector3(side*(1.06f+.34f*Mathf.Cos(angle)),.12f+.49f*Mathf.Sin(angle),1.15f);
                    skull.Spike(start,start+n*.15f+Vector3.back*.025f,.043f,ColorPart.Tusk);
                }
                // A second, longer pair sweeps down on either side of the trunk.
                skull.Curve(new[] {new Vector3(side*.30f,-.13f,.59f),new Vector3(side*.35f,-.56f,.80f),new Vector3(side*.53f,-.91f,1.10f),new Vector3(side*.82f,-1.17f,1.38f),new Vector3(side*.95f,-1.56f,1.54f),new Vector3(side*.85f,-1.91f,1.61f)},
                    new[] {.105f,.090f,.073f,.050f,.027f,0f},ColorPart.Tusk,32,8);
            }
            Render(head,skull,"Mammoth_Head",materials);
            motion.ears=new Transform[2];
            for(int i=0;i<2;i++)
            {
                int side=i==0 ? -1:1;var ear=Bone(i==0 ? "LeftEar":"RightEar",head,new Vector3(side*.63f,.24f,-.08f));var shape=new Shape();
                shape.Ellipsoid(Vector3.zero,new Vector3(.29f,.40f,.15f),ColorPart.FurMid,false,10,5);
                shape.Ellipsoid(new Vector3(0,0,.11f),new Vector3(.235f,.32f,.055f),ColorPart.Ear,false,10,5);
                Render(ear,shape,i==0 ? "Mammoth_LeftEar":"Mammoth_RightEar",materials);motion.ears[i]=ear;
            }
            var trunk=Bone("Trunk",head,new Vector3(0,-.15f,.50f));motion.trunk=trunk;var trunkShape=new Shape();
            trunkShape.Curve(new[] {Vector3.zero,new Vector3(0,-.35f,.17f),new Vector3(0,-.75f,.27f),new Vector3(0,-1.14f,.30f),new Vector3(-.03f,-1.43f,.38f),new Vector3(-.12f,-1.55f,.60f),new Vector3(-.27f,-1.47f,.82f),new Vector3(-.40f,-1.27f,.94f),new Vector3(-.43f,-1.10f,.97f)},
                new[] {.23f,.215f,.185f,.16f,.14f,.12f,.105f,.090f,.085f},ColorPart.Trunk,36,10);
            trunkShape.Ellipsoid(new Vector3(-.43f,-1.066f,1.013f),new Vector3(.060f,.042f,.030f),ColorPart.Tip,false,8,4);
            Render(trunk,trunkShape,"Mammoth_Trunk",materials);
            var tail=Bone("Tail",torso,new Vector3(0,1.93f,-1.52f));motion.tail=tail;var tailShape=new Shape();
            tailShape.Curve(new[] {Vector3.zero,new Vector3(0,-.36f,-.09f),new Vector3(0,-.68f,-.10f)},new[] {.065f,.05f,.025f},ColorPart.Trunk,10,7);
            tailShape.Tuft(new Vector3(0,-.52f,-.08f),new Vector3(0,-.91f,-.15f),Vector3.right,Vector3.back,.19f,ColorPart.FurDark);
            Render(tail,tailShape,"Mammoth_Tail",materials);
            motion.upperLegs=new Transform[4];motion.lowerLegs=new Transform[4];motion.feet=new Transform[4];
            for(int i=0;i<4;i++)
            {
                int side=i%2==0 ? -1:1;bool front=i<2;string name=(front ? "Front":"Rear")+(side<0 ? "Left":"Right");
                var upper=Bone(name+"Leg",root.transform,new Vector3(side*.64f,1.35f,front ? .65f:-.91f));var leg=new Shape();
                leg.Ellipsoid(new Vector3(0,-.20f,0),new Vector3(.30f,.47f,.34f),ColorPart.Coat,false,10,6);
                for(int j=0;j<3;j++) leg.Tuft(new Vector3(side*.22f,.09f-j*.15f,-.16f+j*.14f),new Vector3(side*.28f,-.60f,-.12f+j*.14f),Vector3.forward,new Vector3(side,0,0),.25f,j%2==0 ? ColorPart.FurMid:ColorPart.FurLight);
                Render(upper,leg,"Mammoth_"+name+"Upper",materials);
                var lower=Bone("Lower",upper,new Vector3(0,-.60f,0));var shin=new Shape();
                shin.Tube(new[] {Vector3.zero,new Vector3(0,-.31f,.025f),new Vector3(0,-.60f,.04f)},new[] {.23f,.20f,.20f},ColorPart.Coat,9);
                Render(lower,shin,"Mammoth_"+name+"Lower",materials);
                var foot=Bone("Foot",lower,new Vector3(0,-.60f,.04f));var toes=new Shape();
                toes.Ellipsoid(new Vector3(0,-.015f,.025f),new Vector3(.26f,.135f,.29f),ColorPart.Coat,false,10,4);
                for(int toe=0;toe<4;toe++) toes.Ellipsoid(new Vector3((toe-1.5f)*.108f,-.067f,.235f),new Vector3(.052f,.083f,.060f),ColorPart.Hoof,false,6,4);
                Render(foot,toes,"Mammoth_"+name+"Foot",materials);motion.upperLegs[i]=upper;motion.lowerLegs[i]=lower;motion.feet[i]=foot;
            }
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Mammoth Model And Previews...")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "MammothModel.prefab")
                && !EditorUtility.DisplayDialog("Обновить модель мамонта?", "Сгенерированные меши, материалы и модельные префабы в Art/Models/Mammoth будут обновлены. Ручные изменения в них будут заменены; GUID существующих assets сохранятся. Игровая сцена этой командой не пересоздаётся.", "Обновить модель", "Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            foreach (bool enemy in new[] { false, true })
            {
                var root = Model(Palette(enemy));
                PrefabUtility.SaveAsPrefabAsset(root, Folder + (enemy ? "MammothEnemyModel.prefab" : "MammothModel.prefab"));
                ExportObj(root, Path.GetFullPath(Output + (enemy ? "Mammoth-enemy-lowpoly.obj" : "Mammoth-lowpoly.obj")));
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Preview();
        }
        public static void CreateAndApply() { Create(); ApplyToGame(); }
        [MenuItem("Prehistoric Frontiers/Art/Apply Mammoth Model To Battle")]
        public static void ApplyToGame()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "MammothModel.prefab");
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "MammothEnemyModel.prefab");
            if (!player || !enemy) throw new InvalidOperationException("Create the mammoth art assets before applying them.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string name in new[] { "Mammoth", "Enemy_Mammoth" })
            {
                string path = "Assets/PrehistoricFrontiers/Prefabs/Units/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Install(root.GetComponent<RTSUnit>(), name == "Mammoth" ? player : enemy); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity", OpenSceneMode.Single);
            var stats = AssetDatabase.LoadAssetAtPath<UnitStats>("Assets/PrehistoricFrontiers/Data/MammothStats.asset");
            foreach (var unit in UnityEngine.Object.FindObjectsByType<RTSUnit>())
                if (unit.stats == stats) Install(unit, unit.team == UnitTeam.Player ? player : enemy);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("MAMMOTH_ART_APPLIED_WITH_EXISTING_COLLIDERS");
        }
        static void Install(RTSUnit unit, GameObject model)
        {
            var visual = unit.transform.Find("Visual");
            if (!visual || !visual.GetComponent<CapsuleCollider>()) throw new InvalidOperationException("Existing selection collider missing on " + unit.name);
            visual.GetComponent<Renderer>().enabled = false;
            var existing = unit.GetComponentInChildren<MammothMotion>(true);
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
            var obj = new StringBuilder("# Prehistoric Frontiers stylized mammoth\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "MammothEnemyModel.prefab" : "MammothModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 2.35f;
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
                var positions = new[] { new Vector3(5, 3.6f, 7), new Vector3(7, 2.1f, .45f), new Vector3(4, 7, -6), new Vector3(0,2.1f,8), new Vector3(0,2.1f,-8) };
                var names = new[] { "Мамонт-превью.png", "Мамонт-профиль.png", "Мамонт-вид-сверху.png", "Мамонт-спереди.png", "Мамонт-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0, 1.90f, .40f));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Мамонт-", "Мамонт-враг-") : names[i])), snapshot.EncodeToPNG());
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
