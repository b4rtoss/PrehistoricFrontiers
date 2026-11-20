using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class WorkerMotion : MonoBehaviour
    {
        public Transform torso, head, tail;
        public Transform[] upperArms, forearms, upperLegs, lowerLegs, feet;
        RTSUnit unit;
        WorkerGatherer gatherer;
        WorkerBuilder builder;
        Vector3 torsoPosition;
        Quaternion headRotation, tailRotation;
        readonly Quaternion[] arms = new Quaternion[2], hands = new Quaternion[2], hips = new Quaternion[2], knees = new Quaternion[2], ankles = new Quaternion[2];
        float phase, offset;
        public bool IsWorking { get; private set; }
        void Awake()
        {
            unit = GetComponentInParent<RTSUnit>();
            gatherer = unit ? unit.GetComponent<WorkerGatherer>() : null;
            builder = unit ? unit.GetComponent<WorkerBuilder>() : null;
            torsoPosition = torso.localPosition; headRotation = head.localRotation; tailRotation = tail.localRotation;
            for(int i=0;i<2;i++) { arms[i]=upperArms[i].localRotation; hands[i]=forearms[i].localRotation; hips[i]=upperLegs[i].localRotation; knees[i]=lowerLegs[i].localRotation; ankles[i]=feet[i].localRotation; }
            offset = transform.position.x * .37f + transform.position.z * .19f;
        }
        void LateUpdate()
        {
            var controller = gatherer && gatherer.home ? gatherer.home.controller : null;
            if(!unit || !unit.IsAlive || controller && (controller.IsPaused || controller.MatchOver)) return;
            // Construction attaches WorkerBuilder when the worker receives its first building order.
            if(!builder) builder=unit.GetComponent<WorkerBuilder>();
            float speed=unit.Agent && unit.Agent.isOnNavMesh ? Mathf.Clamp01(unit.Agent.velocity.magnitude/Mathf.Max(.1f,unit.Agent.speed)):0;
            bool building=builder && builder.Site && !builder.Site.Complete && builder.Site.Health.IsAlive
                && !unit.Agent.pathPending && Vector3.Distance(unit.transform.position+Vector3.up,builder.Site.Health.SurfacePoint(unit.transform.position+Vector3.up))<=2.2f;
            // A job can begin while the agent is still reporting its final deceleration velocity.
            IsWorking=gatherer && gatherer.State==GatherState.Gathering || building;
            float work=IsWorking ? Mathf.Sin(Time.time*8+offset):0;
            phase+=Time.deltaTime*Mathf.Lerp(1,10,speed);
            float stride=Mathf.Sin(phase)*speed;
            float carry=gatherer && gatherer.Carried>0 && !IsWorking ? 1:0;
            torso.localPosition=torsoPosition+Vector3.up*(Mathf.Sin(Time.time*2+offset)*.006f+Mathf.Abs(stride)*.014f);
            for(int i=0;i<2;i++)
            {
                float side=i==0 ? 1:-1, step=stride*side;
                upperLegs[i].localRotation=hips[i]*Quaternion.Euler(step*21,0,0);
                lowerLegs[i].localRotation=knees[i]*Quaternion.Euler(Mathf.Max(0,-step)*25,0,0);
                feet[i].localRotation=ankles[i]*Quaternion.Euler(-step*10,0,0);
                upperArms[i].localRotation=arms[i]*Quaternion.Euler(-step*15-carry*22+(IsWorking ? -40+work*side*18:0),0,side*(IsWorking ? -8:0));
                forearms[i].localRotation=hands[i]*Quaternion.Euler(-carry*28+(IsWorking ? -35-work*side*18:0),0,0);
            }
            head.localRotation=headRotation*Quaternion.Euler(IsWorking ? 5+work*2:0,Mathf.Sin(Time.time+offset)*1.6f,0);
            tail.localRotation=tailRotation*Quaternion.Euler(0,Mathf.Sin(Time.time*2+offset)*5+stride*5,0);
        }
    }
}
