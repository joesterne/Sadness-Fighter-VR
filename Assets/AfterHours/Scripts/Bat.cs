using UnityEngine;
namespace AfterHours
{
    // The bat, and every other tool on the rage room's wall (the hammer, the handsaw, the nail bat, the axe). While it is held,
    // any swing of the tip faster than a slow wave counts as a hit on the part the striking end touches, so short swings from a
    // seat count as fully as big ones. Pieces that have already flown are knocked along. A heavier tool (power 2) breaks a part
    // two steps at a time.
    [DefaultExecutionOrder(100)]
    public class Bat : MonoBehaviour
    {
        public RageRoom room;
        public Grabbable grabbable;
        // Along the bat from the grip: where the barrel starts, the tip, and how thick a swing counts as.
        public float barrelFrom=.28f, length=.85f, radius=.06f;
        // Where the striking end sits off the handle's line (an axe blade or a hammer face hangs to one side), and how many
        // steps of breaking one hit does.
        public Vector3 barrelOffset;
        public int power=1;
        public const float MinSpeed=1.1f, WhooshSpeed=2.4f;
        public float Speed{get;private set;}
        public Vector3 Velocity{get;private set;}
        Vector3 lastTip,lastBase;bool tracking;float nextWhoosh;bool whooshArmed=true;int jumps;
        public Vector3 Tip=>transform.TransformPoint(barrelOffset+new Vector3(0,0,length));
        public Vector3 BarrelStart=>transform.TransformPoint(barrelOffset+new Vector3(0,0,barrelFrom));
        void LateUpdate()
        {
            if(!grabbable||!grabbable.held||!room){tracking=false;Speed=0;Velocity=Vector3.zero;return;}
            // A teleport carries it across the room in one frame: that is not a swing, and nothing on the way is hit.
            if(grabbable.Jumps!=jumps){jumps=grabbable.Jumps;tracking=false;Velocity=Vector3.zero;}
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
        static readonly Collider[] touched=new Collider[96];
        void Touch(Vector3 from,Vector3 to)
        {
            int count=Physics.OverlapCapsuleNonAlloc(from,to,radius,touched,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=touched[i];var part=c.GetComponentInParent<Breakable>();
                if(part){if(!part.Broken)room.Hit(part,Speed,c.ClosestPoint(to),Velocity,this);continue;}
                // A piece already knocked loose belongs to the room, not the part: the swing knocks it along.
                var body=c.attachedRigidbody;if(body&&!body.isKinematic)room.Knock(body,Speed,c.ClosestPoint(to),Velocity,this);
            }
        }
    }
}
