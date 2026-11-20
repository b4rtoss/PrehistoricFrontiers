using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ClinicVisual : MonoBehaviour
    {
        public Transform[] ripples, droplets;
        public Transform foam;
        public bool Operational { get; private set; }
        ConstructionSite site;
        float phase;
        void Awake() { site = GetComponentInParent<ConstructionSite>(); }
        public void SetOperational(bool value) { Operational = value; }
        void Update()
        {
            if(!Operational || !site || !site.Health.IsAlive || site.system.controller.IsPaused || site.system.controller.MatchOver) return;
            phase += Time.deltaTime;
            for(int i=0;i<ripples.Length;i++)
            {
                float size=1+Mathf.Sin(phase*2-i*.8f)*.055f;
                ripples[i].localScale=new Vector3(size,1,size);
            }
            for(int i=0;i<droplets.Length;i++)
            {
                float t=1-Mathf.Repeat(phase*.8f+i/3f,1);
                droplets[i].localPosition=new Vector3(.32f+(i-1)*.065f,.31f+t*1.20f,Mathf.Lerp(-.73f,-1.01f,t));
            }
            if(foam) foam.localScale=Vector3.one*(1+Mathf.Sin(phase*4)*.035f);
        }
    }
}
