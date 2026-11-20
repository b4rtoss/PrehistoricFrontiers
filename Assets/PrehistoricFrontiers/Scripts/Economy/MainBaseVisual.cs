using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class MainBaseVisual : MonoBehaviour
    {
        public bool enemy;
        public Transform[] banners;
        BuildingHealth health;
        float phase;
        void Awake() {health=GetComponentInParent<BuildingHealth>();}
        void Update()
        {
            if(!health || !health.IsAlive || !health.controller || health.controller.IsPaused || health.controller.MatchOver) return;
            phase+=Time.deltaTime;
            for(int i=0;i<banners.Length;i++) banners[i].localRotation=Quaternion.Euler(0,Mathf.Sin(phase*1.5f+i)*2,Mathf.Sin(phase+i)*.8f);
        }
    }
}
