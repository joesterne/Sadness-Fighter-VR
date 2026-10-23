using UnityEngine;
namespace AfterHours
{
    // The machete in The NDA web. Like the bat, any swing of the blade faster than a slow wave cuts every web the blade passes through,
    // so short chops from a seat count as fully as big ones. It is put in the player's hand on arrival, and either hand can take it.
    [DefaultExecutionOrder(100)]
    public class Machete : MonoBehaviour
    {
        public WebRoom room;
        public Grabbable grabbable;
        // Along the machete from the grip: where the blade starts, its tip, and how thick a swing counts as.
        public float bladeFrom=.13f, length=.74f, radius=.06f;
        public const float MinSpeed=1.1f, WhooshSpeed=2.4f;
        public float Speed{get;private set;}
        public Vector3 Velocity{get;private set;}
        Vector3 lastTip,lastBase;bool tracking;float nextWhoosh;bool whooshArmed=true;
        public Vector3 Tip=>transform.TransformPoint(0,0,length);
        public Vector3 BladeStart=>transform.TransformPoint(0,0,bladeFrom);
        void LateUpdate()
        {
            if(!grabbable||!grabbable.held||!room){tracking=false;Speed=0;Velocity=Vector3.zero;return;}
            Vector3 tip=Tip,start=BladeStart;float dt=Mathf.Max(Time.deltaTime,1e-4f);
            if(tracking)
            {
                // Smoothed over a few frames, so tracking jitter is never mistaken for a swing.
                var instant=(tip-lastTip)/dt;Velocity=Vector3.Lerp(Velocity,instant,1-Mathf.Exp(-dt/.03f));Speed=Velocity.magnitude;
                if(Speed>=MinSpeed){Touch(Vector3.Lerp(lastBase,start,.5f),Vector3.Lerp(lastTip,tip,.5f));Touch(start,tip);}
                if(Speed>WhooshSpeed&&whooshArmed&&Time.time>nextWhoosh){room.PlaySwing(tip,Speed);nextWhoosh=Time.time+.3f;whooshArmed=false;}
                if(Speed<WhooshSpeed*.5f)whooshArmed=true;
            }
            lastTip=tip;lastBase=start;tracking=true;
        }
        static readonly Collider[] touched=new Collider[64];
        // Webs are triggers, so they are looked for on purpose.
        void Touch(Vector3 from,Vector3 to)
        {
            int count=Physics.OverlapCapsuleNonAlloc(from,to,radius,touched,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
            for(int i=0;i<count;i++){var web=touched[i].GetComponentInParent<Cobweb>();if(web&&!web.cut)room.Cut(web,Speed,touched[i].ClosestPoint(to));}
        }
    }
}
