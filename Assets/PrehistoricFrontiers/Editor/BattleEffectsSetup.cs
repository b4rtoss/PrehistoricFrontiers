using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace PrehistoricFrontiers.Editor
{
    public static class BattleEffectsSetup
    {
        const string Root="Assets/PrehistoricFrontiers/";
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);Folder(parent);AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
        }
        public static void ApplyAndBuild()
        {
            EditorSceneManager.OpenScene(Root+"Scenes/Battle.unity",OpenSceneMode.Single);
            ApplyToOpenScene();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            FoundationBuilder.Build();
        }
        public static void ApplyToOpenScene()
        {
            Folder(Root+"Art/Models/Effects");Folder(Root+"Art/Materials");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"Art/Models/Effects/Particle.asset");
            var vertices=new[] { Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f };
            var faces=new[]{0,4,3,0,2,4,0,5,2,0,3,5,1,3,4,1,4,2,1,2,5,1,5,3};
            var points=new Vector3[faces.Length];var indices=new int[faces.Length];
            for(int i=0;i<faces.Length;i++){points[i]=vertices[faces[i]];indices[i]=i;}
            var generated=new Mesh{name="Effect shard",vertices=points,triangles=indices};generated.RecalculateNormals();generated.RecalculateBounds();
            if(!mesh){mesh=generated;AssetDatabase.CreateAsset(mesh,Root+"Art/Models/Effects/Particle.asset");}
            else {EditorUtility.CopySerialized(generated,mesh);Object.DestroyImmediate(generated);EditorUtility.SetDirty(mesh);}
            var shader=Shader.Find("Particles/Standard Unlit");if(!shader)throw new InvalidOperationException("Particle shader is unavailable");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Art/Materials/BattleParticles.mat");
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,Root+"Art/Materials/BattleParticles.mat");}
            material.shader=shader;material.SetColor("_Color",Color.white);material.mainTexture=Texture2D.whiteTexture;
            material.SetFloat("_Mode",2);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite",0);material.SetOverrideTag("RenderType","Transparent");material.DisableKeyword("_ALPHATEST_ON");material.EnableKeyword("_ALPHABLEND_ON");material.renderQueue=3000;
            EditorUtility.SetDirty(material);
            var c=Object.FindAnyObjectByType<RTSSelectionController>();var effects=c.GetComponent<BattleEffects>() ?? c.gameObject.AddComponent<BattleEffects>();
            effects.material=material;effects.particleMesh=mesh;EditorUtility.SetDirty(effects);
            foreach(var light in Object.FindObjectsByType<Light>())if(light.type==LightType.Directional)
            {
                light.shadows=LightShadows.Soft;light.shadowResolution=LightShadowResolution.VeryHigh;light.shadowStrength=.8f;
                light.shadowBias=.025f;light.shadowNormalBias=.12f;light.shadowNearPlane=.1f;EditorUtility.SetDirty(light);
            }
            foreach(var renderer in Object.FindObjectsByType<MeshRenderer>())if(renderer.enabled)
            {renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;EditorUtility.SetDirty(renderer);}
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var quality=settings.FindProperty("m_QualitySettings").GetArrayElementAtIndex(QualitySettings.GetQualityLevel());
            quality.FindPropertyRelative("shadows").intValue=(int)ShadowQuality.All;
            quality.FindPropertyRelative("shadowResolution").intValue=(int)ShadowResolution.VeryHigh;
            quality.FindPropertyRelative("shadowDistance").floatValue=70;
            quality.FindPropertyRelative("shadowCascades").intValue=4;
            quality.FindPropertyRelative("shadowCascade4Split").vector3Value=new Vector3(.15f,.4f,.7f);
            settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(settings.targetObject);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
