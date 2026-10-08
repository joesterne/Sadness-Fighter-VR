using UnityEngine;
namespace AfterHours
{
    // The bat. While it is held, any swing of the tip faster than a slow wave counts as a hit on the part the barrel
    // touches, so short swings from a seat count as fully as big ones.
    [DefaultExecutionOrder(100)]
    public class Bat : MonoBehaviour
    {
        public RageRoom room;
        public Grabbable grabbable;
        // Along the bat from the grip: where the barrel starts, the tip, and how thick a swing counts as.
        public float barrelFrom=.28f, length=.85f, radius=.06f;
        public const float MinSpeed=1.1f, WhooshSpeed=2.4f;
        public float Speed{get;private set;}
        public Vector3 Velocity{get;private set;}
        Vector3 lastTip,lastBase;bool tracking;float nextWhoosh;bool whooshArmed=true;
        public Vector3 Tip=>transform.TransformPoint(0,0,length);
        public Vector3 BarrelStart=>transform.TransformPoint(0,0,barrelFrom);
        void LateUpdate()
        {
            if(!grabbable||!grabbable.held||!room){tracking=false;Speed=0;Velocity=Vector3.zero;return;}
            Vector3 tip=Tip,start=BarrelStart;float dt=Mathf.Max(Time.deltaTime,1e-4f);
            if(tracking)
            {
                // Smoothed over a few frames, so tracking jitter is never mistaken for a swing.
                var instant=(tip-lastTip)/dt;Velocity=Vector3.Lerp(Velocity,instant,1-Mathf.Exp(-dt/.03f));Speed=Velocity.magnitude;
                if(Speed>=MinSpeed){Touch(Vector3.Lerp(lastBase,start,.5f),Vector3.Lerp(lastTip,tip,.5f));Touch(start,tip);}
                if(Speed>WhooshSpeed&&whooshArmed&&Time.time>nextWhoosh){room.PlaySwing(tip,Speed);nextWhoosh=Time.time+.35f;whooshArmed=false;}
                if(Speed<WhooshSpeed*.5f)whooshArmed=true;
            }
            lastTip=tip;lastBase=start;tracking=true;
        }
        void Touch(Vector3 from,Vector3 to)
        {
            foreach(var c in Physics.OverlapCapsule(from,to,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                // A piece already knocked loose belongs to the room, not the part, and is skipped.
                var part=c.GetComponentInParent<Breakable>();if(!part||part.Broken)continue;
                room.Hit(part,Speed,c.ClosestPoint(to),Velocity);
            }
        }
    }
}
