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
    public static class BarracksModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/Barracks/";
        const string Output = "../";
        enum ColorPart { Grass, Rock, Dirt, Roof, Foam, Wood, Red, Orange, Gold, Dark, GrassLight, RockLight, DirtLight, Rope, WoodLight, Straw, Emblem }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[17];
            public Shape() { for (int i = 0; i < faces.Length; i++) faces[i] = new List<int>(); }
            void Triangle(Vector3 a, Vector3 b, Vector3 c, ColorPart material)
            {
                // Put the training yard on the right when viewing the entrance.
                a.x=-a.x;b.x=-b.x;c.x=-c.x;var swap=b;b=c;c=swap;
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
                    int next = (j + 1) % sides; var mat = bands && i % 2 == 1 ? ColorPart.Dirt : material;
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
                    var mat=water && j%4==0 ? ColorPart.DirtLight:material;
                    Triangle(center,P(j+1,.55f),P(j,.55f),mat);
                    Triangle(P(j,.55f),P(j+1,.55f),P(j+1,1),material);
                    Triangle(P(j,.55f),P(j+1,1),P(j,1),water && j%5==0 ? ColorPart.DirtLight:material);
                }
            }
            public void Box(Vector3 center,Vector3 size,ColorPart mat)
            {
                var h=size*.5f;
                Vector3 P(int i)=>center+new Vector3((i&1)==0 ? -h.x:h.x,(i&2)==0 ? -h.y:h.y,(i&4)==0 ? -h.z:h.z);
                void Face(int a,int b,int c,int d) {Triangle(P(a),P(b),P(c),mat);Triangle(P(a),P(c),P(d),mat);}
                Face(0,4,6,2);Face(1,3,7,5);Face(0,1,5,4);Face(2,6,7,3);Face(0,2,3,1);Face(4,5,7,6);
            }
            public static Vector3 RoofPoint(float u,float v)
            {
                float wave=Mathf.Cos(u*Mathf.PI*3)*.08f;
                return new Vector3(-.42f+u*1.36f,1.48f+.25f*(1-u*u)+.70f*(1-v)+.18f*Mathf.Sin(v*Mathf.PI)+wave*(1-v*.3f),-1.12f+v*1.94f);
            }
            public void Roof()
            {
                for(int i=0;i<12;i++) for(int j=0;j<6;j++)
                {
                    float u=-1+i/6f,v=j/6f;var a=RoofPoint(u,v);var b=RoofPoint(u+1/6f,v);var c=RoofPoint(u+1/6f,v+1/6f);var d=RoofPoint(u,v+1/6f);
                    var mat=(i+j)%5==0 ? ColorPart.Orange:ColorPart.Roof;
                    Triangle(a,c,b,mat);Triangle(a,d,c,ColorPart.Roof);var under=Vector3.down*.035f;
                    Triangle(a+under,b+under,c+under,ColorPart.Roof);Triangle(a+under,c+under,d+under,ColorPart.Roof);
                }
            }
            public void Ring(Vector3 center,Vector3 axis,float radius,float thickness,ColorPart mat)
            {
                var n=axis.normalized;var u=Vector3.Cross(n,Mathf.Abs(n.y)>.9f ? Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(n,u);
                var points=new Vector3[19];var radii=new float[19];
                for(int i=0;i<19;i++) {float a=i*Mathf.PI*2/18;points[i]=center+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;radii[i]=thickness;}
                Tube(points,radii,mat,5);
            }
            public void Banner()
            {
                var a=new Vector3(-.16f,0,0);var b=new Vector3(.16f,0,0);var c=new Vector3(.16f,-.53f,0);var d=new Vector3(0,-.39f,.018f);var e=new Vector3(-.16f,-.53f,0);
                void Face(Vector3 x,Vector3 y,Vector3 z,ColorPart mat) {Triangle(x,y,z,mat);Triangle(z,y,x,mat);}
                Face(a,d,b,ColorPart.Red);Face(b,d,c,ColorPart.Red);Face(a,e,d,ColorPart.Red);
                for(int i=-1;i<=1;i++)
                {
                    float x=i*.075f;Face(new Vector3(x-.018f,-.12f,.025f),new Vector3(x+.020f,-.12f,.025f),new Vector3(x*.5f,-.33f,.027f),ColorPart.Emblem);
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
            position.x=-position.x;var bone = new GameObject(name).transform; bone.SetParent(parent, false); bone.localPosition = position; bone.gameObject.layer = 13; return bone;
        }
        static void Render(Transform bone, Shape shape, string name, Material[] materials)
        {
            bone.gameObject.AddComponent<MeshFilter>().sharedMesh = shape.Save(name);
            bone.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        static Material[] Palette(bool enemy)
        {
            string[] colors={"#627D28","#70737B","#B17D32","#B34317","#D2EEEA","#805026","#B62113","#BE4C13","#D0A459","#352317","#8FA22E","#9499A1","#BD8C43","#D9B578","#A26831","#B88925","#F5D5A0"};
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
            var root=new GameObject("BarracksModel");var visual=root.AddComponent<BarracksVisual>();
            var terrain=Bone("Island and earth",root.transform,Vector3.zero);var ground=new Shape();
            ground.Tube(new[] {Vector3.zero,Vector3.up*.11f},new[] {2.06f,2.06f},ColorPart.Grass,24);ground.Disc(Vector3.up*.115f,2.06f,ColorPart.GrassLight);ground.Disc(Vector3.up*.121f,1.86f,ColorPart.Dirt,true);
            Render(terrain,ground,"Barracks_Ground",materials);
            var wall=Bone("Rock walls",root.transform,Vector3.zero);var rocks=new Shape();
            foreach(float x in new[] {-1.43f,.59f}) for(int j=0;j<4;j++)
            {
                float z=-.94f+j*.49f;
                rocks.Ellipsoid(new Vector3(x,.60f,z),new Vector3(.32f,.51f+(j%2)*.08f,.34f),j%3==0 ? ColorPart.RockLight:ColorPart.Rock,false,7,5);
                rocks.Ellipsoid(new Vector3(x,.99f,z),new Vector3(.27f,.37f,.30f),ColorPart.Rock,false,7,4);
            }
            rocks.Ellipsoid(new Vector3(-.42f,.61f,-1.03f),new Vector3(.96f,.52f,.27f),ColorPart.Rock,false,10,5);
            foreach(float x in new[] {-1.24f,.41f}) rocks.Ellipsoid(new Vector3(x,.55f,.78f),new Vector3(.35f,.46f,.31f),ColorPart.RockLight,false,7,5);
            for(int i=0;i<9;i++) {float a=i*Mathf.PI*2/9;rocks.Ellipsoid(new Vector3(Mathf.Cos(a)*1.86f,.17f,Mathf.Sin(a)*1.86f),new Vector3(.14f,.13f,.14f),ColorPart.Rock,false,6,3);}
            Render(wall,rocks,"Barracks_Rocks",materials);
            var roof=Bone("Stretched hide roof",root.transform,Vector3.zero);var hide=new Shape();hide.Roof();Render(roof,hide,"Barracks_HideRoof",materials);
            var frame=Bone("Timber and rope frame",root.transform,Vector3.zero);var timber=new Shape();
            for(int i=0;i<5;i++)
            {
                float u=-1+i*.5f;var points=new Vector3[9];var radii=new float[9];
                for(int j=0;j<9;j++) {points[j]=Shape.RoofPoint(u,j/8f)+Vector3.up*.045f;radii[j]=.067f;}
                timber.Tube(points,radii,ColorPart.WoodLight,7);
                for(int j=0;j<3;j++) {float v=.40f+j*.022f;var p=Shape.RoofPoint(u,v)+Vector3.up*.045f;var axis=Shape.RoofPoint(u,v+.02f)-Shape.RoofPoint(u,v);timber.Ring(p,axis,.080f,.015f,ColorPart.Rope);}
            }
            foreach(int edge in new[] {-1,1})
            {
                var points=new Vector3[13];var radii=new float[13];
                for(int i=0;i<13;i++) {points[i]=Shape.RoofPoint(-1+i/6f,edge<0 ? 0:1);radii[i]=.032f;}timber.Tube(points,radii,ColorPart.Gold,6);
            }
            timber.Curve(new[] {new Vector3(-1.45f,1.46f,.89f),new Vector3(-.42f,1.62f,.89f),new Vector3(.65f,1.46f,.89f)},new[] {.10f,.10f,.10f},ColorPart.Wood,18,8);
            foreach(float x in new[] {-1.21f,.40f})
            {
                timber.Tube(new[] {new Vector3(x,.12f,.60f),new Vector3(x,1.50f,.65f)},new[] {.075f,.085f},ColorPart.Wood,7);
                timber.Tube(new[] {new Vector3(x,.82f,.62f),new Vector3(-.42f,1.59f,.68f)},new[] {.048f,.048f},ColorPart.Wood,7);
            }
            foreach(float x in new[] {-.98f,-.42f,.15f}) for(int j=0;j<3;j++) timber.Ring(new Vector3(x+j*.025f,1.57f,.89f),Vector3.right,.118f,.016f,ColorPart.Rope);
            Render(frame,timber,"Barracks_Frame",materials);
            var interior=Bone("Straw bunks",root.transform,Vector3.zero);var beds=new Shape();
            foreach(float x in new[] {-.98f,.08f}) foreach(float z in new[] {-.65f,.06f})
            {
                beds.Box(new Vector3(x,.27f,z),new Vector3(.39f,.22f,.48f),ColorPart.Dark);beds.Ellipsoid(new Vector3(x,.40f,z),new Vector3(.225f,.08f,.265f),ColorPart.Straw,false,8,3);
                for(int j=0;j<5;j++) beds.Spike(new Vector3(x-.16f+j*.08f,.41f,z+.17f),new Vector3(x-.16f+j*.08f,.42f,z+.28f),.022f,ColorPart.Straw);
            }
            Render(interior,beds,"Barracks_Bunks",materials);
            visual.banners=new Transform[2];
            for(int i=0;i<2;i++)
            {
                float x=i==0 ? -1.21f:.40f;var banner=Bone("Banner "+i,root.transform,new Vector3(x,1.28f,1.01f));var flag=new Shape();flag.Banner();Render(banner,flag,"Barracks_Banner"+i,materials);visual.banners[i]=banner;
            }
            var details=Bone("Banners rack and training yard",root.transform,Vector3.zero);var yard=new Shape();
            foreach(float x in new[] {-1.21f,.40f})
            {
                yard.Tube(new[] {new Vector3(x-.23f,1.32f,1.0f),new Vector3(x+.23f,1.32f,1.0f)},new[] {.036f,.036f},ColorPart.WoodLight,7);
                foreach(float dx in new[] {-.105f,.105f}) yard.Ring(new Vector3(x+dx,1.32f,1.0f),Vector3.right,.046f,.010f,ColorPart.Rope);
            }
            foreach(float y in new[] {.35f,.90f}) yard.Tube(new[] {new Vector3(.96f,y,-.62f),new Vector3(.96f,y,.52f)},new[] {.040f,.040f},ColorPart.WoodLight,7);
            foreach(float z in new[] {-.49f,-.03f,.43f})
            {
                yard.Ring(new Vector3(1.005f,.72f,z),Vector3.right,.105f,.022f,ColorPart.Wood);
                yard.Tube(new[] {new Vector3(.97f,.35f,z),new Vector3(1.015f,.35f,z)},new[] {.165f,.165f},ColorPart.WoodLight,12);
                yard.Ellipsoid(new Vector3(1.035f,.35f,z),new Vector3(.020f,.04f,.04f),ColorPart.Gold,false,6,3);
            }
            yard.Tube(new[] {new Vector3(.88f,.57f,1.02f),new Vector3(1.52f,.57f,1.02f)},new[] {.135f,.135f},ColorPart.Wood,10);
            yard.Tube(new[] {new Vector3(1.02f,.57f,1.02f),new Vector3(1.40f,.57f,1.02f)},new[] {.144f,.144f},ColorPart.Orange,10);
            foreach(float x in new[] {1.04f,1.38f})
            {
                yard.Ring(new Vector3(x,.57f,1.02f),Vector3.right,.15f,.014f,ColorPart.Rope);
                yard.Tube(new[] {new Vector3(x,.12f,.80f),new Vector3(x,.46f,1.02f),new Vector3(x,.12f,1.24f)},new[] {.043f,.043f,.043f},ColorPart.Wood,6);
            }
            for(int j=0;j<7;j++)
            {
                float z=-.80f+j*.18f;yard.Tube(new[] {new Vector3(1.69f,.12f,z),new Vector3(1.69f,.68f,z),new Vector3(1.69f,.78f,z)},new[] {.055f,.055f,0f},ColorPart.Wood,6);
            }
            yard.Tube(new[] {new Vector3(1.65f,.38f,-.80f),new Vector3(1.65f,.38f,.28f)},new[] {.038f,.038f},ColorPart.Wood,6);
            foreach(float z in new[] {.25f,.59f})
            {
                var start=new Vector3(1.51f,.13f,z);var end=start+new Vector3(.11f,.45f,-.15f);yard.Tube(new[] {start,end},new[] {.065f,.065f},ColorPart.WoodLight,6);yard.Ring(Vector3.Lerp(start,end,.65f),end-start,.075f,.018f,ColorPart.Rope);
            }
            Render(details,yard,"Barracks_Details",materials);
            var trees=Bone("Autumn trees and grass",root.transform,Vector3.zero);var plants=new Shape();
            foreach(float x in new[] {-1.43f,1.43f})
            {
                var p=new Vector3(x,.11f,-1.27f);plants.Tube(new[] {p,p+new Vector3(0,1.30f,0)},new[] {.10f,.065f},ColorPart.Wood,7);
                plants.Tube(new[] {p+Vector3.up*.70f,p+new Vector3(.23f,1.18f,0)},new[] {.06f,.025f},ColorPart.Wood,6);
                plants.Ellipsoid(p+new Vector3(0,1.43f,0),new Vector3(.39f,.39f,.35f),ColorPart.Red,false,7,4);
                plants.Ellipsoid(p+new Vector3(.19f,1.25f,.16f),new Vector3(.28f,.28f,.26f),ColorPart.Orange,false,7,4);
                plants.Ellipsoid(p+new Vector3(-.12f,1.72f,-.05f),new Vector3(.31f,.30f,.29f),ColorPart.Red,false,7,4);
            }
            for(int j=0;j<8;j++) {float a=j*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a)*1.94f,.12f,Mathf.Sin(a)*1.94f);for(int blade=0;blade<3;blade++) plants.Spike(p,p+new Vector3((blade-1)*.09f,.19f+blade*.02f,.04f),.06f,ColorPart.GrassLight);}
            Render(trees,plants,"Barracks_Trees",materials);
            foreach(var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=13;
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Barracks Model And Previews...")]
        static void CreateFromMenu()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"BarracksModel.prefab") && !EditorUtility.DisplayDialog("Обновить модель казармы?","Модельные assets будут обновлены с сохранением GUID. Ручные изменения сгенерированной модели заменятся.","Обновить","Отмена")) return;
            Create();
        }
        public static void Create()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var root=Model(Palette(false));
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"BarracksModel.prefab");ExportObj(root,Path.GetFullPath(Output+"Barracks-lowpoly.obj"));UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();Preview();
        }
        public static void CreateAndApply() {Create();ApplyToGame();}
        [MenuItem("Prehistoric Frontiers/Art/Apply Barracks Model To Battle")]
        public static void ApplyToGame()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"BarracksModel.prefab");if(!model) throw new InvalidOperationException("Generate barracks art first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity",OpenSceneMode.Single);
            var system=UnityEngine.Object.FindAnyObjectByType<ConstructionSystem>();if(!system) throw new InvalidOperationException("Construction system missing.");
            system.barracksModelPrefab=model;EditorUtility.SetDirty(system);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("BARRACKS_MODEL_ASSIGNED_TO_CONSTRUCTION");
        }
        static void ExportObj(GameObject root, string path)
        {
            var obj = new StringBuilder("# Prehistoric Frontiers low poly barracks\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "BarracksModel.prefab" : "BarracksModel.prefab")), scene);
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
                var positions = new[] { new Vector3(-4,4.4f,6),new Vector3(-6,2.9f,0),new Vector3(0,7,.01f),new Vector3(0,3.2f,6),new Vector3(0,3.2f,-6) };
                var names = new[] { "Казарма-превью.png", "Казарма-профиль.png", "Казарма-вид-сверху.png", "Казарма-спереди.png", "Казарма-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0,.82f,0));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("Казарма-", "Казарма-враг-") : names[i])), snapshot.EncodeToPNG());
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
