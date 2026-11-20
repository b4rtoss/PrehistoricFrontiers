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
    public static class MainBaseModelBuilder
    {
        const string Folder = "Assets/PrehistoricFrontiers/Art/Models/MainBase/";
        const string Output = "../";
        enum ColorPart { Grass, Rock, Dirt, Roof, Foam, Wood, Red, Orange, Gold, Dark, GrassLight, RockLight, DirtLight, Rope, WoodLight, Straw, Emblem, Water, Purple }
        sealed class Shape
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int>[] faces = new List<int>[19];
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
            public static Vector3 RoofPoint(float u,float v,bool enemy)
            {
                float y=enemy ? 1.36f+1.03f*(1-Mathf.Abs(u))+.06f*Mathf.Sin(v*Mathf.PI)
                    : 1.42f+.43f*(1-u*u)+.34f*(1-v)+.13f*Mathf.Sin(v*Mathf.PI)+.08f*Mathf.Cos(u*Mathf.PI*3);
                return new Vector3(u*1.51f,y,-1.12f+v*1.63f);
            }
            public void Roof(bool enemy)
            {
                for(int i=0;i<12;i++) for(int j=0;j<6;j++)
                {
                    float u=-1+i/6f,v=j/6f;var a=RoofPoint(u,v,enemy);var b=RoofPoint(u+1/6f,v,enemy);var c=RoofPoint(u+1/6f,v+1/6f,enemy);var d=RoofPoint(u,v+1/6f,enemy);
                    var mat=(i*3+j*5)%9==0 ? ColorPart.Orange:ColorPart.Roof;
                    Triangle(a,c,b,mat);Triangle(a,d,c,ColorPart.Roof);var under=Vector3.down*.035f;
                    Triangle(a+under,b+under,c+under,ColorPart.Roof);Triangle(a+under,c+under,d+under,ColorPart.Roof);
                }
            }
            public void Awning(float x)
            {
                Vector3 P(int i,int j) {float u=-1+i/2f,v=j/4f;return new Vector3(x+u*.36f,1.04f+.24f*(1-v)+.10f*(1-u*u)+.07f*Mathf.Sin(v*Mathf.PI),-.21f+v*.91f);}
                for(int i=0;i<4;i++) for(int j=0;j<4;j++) {var a=P(i,j);var b=P(i+1,j);var c=P(i+1,j+1);var d=P(i,j+1);Triangle(a,c,b,ColorPart.Orange);Triangle(a,d,c,ColorPart.Roof);Triangle(a,b,c,ColorPart.Roof);Triangle(a,c,d,ColorPart.Roof);}
            }
            public void Symbol(Vector3 center,float scale,bool enemy,bool floor=false)
            {
                Vector3 P(float x,float y) => center+(floor ? new Vector3(x*scale,0,-y*scale):new Vector3(x*scale,y*scale,0));
                void Face(float ax,float ay,float bx,float by,float cx,float cy) {var a=P(ax,ay);var b=P(bx,by);var c=P(cx,cy);Triangle(a,b,c,ColorPart.Emblem);Triangle(c,b,a,ColorPart.Emblem);}
                if(enemy)
                {
                    Face(-.19f,.16f,.19f,.16f,.09f,.06f);Face(-.19f,.16f,.09f,.06f,-.09f,.06f);
                    Face(-.19f,.16f,-.09f,.06f,-.14f,-.20f);Face(.19f,.16f,.14f,-.20f,.09f,.06f);
                }
                else for(int i=-1;i<=1;i++) {float x=i*.105f;Face(x-.035f,-.05f,x+.028f,-.05f,i*.19f,.20f+(i==0 ? .09f:0));Face(x-.035f,-.05f,0,-.20f,x+.028f,-.05f);}
            }
            public void Ring(Vector3 center,Vector3 axis,float radius,float thickness,ColorPart mat)
            {
                var n=axis.normalized;var u=Vector3.Cross(n,Mathf.Abs(n.y)>.9f ? Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(n,u);
                var points=new Vector3[19];var radii=new float[19];
                for(int i=0;i<19;i++) {float a=i*Mathf.PI*2/18;points[i]=center+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;radii[i]=thickness;}
                Tube(points,radii,mat,5);
            }
            public void Banner(bool enemy)
            {
                var a=new Vector3(-.15f,0,0);var b=new Vector3(.15f,0,0);var c=new Vector3(.15f,-.50f,0);var d=new Vector3(0,-.38f,.012f);var e=new Vector3(-.15f,-.50f,0);
                void Face(Vector3 x,Vector3 y,Vector3 z,ColorPart mat) {Triangle(x,y,z,mat);Triangle(z,y,x,mat);}
                Face(a,d,b,ColorPart.Red);Face(b,d,c,ColorPart.Red);Face(a,e,d,ColorPart.Red);Symbol(new Vector3(0,-.21f,.025f),.63f,enemy);
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
            var bone = new GameObject(name).transform; bone.SetParent(parent, false); bone.localPosition = position; bone.gameObject.layer = 13; return bone;
        }
        static void Render(Transform bone, Shape shape, string name, Material[] materials)
        {
            bone.gameObject.AddComponent<MeshFilter>().sharedMesh = shape.Save(name);
            bone.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        static Material[] Palette(bool enemy)
        {
            string[] colors=enemy ? new[] {"#756526","#595456","#80562C","#64252D","#D2EEEA","#422D20","#69252C","#853137","#A75B30","#281C17","#A08B32","#777072","#946337","#BA9663","#65432C","#A77529","#DFC49A","#3A9B9F","#725071"}
                : new[] {"#66822B","#7E8385","#B7823E","#C45119","#D2EEEA","#9D612B","#C63D13","#D96819","#D4A158","#4D301A","#94A634","#9A9C9C","#C89046","#D9B578","#B47936","#B88925","#F5D5A0","#59BDC0","#784787"};
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
        static GameObject Model(Material[] materials,bool enemy)
        {
            string prefix=enemy ? "EnemyBase_":"PlayerBase_";var root=new GameObject(enemy ? "EnemyBaseModel":"PlayerBaseModel");
            var visual=root.AddComponent<MainBaseVisual>();visual.enemy=enemy;
            void Part(string name,Shape shape) {Render(Bone(name,root.transform,Vector3.zero),shape,prefix+name.Replace(" ",""),materials);}
            var ground=new Shape();ground.Tube(new[] {Vector3.zero,Vector3.up*.10f},new[] {2.14f,2.14f},ColorPart.Grass,24);ground.Disc(Vector3.up*.105f,2.14f,ColorPart.GrassLight);ground.Disc(Vector3.up*.112f,1.97f,ColorPart.Dirt,true);
            var island=Bone("Island",root.transform,Vector3.zero);island.localScale=new Vector3(1,1,.78f);Render(island,ground,prefix+"Island",materials);
            var stone=new Shape();
            foreach(float x in new[] {-1.12f,1.12f}) for(int j=0;j<4;j++) stone.Ellipsoid(new Vector3(x,.63f,-.90f+j*.42f),new Vector3(.28f,.53f,.31f),j%3==0 ? ColorPart.RockLight:ColorPart.Rock,false,7,5);
            stone.Ellipsoid(new Vector3(0,.61f,-1.02f),new Vector3(1.10f,.51f,.24f),ColorPart.Rock,false,12,5);
            foreach(float x in new[] {-.65f,.65f}) {stone.Ellipsoid(new Vector3(x,.63f,.61f),new Vector3(.27f,.54f,.28f),ColorPart.Rock,false,7,5);stone.Ellipsoid(new Vector3(x,.27f,.77f),new Vector3(.29f,.22f,.22f),ColorPart.RockLight,false,7,4);}
            for(int j=0;j<16;j++) {float a=j*Mathf.PI/8;stone.Ellipsoid(new Vector3(Mathf.Cos(a)*2.02f,.13f,Mathf.Sin(a)*1.51f),new Vector3(.13f,.12f,.11f),ColorPart.Rock,false,6,3);}
            Part("Stone walls",stone);
            var roof=new Shape();roof.Roof(enemy);if(!enemy) {roof.Awning(-1.52f);roof.Awning(1.52f);}else roof.Awning(1.50f);Part("Hide roofs",roof);
            var timber=new Shape();
            for(int i=0;i<5;i++)
            {
                float u=-1+i*.5f;var points=new Vector3[9];var radii=new float[9];
                for(int j=0;j<9;j++) {points[j]=Shape.RoofPoint(u,j/8f,enemy)+Vector3.up*.04f;radii[j]=enemy ? .075f:.060f;}
                timber.Tube(points,radii,ColorPart.WoodLight,7);var p=Shape.RoofPoint(u,.58f,enemy)+Vector3.up*.04f;
                timber.Ring(p,Shape.RoofPoint(u,.60f,enemy)-Shape.RoofPoint(u,.58f,enemy),enemy ? .089f:.075f,.014f,ColorPart.Rope);
            }
            if(enemy)
            {
                foreach(float v in new[] {.05f,1.01f}) timber.Tube(new[] {Shape.RoofPoint(-1,v,true),Shape.RoofPoint(0,v,true)+Vector3.up*.08f,Shape.RoofPoint(1,v,true)},new[] {.095f,.095f,.095f},ColorPart.Wood,8);
            }
            timber.Tube(new[] {new Vector3(-.91f,1.43f,.72f),new Vector3(.91f,1.43f,.72f)},new[] {.105f,.105f},ColorPart.WoodLight,8);
            foreach(float x in new[] {-.66f,.66f})
            {
                timber.Tube(new[] {new Vector3(x,.12f,.51f),new Vector3(x,1.42f,.61f)},new[] {.065f,.075f},ColorPart.Wood,7);
                timber.Tube(new[] {new Vector3(x,.78f,.55f),new Vector3(0,1.42f,.61f)},new[] {.045f,.045f},ColorPart.Wood,6);
            }
            foreach(float x in new[] {-.65f,0,.65f}) timber.Ring(new Vector3(x,1.43f,.72f),Vector3.right,.12f,.019f,ColorPart.Rope);
            foreach(float x in enemy ? new[] {1.18f,1.85f}:new[] {-1.85f,-1.18f,1.18f,1.85f})
                timber.Tube(new[] {new Vector3(x,.12f,.63f),new Vector3(x,1.10f,.63f)},new[] {.05f,.06f},ColorPart.WoodLight,7);
            for(int j=0;j<5;j++) timber.Tube(new[] {new Vector3(-.80f+j*.40f,.12f,-.81f),new Vector3(-.80f+j*.40f,1.21f,-.81f)},new[] {.04f,.04f},ColorPart.Wood,6);
            Part("Timber frame",timber);
            var emblem=new Shape();emblem.Tube(new[] {new Vector3(0,1.83f,.69f),new Vector3(0,1.83f,.77f)},new[] {.34f,.34f},enemy ? ColorPart.Gold:ColorPart.WoodLight,enemy ? 6:18);
            emblem.Tube(new[] {new Vector3(0,1.83f,.775f),new Vector3(0,1.83f,.79f)},new[] {.294f,.294f},enemy ? ColorPart.Dark:ColorPart.Red,enemy ? 6:18);emblem.Symbol(new Vector3(0,1.83f,.795f),.93f,enemy);Part("Entrance emblem",emblem);
            visual.banners=new Transform[2];
            for(int i=0;i<2;i++) {float x=i==0 ? -.65f:.65f;var flag=Bone("Banner "+i,root.transform,new Vector3(x,1.22f,.91f));var shape=new Shape();shape.Banner(enemy);Render(flag,shape,prefix+"Banner"+i,materials);visual.banners[i]=flag;}
            var courtyard=new Shape();courtyard.Tube(new[] {new Vector3(0,.115f,.99f),new Vector3(0,.16f,.99f)},new[] {.49f,.49f},ColorPart.Rock,16);courtyard.Disc(new Vector3(0,.166f,.99f),.41f,ColorPart.Red);courtyard.Symbol(new Vector3(0,.172f,.99f),.83f,enemy,true);
            Part("Courtyard medallion",courtyard);
            var watch=new Shape();var pos=enemy ? new Vector3(-.87f,2.30f,-.65f):new Vector3(0,2.14f,-.53f);
            if(!enemy)
            {
                watch.Tube(new[] {pos,pos+Vector3.up*.06f},new[] {.48f,.48f},ColorPart.Rock,12);
                for(int j=0;j<6;j++) {float a=j*Mathf.PI/3;var p=pos+new Vector3(Mathf.Cos(a)*.37f,.06f,Mathf.Sin(a)*.37f);watch.Tube(new[] {p,p+Vector3.up*.49f},new[] {.042f,.042f},ColorPart.WoodLight,6);}
                watch.Ring(pos+Vector3.up*.30f,Vector3.up,.39f,.027f,ColorPart.WoodLight);
                watch.Tube(new[] {pos+Vector3.up*.58f,pos+Vector3.up*.66f,pos+Vector3.up*.89f,pos+Vector3.up*.98f},new[] {.55f,.38f,.16f,0f},ColorPart.Orange,12);
            }
            else
            {
                watch.Box(pos,new Vector3(.86f,.10f,.75f),ColorPart.Wood);
                foreach(float dx in new[] {-.33f,.33f}) foreach(float dz in new[] {-.27f,.27f}) {var p=pos+new Vector3(dx,.05f,dz);watch.Tube(new[] {p,p+Vector3.up*.52f},new[] {.04f,.04f},ColorPart.WoodLight,6);watch.Tube(new[] {p-Vector3.up*.06f,p+new Vector3(-dx*.5f,-.86f,0)},new[] {.055f,.055f},ColorPart.Wood,6);}
                foreach(float dz in new[] {-.28f,.28f}) watch.Tube(new[] {pos+new Vector3(-.34f,.31f,dz),pos+new Vector3(.34f,.31f,dz)},new[] {.025f,.025f},ColorPart.WoodLight,6);
                watch.Tube(new[] {pos+Vector3.up*.59f,pos+Vector3.up*.86f},new[] {.63f,0f},ColorPart.Roof,4);
            }
            Part("Lookout tower",watch);
            var supplies=new Shape();
            foreach(float x in new[] {-.88f,-1.16f}) for(int j=0;j<2;j++) supplies.Tube(new[] {new Vector3(x,.22f+j*.12f,.83f),new Vector3(x,.22f+j*.12f,1.29f)},new[] {.07f,.07f},ColorPart.WoodLight,8);
            for(int j=0;j<6;j++) supplies.Ellipsoid(new Vector3(-1.47f+(j%2)*.17f,.22f+(j/2)*.09f,.95f+(j%3)*.12f),new Vector3(.12f,.11f,.12f),ColorPart.Rock,false,6,3);
            foreach(float z in new[] {.71f,1.40f}) supplies.Tube(new[] {new Vector3(-1.72f,.20f,z),new Vector3(-.69f,.20f,z)},new[] {.035f,.035f},ColorPart.Wood,6);
            if(!enemy)
            {
                for(int i=0;i<3;i++) {float x=.80f+(i%2)*.32f,z=1.00f+(i/2)*.25f;supplies.Tube(new[] {new Vector3(x,.12f,z),new Vector3(x,.34f,z)},new[] {.16f,.18f},ColorPart.WoodLight,10);for(int j=0;j<5;j++) {float a=j*Mathf.PI*2/5;supplies.Ellipsoid(new Vector3(x+Mathf.Cos(a)*.10f,.36f,z+Mathf.Sin(a)*.10f),Vector3.one*.065f,i==0 ? ColorPart.Red:(i==1 ? ColorPart.Gold:ColorPart.Purple),false,6,3);}}
                supplies.Tube(new[] {new Vector3(1.48f,.12f,.21f),new Vector3(1.48f,.39f,.21f)},new[] {.07f,.07f},ColorPart.Wood,8);supplies.Tube(new[] {new Vector3(1.48f,.39f,.21f),new Vector3(1.48f,.44f,.21f)},new[] {.25f,.25f},ColorPart.WoodLight,12);
                foreach(float dx in new[] {-.18f,0,.18f}) supplies.Spike(new Vector3(1.48f+dx,.46f,.21f),new Vector3(1.48f+dx,.53f,.21f),.04f,ColorPart.Gold);
            }
            else foreach(int side in new[] {-1,1})
                for(int j=0;j<7;j++) {var p=new Vector3(side*1.65f,.12f,-.76f+j*.20f);supplies.Tube(new[] {p,p+Vector3.up*.72f,p+Vector3.up*.85f},new[] {.068f,.068f,0f},ColorPart.Wood,7);if(j%2==0) supplies.Ring(p+Vector3.up*.49f,Vector3.up,.078f,.016f,ColorPart.Rope);}
            float waterX=enemy ? 1.48f:-1.48f;
            for(int j=0;j<10;j++) {float a=j*Mathf.PI/5;supplies.Ellipsoid(new Vector3(waterX+Mathf.Cos(a)*.23f,.23f,.22f+Mathf.Sin(a)*.23f),new Vector3(.095f,.11f,.085f),ColorPart.Rock,false,6,3);}
            supplies.Disc(new Vector3(waterX,.26f,.22f),.18f,ColorPart.Water);Part("Supplies and water",supplies);
            var plants=new Shape();
            foreach(float x in new[] {-1.72f,1.72f})
            {
                var p=new Vector3(x,.10f,-.89f);plants.Curve(new[] {p,p+new Vector3(.03f,.65f,0),p+new Vector3(-.05f,1.18f,0)},new[] {.075f,.06f,.04f},ColorPart.Wood,10,6);
                if(enemy)
                {
                    foreach(float dx in new[] {-.16f,.16f}) {plants.Tube(new[] {p+Vector3.up*.63f,p+new Vector3(dx,1.18f,.04f)},new[] {.042f,.02f},ColorPart.Wood,6);plants.Ellipsoid(p+new Vector3(dx,1.26f,.04f),new Vector3(.30f,.13f,.24f),ColorPart.Gold,false,7,3);}
                }
                else {plants.Ellipsoid(p+new Vector3(-.06f,1.32f,0),new Vector3(.32f,.36f,.28f),ColorPart.Red,false,7,4);plants.Ellipsoid(p+new Vector3(.10f,1.11f,.08f),new Vector3(.25f,.25f,.24f),ColorPart.Orange,false,7,4);plants.Ellipsoid(p+new Vector3(-.06f,1.56f,0),new Vector3(.26f,.27f,.25f),ColorPart.Red,false,7,4);}
            }
            for(int j=0;j<10;j++) {float a=j*Mathf.PI/5;var p=new Vector3(Mathf.Cos(a)*2.04f,.11f,Mathf.Sin(a)*1.51f);for(int blade=0;blade<3;blade++) plants.Spike(p,p+new Vector3((blade-1)*.07f,.17f,.04f),.05f,ColorPart.GrassLight);}
            Part("Autumn trees and grass",plants);
            foreach(var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=11;
            return root;
        }
        [MenuItem("Prehistoric Frontiers/Art/Generate Main Bases And Previews...")]
        static void CreateFromMenu() {if(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PlayerBaseModel.prefab") && !EditorUtility.DisplayDialog("Обновить модели баз?","Модельные assets будут обновлены с сохранением GUID. Ручные изменения сгенерированных моделей заменятся.","Обновить","Отмена")) return;Create();}
        public static void Create()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            foreach(bool enemy in new[] {false,true}) {var root=Model(Palette(enemy),enemy);PrefabUtility.SaveAsPrefabAsset(root,Folder+(enemy ? "EnemyBaseModel.prefab":"PlayerBaseModel.prefab"));ExportObj(root,Path.GetFullPath(Output+(enemy ? "EnemyBase-lowpoly.obj":"PlayerBase-lowpoly.obj")));UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Preview();
        }
        public static void CreateAndApply() {Create();ApplyToGame();}
        static void Install(BuildingHealth health,GameObject model)
        {
            foreach(string name in new[] {"Stone foundation","Longhouse","Thatched roof"}) {var old=health.transform.Find(name);if(old) foreach(var renderer in old.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;}
            var existing=health.GetComponentInChildren<MainBaseVisual>(true);
            if(existing) {if(PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject)==model) return;UnityEngine.Object.DestroyImmediate(existing.gameObject);}
            var art=(GameObject)PrefabUtility.InstantiatePrefab(model,health.transform);art.transform.localPosition=Vector3.zero;art.transform.localRotation=Quaternion.Euler(0,180,0);art.transform.localScale=Vector3.one;
        }
        [MenuItem("Prehistoric Frontiers/Art/Apply Main Bases To Battle")]
        public static void ApplyToGame()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PlayerBaseModel.prefab");var enemy=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"EnemyBaseModel.prefab");if(!player || !enemy) throw new InvalidOperationException("Generate the base models first.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach(string name in new[] {"MainBuilding","EnemyMainBuilding"}) {string path="Assets/PrehistoricFrontiers/Prefabs/Buildings/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try {Install(root.GetComponent<BuildingHealth>(),name=="MainBuilding" ? player:enemy);PrefabUtility.SaveAsPrefabAsset(root,path);}finally {PrefabUtility.UnloadPrefabContents(root);}}
            var scene=EditorSceneManager.OpenScene("Assets/PrehistoricFrontiers/Scenes/Battle.unity",OpenSceneMode.Single);var controller=UnityEngine.Object.FindAnyObjectByType<RTSSelectionController>();Install(controller.playerBase,player);Install(controller.enemyBase,enemy);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("MAIN_BASE_MODELS_APPLIED_WITH_EXISTING_HEALTH_AND_COLLIDERS");
        }
        static void ExportObj(GameObject root, string path)
        {
            var obj = new StringBuilder("# Prehistoric Frontiers low poly main base\nmtllib " + Path.GetFileNameWithoutExtension(path) + ".mtl\n");
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
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (enemy ? "EnemyBaseModel.prefab" : "PlayerBaseModel.prefab")), scene);
                var cameraObject = new GameObject("Art camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 2.65f;
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
                var names = new[] { "База-превью.png", "База-профиль.png", "База-вид-сверху.png", "База-спереди.png", "База-сзади.png" };
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i]; camera.transform.LookAt(new Vector3(0,1.04f,0));
                    var previous = RenderTexture.active; var target = new RenderTexture(1200, 900, 24);
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    var snapshot = new Texture2D(1200, 900, TextureFormat.RGB24, false); snapshot.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); snapshot.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Output + (enemy ? names[i].Replace("База-", "База-враг-") : names[i])), snapshot.EncodeToPNG());
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
