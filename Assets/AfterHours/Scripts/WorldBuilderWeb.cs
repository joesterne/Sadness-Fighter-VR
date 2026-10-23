using System.Collections.Generic;
using UnityEngine;
namespace AfterHours
{
    // The eighth chapter's room: a dusty records store with a ring of cobwebs round the seat, a machete, and a spider the size of a person in the far corner.
    // Anything that moves or switches on and off while playing is built under one group (WebRoom.changing), which the scene's merging leaves alone.
    // See WebRoom for how it plays.
    public static partial class WorldBuilder
    {
        // The ring of webs: nine columns from 120 degrees left of straight ahead to 120 degrees right, each a web low and a web high, at this distance from the seat.
        // The gap behind the seat is closed by a curtain of silk that cannot be cut. Every web is within a short chop of a seated hand.
        const int WebColumns=9;const float WebRadius=.72f,WebLow=.95f,WebHigh=1.62f,WebSize=.66f,CutWidth=.52f;
        // One web as a single see-through mesh with its hub at the origin, facing -z: spokes out to an uneven rim, and a spiral of threads between them.
        static Mesh WebMesh(string name,float size,int seed,bool ragged)
        {
            var random=new System.Random(seed);float R()=>(float)random.NextDouble();
            var threads=new List<CombineInstance>();
            void Thread(Vector3 a,Vector3 b,float width){if((b-a).sqrMagnitude<1e-6f)return;Piece(threads,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a,Vector3.up));}
            int spokes=ragged?7:8;var rim=new Vector3[spokes];
            for(int k=0;k<spokes;k++)
            {
                float a=(k+(R()-.5f)*.35f)*Mathf.PI*2/spokes,reach=size*.5f*(.82f+R()*.2f);
                rim[k]=new Vector3(Mathf.Cos(a)*reach,Mathf.Sin(a)*reach,(R()-.5f)*.03f);
                Thread(Vector3.zero,rim[k],.006f);
            }
            for(int k=0;k<spokes;k++)Thread(rim[k],rim[(k+1)%spokes],.007f);
            // The spiral: rings of threads between the spokes, each sagging a little toward the hub.
            foreach(float f in new[]{.2f,.34f,.48f,.62f,.76f,.9f})
                for(int k=0;k<spokes;k++)
                {
                    if(ragged&&R()<.25f)continue;
                    var a=rim[k]*f;var b=rim[(k+1)%spokes]*f;var mid=(a+b)*.5f*.93f;
                    Thread(a,mid,.0045f);Thread(mid,b,.0045f);
                }
            var mesh=new Mesh{name=name};mesh.CombineMeshes(threads.ToArray(),true,true);mesh.RecalculateBounds();return mesh;
        }
        static GameObject Silk(string name,Mesh mesh,Transform parent)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent?parent:Art.Root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=Art.Silk;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go;
        }
        // A web that hangs on its own somewhere in the room, for the look of the place. It cannot be cut.
        static void HangingWeb(string name,Vector3 at,Vector3 facing,float size,int seed)
        {
            var g=Art.Group(name,at);g.localRotation=Quaternion.LookRotation(facing,Vector3.up);
            Silk(name+" silk",WebMesh("NDA web - "+name+" "+seed,size,seed,true),g);
        }
        // The spider: a person-sized body on eight long legs, facing +z. Nothing about it is solid, so it never stands in the way of a pointer.
        static Transform Spider(WebRoom room,Vector3 at,Transform parent)
        {
            var root=Art.Group("Spider",at,parent);
            var body=new List<Renderer>();var eyes=new List<Renderer>();var legs=new List<Transform>();
            Renderer Part(GameObject g){var r=g.GetComponent<Renderer>();body.Add(r);return r;}
            Part(Art.Shape("Spider abdomen",PrimitiveType.Sphere,new Vector3(0,.82f,-.62f),new Vector3(1f,.82f,1.2f),Art.SpiderBody,false,root));
            Part(Art.Shape("Spider abdomen mark",PrimitiveType.Sphere,new Vector3(0,1.18f,-.6f),new Vector3(.36f,.1f,.5f),Art.Navy,false,root));
            Part(Art.Shape("Spider body",PrimitiveType.Sphere,new Vector3(0,.66f,.2f),new Vector3(.68f,.5f,.72f),Art.SpiderBody,false,root));
            Part(Art.Shape("Spider head",PrimitiveType.Sphere,new Vector3(0,.72f,.5f),new Vector3(.46f,.36f,.34f),Art.SpiderBody,false,root));
            for(int s=-1;s<=1;s+=2)
            {
                Part(Art.Facet("Spider fang",new Vector3(s*.08f,.47f,.6f),new Vector3(.07f,.16f,.07f),Art.Ink,5,root)).transform.localRotation=Quaternion.Euler(180,0,s*10);
                Part(Art.Shape("Spider feeler",PrimitiveType.Capsule,new Vector3(s*.16f,.52f,.66f),new Vector3(.05f,.12f,.05f),Art.SpiderBody,false,root)).transform.localRotation=Quaternion.Euler(60,0,0);
            }
            // Eight eyes that glow red: two big ones in front, two beside them, and four small ones above.
            var eyeSpots=new[]{(-.09f,.76f,.66f,.12f),(.09f,.76f,.66f,.12f),(-.2f,.73f,.6f,.08f),(.2f,.73f,.6f,.08f),(-.06f,.87f,.62f,.055f),(.06f,.87f,.62f,.055f),(-.16f,.86f,.57f,.05f),(.16f,.86f,.57f,.05f)};
            foreach(var (x,y,z,size) in eyeSpots)eyes.Add(Art.Shape("Spider eye",PrimitiveType.Sphere,new Vector3(x,y,z),Vector3.one*size,Art.SpiderEye,false,root).GetComponent<Renderer>());
            // Four legs a side. Each leg turns from a hip on the body, so it can curl up when the spider burns; its local +x points away from the body.
            for(int s=-1;s<=1;s+=2)for(int k=0;k<4;k++)
            {
                float spread=new[]{50f,18f,-14f,-46f}[k];
                var hip=Art.Group("Spider leg",new Vector3(s*.26f,.68f,.25f-k*.16f),root);hip.localRotation=Quaternion.Euler(0,s>0?-spread:180+spread,0);
                var knee=new Vector3(.62f,.62f,0);var foot=new Vector3(1.2f+.1f*(k==0||k==3?1:0),-.68f,0);
                Part(Art.Line("Spider thigh",Vector3.zero,knee,.075f,Art.SpiderBody,hip));
                Part(Art.Shape("Spider knee",PrimitiveType.Sphere,knee,Vector3.one*.1f,Art.SpiderBody,false,hip));
                Part(Art.Line("Spider shin",knee,foot,.05f,Art.SpiderBody,hip));
                Part(Art.Shape("Spider foot",PrimitiveType.Sphere,foot,Vector3.one*.06f,Art.Ink,false,hip));
                legs.Add(hip);
            }
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            room.spider=root;room.legs=legs.ToArray();room.eyes=eyes.ToArray();room.spiderBody=body.ToArray();room.charred=Art.Ink;
            return root;
        }
        // The machete: a dark grip, a brass guard and a long blade along +z. Its grip is at the origin, where the hand holds it.
        static Grabbable MacheteTool(WebRoom room,Vector3 at,Transform parent)
        {
            var m=Art.Group("Machete",at,parent);m.localRotation=Quaternion.Euler(-80,30,0);
            GameObject Bar(string name,float z,float length,Vector3 size,Material material)=>Art.Box(name,new Vector3(0,0,z),new Vector3(size.x,size.y,length),material,false,m,.004f);
            Bar("Machete grip",-.02f,.15f,new Vector3(.036f,.03f,0),Art.Ink);
            Bar("Machete pommel",-.1f,.02f,new Vector3(.044f,.04f,0),Art.Gold);
            Bar("Machete guard",.06f,.025f,new Vector3(.1f,.04f,0),Art.Gold);
            Bar("Machete blade",.4f,.62f,new Vector3(.006f,.07f,0),Art.Metal);
            Bar("Machete spine",.4f,.62f,new Vector3(.012f,.012f,0),Art.Navy).transform.localPosition=new Vector3(0,.035f,.4f);
            Art.Box("Machete tip",new Vector3(0,.012f,.73f),new Vector3(.006f,.05f,.06f),Art.Metal,false,m,.002f).transform.localRotation=Quaternion.Euler(-30,0,0);
            var box=m.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,0,.33f);box.size=new Vector3(.06f,.09f,.88f);
            var body=m.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=m.gameObject.AddComponent<Grabbable>();grab.label="The machete";grab.room=WebRoom.Room;grab.tool=true;room.machete=grab;
            var blade=m.gameObject.AddComponent<Machete>();blade.room=room;blade.grabbable=grab;
            return grab;
        }
        static void Web()
        {
            var room=d.rooms[WebRoom.Room].root.AddComponent<WebRoom>();room.director=d;d.web=room;
            var seat=d.rooms[WebRoom.Room].spawn.localPosition;
            // ----- The room: a records store after hours, with filing cabinets of everything agreed and never said.
            Shell(10,11,Art.Ink);
            Header(WebRoom.Room,"08 / THE NDA WEB","Cut yourself free.","Everything you agreed never to say has spun itself around you. Swing through the webs.",new Vector3(-.6f,3.55f,5.85f),7);
            for(int side=-1;side<=1;side+=2)for(int k=0;k<4;k++)
            {
                float z=-2.6f+k*1.6f;var at=new Vector3(side*4.62f,0,z);
                Art.Box("Filing cabinet",at+Vector3.up*.66f,new Vector3(.66f,1.32f,.5f),Art.Navy);
                for(int drawer=0;drawer<3;drawer++)
                {
                    Art.Box("Cabinet drawer",at+new Vector3(-side*.335f,.25f+drawer*.42f,0),new Vector3(.02f,.36f,.44f),Art.Ink,false);
                    Art.Box("Drawer handle",at+new Vector3(-side*.35f,.36f+drawer*.42f,0),new Vector3(.02f,.025f,.14f),Art.Gold,false);
                }
                var label=Art.Text("Drawer label",k%2==0?"CONFIDENTIAL":"DO NOT DISCUSS",at+new Vector3(-side*.35f,1.18f,0),.06f,ivory,.42f,null,false);
                label.transform.localRotation=Quaternion.Euler(0,side*90,0);
            }
            Art.Box("Records shelf",new Vector3(-2.2f,1.1f,5.7f),new Vector3(3.2f,2.2f,.5f),Art.Navy);
            for(int k=0;k<14;k++)Art.Box("Archive box",new Vector3(-3.55f+k*.21f,k%2==0?.62f:1.52f,5.5f),new Vector3(.19f,.3f,.36f),k%3==0?Art.Paper:Art.Cream,false,null,.01f);
            // A bare bulb on a cord above the seat.
            Art.Line("Bulb cord",new Vector3(seat.x,4.58f,seat.z+.4f),new Vector3(seat.x,2.75f,seat.z+.4f),.012f,Art.Ink);
            Art.Shape("Bulb",PrimitiveType.Sphere,new Vector3(seat.x,2.7f,seat.z+.4f),Vector3.one*.1f,Art.Lantern,false);
            // Old webs in the corners and across the shelf, for the look of the place.
            HangingWeb("Corner web",new Vector3(-4.6f,3.9f,5.6f),new Vector3(1,-.4f,-1),1.6f,91);
            HangingWeb("Corner web",new Vector3(4.6f,3.9f,5.6f),new Vector3(-1,-.4f,-1),1.8f,92);
            HangingWeb("Corner web",new Vector3(-4.6f,3.9f,-4.6f),new Vector3(1,-.4f,1),1.4f,93);
            HangingWeb("Corner web",new Vector3(4.6f,3.9f,-4.6f),new Vector3(-1,-.4f,1),1.4f,94);
            HangingWeb("Shelf web",new Vector3(-1.2f,2.4f,5.4f),new Vector3(0,0,1),1.1f,95);
            // ----- The room's moving parts.
            var changing=Art.Group("Moving parts",Vector3.zero);room.changing=changing;
            // The ring of webs, each facing the seat, with a clause written across the low one.
            var webs=new List<Cobweb>();
            for(int col=0;col<WebColumns;col++)
            {
                float angle=Mathf.Lerp(-120,120,col/(WebColumns-1f));var way=Quaternion.Euler(0,angle,0)*Vector3.forward;
                for(int tier=0;tier<2;tier++)
                {
                    int index=col*2+tier;
                    var at=seat+way*WebRadius;at.y=tier==0?WebLow:WebHigh;
                    var g=Art.Group("Web "+(index+1),at,changing);g.localRotation=Quaternion.LookRotation(way,Vector3.up);
                    var web=g.gameObject.AddComponent<Cobweb>();web.room=room;web.index=index;
                    // The cut zone is narrower and thinner than the silk: neighbouring silks overlap at their edges, and a full-size zone
                    // let one chop through the middle of a column cut the columns either side of it too.
                    var volume=g.gameObject.AddComponent<BoxCollider>();volume.isTrigger=true;volume.size=new Vector3(CutWidth,WebSize,.2f);web.volume=volume;
                    var silk=Silk("Web silk",WebMesh("NDA web "+(index+1),WebSize,index*7+3,false),g);web.silk=silk.transform;
                    // The clause is written on the low web, below every line of sight to the room's heading.
                    if(tier==0)Art.Text("Web clause",WebRoom.Clauses[col],new Vector3(0,-.04f,-.01f),.045f,ivory,WebSize*.8f,silk.transform,false);
                    webs.Add(web);
                }
            }
            room.webs=webs.ToArray();d.rooms[WebRoom.Room].total=room.webs.Length;
            // Behind the seat the ring closes with a curtain of silk, too high and too far back to need cutting.
            for(int k=0;k<3;k++)
            {
                float angle=150+k*30;var way=Quaternion.Euler(0,angle,0)*Vector3.forward;var at=seat+way*1.05f;at.y=1.35f;
                var g=Art.Group("Silk curtain",at,changing);g.localRotation=Quaternion.LookRotation(way,Vector3.up);
                Silk("Curtain silk",WebMesh("NDA web curtain "+k,1.3f,60+k,true),g);
            }
            // The machete's hook, at the right hand behind the seat. The machete is put in your hand on arrival, and only rests here before a hand is found.
            var hook=seat+Quaternion.Euler(0,140,0)*Vector3.forward*.42f;
            Art.Shape("Machete stand",PrimitiveType.Cylinder,new Vector3(hook.x,.015f,hook.z),new Vector3(.2f,.015f,.2f),Art.Navy);
            Art.Box("Machete post",new Vector3(hook.x,.45f,hook.z),new Vector3(.03f,.9f,.03f),Art.Metal);
            MacheteTool(room,new Vector3(hook.x,.2f,hook.z),changing);
            // The spider, in the far right corner, turned toward the seat, with the ash it would leave.
            var corner=new Vector3(3.55f,0,4.45f);var spider=Spider(room,corner,changing);
            var toSeat=seat-corner;toSeat.y=0;spider.localRotation=Quaternion.LookRotation(toSeat.normalized,Vector3.up);
            var ash=Art.Group("Spider ash",corner,changing);
            Art.Shape("Ash heap",PrimitiveType.Sphere,new Vector3(0,.04f,0),new Vector3(.8f,.1f,.7f),Art.Ink,false,ash);
            for(int k=0;k<5;k++){float a=k*1.3f;Art.Shape("Ash lump",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.3f,.06f,Mathf.Sin(a)*.25f),Vector3.one*.12f,Art.Navy,false,ash);}
            Art.Line("Curled leg",new Vector3(-.2f,.05f,.1f),new Vector3(-.35f,.22f,.25f),.04f,Art.Ink,ash);Art.Line("Curled leg",new Vector3(.2f,.05f,-.1f),new Vector3(.38f,.2f,-.2f),.04f,Art.Ink,ash);
            ash.gameObject.SetActive(false);room.ash=ash.gameObject;
            // A web strung across the corner above the spider.
            HangingWeb("Spider's web",corner+new Vector3(.3f,2.6f,.3f),new Vector3(-1,-.2f,-1),2.2f,97);
            // The button nobody labelled: a plain plate on the left wall with a red button on it. It does what it looks like it should not.
            var plate=Art.Group("Unlabelled button",new Vector3(-4.88f,1.5f,1.4f),changing);plate.localRotation=Quaternion.Euler(0,-90,0);
            Art.Box("Button plate",new Vector3(0,0,.02f),new Vector3(.32f,.32f,.04f),Art.Metal,true,plate,.006f);
            var press=Art.Shape("Button",PrimitiveType.Cylinder,new Vector3(0,0,-.02f),new Vector3(.17f,.03f,.17f),Art.Coral,true,plate);press.transform.localRotation=Quaternion.Euler(90,0,0);
            var fire=press.AddComponent<Interactable>();fire.kind=ActionKind.KillItWithFire;fire.director=d;fire.prompt="";
            // ----- Silk thrown from a cut, the fire and the smoke after it.
            var sphere=Geometry.Get(PrimitiveType.Sphere);var cube=Geometry.Get(PrimitiveType.Cube);
            var fade=new AnimationCurve(new Keyframe(0,1),new Keyframe(.6f,.8f),new Keyframe(1,0));
            room.silk=BlastSystem("Silk bits",changing,cube,new[]{Art.White.color,new Color(.82f,.86f,.88f)},24,new Vector2(.6f,1.2f),new Vector2(.4f,1.3f),.12f,.35f,1.2f,new Vector2(.008f,.02f),fade);
            // Silk is thrown where each web is cut (WebRoom emits it), never in one burst from where the system stands.
            var silkEmission=room.silk.emission;silkEmission.SetBursts(new ParticleSystem.Burst[0]);var silkMain=room.silk.main;silkMain.maxParticles=200;
            room.flames=BlastSystem("Fire",changing,sphere,new[]{Art.Flame.color,Art.FlameCore.color,Art.Coral.color,Art.Orange.color},180,new Vector2(.5f,1.3f),new Vector2(1.2f,3.6f),.7f,-.5f,1.6f,new Vector2(.12f,.32f),fade);
            room.smoke=BlastSystem("Smoke",changing,sphere,new[]{new Color(.22f,.22f,.25f),new Color(.32f,.31f,.33f)},40,new Vector2(1.6f,2.8f),new Vector2(.2f,.7f),.4f,-.12f,.8f,new Vector2(.18f,.4f),fade);
            room.swingSound=SoundBank.Get("SFX - Slash");room.cutSound=SoundBank.Get("SFX - Web cut");room.growSound=SoundBank.Get("SFX - Web grow");
            room.fireSound=SoundBank.Get("SFX - Fire");room.hissSound=SoundBank.Get("SFX - Hiss");room.calmSound=SoundBank.Get("SFX - Calm");
            Sound("Ambience - Records room",.5f);
        }
    }
}
