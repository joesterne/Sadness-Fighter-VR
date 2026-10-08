using UnityEngine;
namespace AfterHours
{
    // One wearable piece built onto the avatar. A top also decides the colour and length of the sleeves.
    [System.Serializable] public class OutfitLook { public string id; public GameObject group; public Material sleeve; public bool longSleeves; }

    // The player's reflection. A faceted figure stands on the far side of the lobby mirror and copies the head and hands,
    // mirrored across the glass, so it moves as a reflection would. There is no second camera: the room behind the glass
    // is a mirror-image copy of the lobby, and the figure's parent has a negative scale across the mirror plane.
    public class MirrorAvatar : MonoBehaviour
    {
        public GameDirector director;
        // Its local Z axis points out of the mirror into the lobby. Children are posed in this space, unreflected.
        public Transform reflection, head, neck, torso, legs;
        public Transform[] upperArm=new Transform[2], foreArm=new Transform[2], hand=new Transform[2], leg=new Transform[2];
        public Renderer[] skin;
        public Renderer[] sleeveUpper=new Renderer[2], sleeveFore=new Renderer[2];
        public GameObject[] hairStyles;
        public OutfitLook[] looks;
        public Material[] skins, hairs;
        public float maxDistance=14;
        float bodyYaw, width=1; bool hasYaw;
        const float Upper=.29f, Fore=.27f, NominalSpine=.61f, NominalHips=.9f;
        static readonly float[] Widths={.88f,1f,1.14f};

