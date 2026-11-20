using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class BarracksVisual : MonoBehaviour
    {
        public Transform[] banners;
        public bool Operational { get; private set; }
        ConstructionSite site;
        float phase;
        void Awake() {site=GetComponentInParent<ConstructionSite>();}
        public void SetOperational(bool value) {Operational=value;}
        void Update()
        {
            if(!Operational || !site || !site.Health.IsAlive || site.system.controller.IsPaused || site.system.controller.MatchOver) return;
            phase+=Time.deltaTime;
            for(int i=0;i<banners.Length;i++) banners[i].localRotation=Quaternion.Euler(0,Mathf.Sin(phase*1.8f+i)*2.5f,Mathf.Sin(phase*1.3f+i)*1.2f);
        }
    }
}