        void LateUpdate()
        {
            if(!director||!director.player)return;var p=director.player;
            var eye=transform.InverseTransformPoint(p.Head.position);
            bool show=eye.z>.12f&&eye.z<maxDistance;
            if(reflection.gameObject.activeSelf!=show)reflection.gameObject.SetActive(show);
            if(!show)return;
            float floor=transform.InverseTransformPoint(p.transform.position).y;
            Vector3? left=null,right=null;
            if(p.TryGetHand(0,out var lp,out _))left=transform.InverseTransformPoint(lp);
            if(p.TryGetHand(1,out var rp,out _))right=transform.InverseTransformPoint(rp);
            Pose(eye,Quaternion.Inverse(transform.rotation)*p.Head.rotation,floor,left,right,Time.deltaTime);
        }
        // Poses the reflection for a viewer at a given eye, without hands (editor previews).
        public void PoseFor(Vector3 eyeWorld,Quaternion headWorld,float floorWorld)
        {
            reflection.gameObject.SetActive(true);hasYaw=false;
            var eye=transform.InverseTransformPoint(eyeWorld);
            Pose(eye,Quaternion.Inverse(transform.rotation)*headWorld,floorWorld-transform.position.y,null,null,1);
        }
        void Pose(Vector3 eye,Quaternion headRot,float floor,Vector3? leftHand,Vector3? rightHand,float dt)
        {
            // The shoulders follow the head's heading, lagging a little and never more than 35 degrees behind.
            var look=headRot*Vector3.forward;look.y=0;
            if(look.sqrMagnitude>.01f)
            {
                float yaw=Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg;
                if(!hasYaw){bodyYaw=yaw;hasYaw=true;}
                float behind=Mathf.DeltaAngle(bodyYaw,yaw);if(Mathf.Abs(behind)>35)bodyYaw=yaw-Mathf.Sign(behind)*35;
                bodyYaw=Mathf.LerpAngle(bodyYaw,yaw,Mathf.Clamp01(dt*1.5f));
            }
            var body=Quaternion.Euler(0,bodyYaw,0);
            float eyeHeight=Mathf.Clamp(eye.y-floor,.9f,2.2f);
            var spineBase=new Vector3(eye.x,floor,eye.z)+body*new Vector3(0,0,-.09f);
            var neckBase=spineBase+Vector3.up*(eyeHeight-.17f);
            float hipHeight=Mathf.Max(.42f,eyeHeight-.78f);
            var hips=spineBase+Vector3.up*hipHeight;

            head.localPosition=eye+headRot*new Vector3(0,.03f,-.08f);head.localRotation=headRot;
            neck.localPosition=neckBase;neck.localRotation=body;
            var spine=neckBase-hips;var up=spine.normalized;
            var torsoRot=Quaternion.LookRotation(Vector3.ProjectOnPlane(body*Vector3.forward,up),up);
            torso.localPosition=(neckBase+hips)*.5f;torso.localRotation=torsoRot;torso.localScale=new Vector3(width,spine.magnitude/NominalSpine,1);
            legs.localPosition=new Vector3(hips.x,floor,hips.z);legs.localRotation=body;
            for(int i=0;i<2;i++)if(leg[i])leg[i].localScale=new Vector3(1,hipHeight/NominalHips,1);

            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                var shoulder=neckBase+torsoRot*new Vector3(side*.19f*width,-.07f,0);
                // Without a tracked hand the arm rests at the side.
                var target=(i==0?leftHand:rightHand)??shoulder+body*new Vector3(side*.07f,-.53f,.05f);
                var to=target-shoulder;float d=Mathf.Clamp(to.magnitude,.08f,Upper+Fore-.005f);
                var dir=to.sqrMagnitude>1e-6f?to.normalized:body*Vector3.down;
                // Elbows bend down, back and slightly out, as arms do.
                var perp=Vector3.ProjectOnPlane(body*new Vector3(side*.35f,-.6f,-.7f),dir);
                if(perp.sqrMagnitude<1e-6f)perp=Vector3.ProjectOnPlane(body*Vector3.back,dir);perp.Normalize();
                float cos=Mathf.Clamp((Upper*Upper+d*d-Fore*Fore)/(2*Upper*d),-1,1),sin=Mathf.Sqrt(1-cos*cos);
                var elbow=shoulder+dir*(Upper*cos)+perp*(Upper*sin);var wrist=shoulder+dir*d;
                upperArm[i].localPosition=shoulder;upperArm[i].localRotation=Quaternion.LookRotation(elbow-shoulder,perp);
                var forearm=wrist-elbow;
                foreArm[i].localPosition=elbow;foreArm[i].localRotation=Quaternion.LookRotation(forearm,Vector3.ProjectOnPlane(perp,forearm));
                hand[i].localPosition=wrist;hand[i].localRotation=foreArm[i].localRotation;
            }
        }
        public void Apply(WardrobeState state)
        {
            if(state==null)return;
            var skinMaterial=skins!=null&&skins.Length>0?skins[Mathf.Clamp(state.skin,0,skins.Length-1)]:null;
            if(skinMaterial&&skin!=null)foreach(var r in skin)if(r)r.sharedMaterial=skinMaterial;
            bool hat=state.head!="no-hat";
            if(hairStyles!=null)for(int i=0;i<hairStyles.Length;i++)
            {
                var style=hairStyles[i];if(!style)continue;style.SetActive(i==state.hair);
                var hairMaterial=hairs!=null&&hairs.Length>0?hairs[Mathf.Clamp(state.hairColour,0,hairs.Length-1)]:null;
                foreach(var r in style.GetComponentsInChildren<Renderer>(true)){if(hairMaterial)r.sharedMaterial=hairMaterial;if(r.name.StartsWith("Crown"))r.gameObject.SetActive(!hat);}
            }
            width=Widths[Mathf.Clamp(state.build,0,Widths.Length-1)];
            OutfitLook top=null;
            if(looks!=null)foreach(var look in looks)
            {
                bool on=look.id==state.top||look.id==state.head||look.id==state.neck||look.id==state.pin;
                if(look.group&&look.group.activeSelf!=on)look.group.SetActive(on);
                if(on&&look.sleeve)top=look;
            }
            for(int i=0;i<2;i++)
            {
                if(sleeveUpper[i]&&top!=null)sleeveUpper[i].sharedMaterial=top.sleeve;
                if(sleeveFore[i])sleeveFore[i].sharedMaterial=top!=null&&top.longSleeves?top.sleeve:skinMaterial;
            }
            if(torso)torso.localScale=new Vector3(width,torso.localScale.y==0?1:torso.localScale.y,1);
        }
        // True when a piece with this id is built onto the avatar and showing.
        public bool Wearing(string id){if(looks!=null)foreach(var l in looks)if(l.id==id)return l.group&&l.group.activeSelf;return false;}
    }
}
