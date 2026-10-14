using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The seventh chapter's room: a boardroom with a very long table, two giant clowns on the far side of it, and everything that a meeting like that leaves on the table.
    // Anything that moves, changes colour or switches on and off while playing is built under one group (AmbushRoom.changing), which the scene's merging leaves alone.
    // Everything else is ordinary scenery. See AmbushRoom for how it plays.
    public static partial class WorldBuilder
    {
        // One clown's solid parts, gathered by material and merged at the end: a whole clown is a handful of draws and moves, leans and scales as one.
        sealed class ClownParts
        {
            public readonly Dictionary<Material,List<CombineInstance>> by=new Dictionary<Material,List<CombineInstance>>();
            public void Add(PrimitiveType type,Vector3 at,Vector3 size,Vector3 euler,Material material,float bevel=.012f)
            {
                if(!by.TryGetValue(material,out var list)){list=new List<CombineInstance>();by[material]=list;}
                list.Add(new CombineInstance{mesh=Geometry.Get(type,size,bevel),transform=Matrix4x4.TRS(at,Quaternion.Euler(euler),size)});
            }
            public void Cube(Vector3 at,Vector3 size,Material material,Vector3 euler=default)=>Add(PrimitiveType.Cube,at,size,euler,material);
            public void Ball(Vector3 at,Vector3 size,Material material)=>Add(PrimitiveType.Sphere,at,size,Vector3.zero,material);
            // A cylinder by its width and height (a Unity cylinder is two units tall at scale one).
            public void Tube(Vector3 at,float diameter,float height,Material material,Vector3 euler=default)=>Add(PrimitiveType.Cylinder,at,new Vector3(diameter,height*.5f,diameter),euler,material,.01f);
        }
        static void MergeClown(ClownParts parts,Transform root,string who)
        {
            foreach(var pair in parts.by)
            {
                var mesh=new Mesh{name="Ambush clown "+who+" - "+pair.Key.name};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                var go=new GameObject("Clown "+who+" - "+pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        // A clown is drawn at the size of a person, facing +z, and the room scales and leans it as it plays (see AmbushRoom.Place).
        // The first clown is angry and the second worried; both smile exactly the same smile. Only the nose, the balloons and the chest target can be pointed at.
        static AmbushRoom.Giant AmbushClown(int index,Transform parent)
        {
            string who=index==0?"A":"B";float inner=index==0?-1:1;
            Material jacket=index==0?Art.Coral:Art.Teal,pants=index==0?Art.Teal:Art.Gold,hair=index==0?Art.Gold:Art.Coral,trim=index==0?Art.Gold:Art.Coral,paint=index==0?Art.Teal:Art.Coral,cap=index==0?Art.Coral:Art.Gold;
            var root=Art.Group("Clown "+who,new Vector3((index==0?-1:1)*2.6f,0,5.5f),parent);
            var p=new ClownParts();
            for(int s=-1;s<=1;s+=2)
            {
                p.Cube(new Vector3(s*.18f,.085f,.1f),new Vector3(.27f,.17f,.56f),Art.Ink);                    // shoe
                p.Ball(new Vector3(s*.18f,.095f,.38f),new Vector3(.27f,.19f,.3f),Art.Ink);                    // round toe
                p.Cube(new Vector3(s*.17f,.55f,0),new Vector3(.29f,.76f,.31f),pants);                         // leg
                p.Cube(new Vector3(s*.45f,1.27f,.02f),new Vector3(.18f,.66f,.2f),jacket);                     // sleeve
                p.Tube(new Vector3(s*.46f,.97f,.02f),.2f,.07f,Art.White);                                     // cuff
                p.Ball(new Vector3(s*.47f,.86f,.03f),Vector3.one*.25f,Art.White);                             // glove
                p.Ball(new Vector3(s*.37f,2.2f,-.03f),Vector3.one*.34f,hair);                                 // hair puff
                p.Cube(new Vector3(s*.115f,2.1f,.245f),new Vector3(.13f,.13f,.02f),paint,new Vector3(0,0,45)); // painted diamond round the eye
                p.Ball(new Vector3(s*.115f,2.1f,.262f),new Vector3(.07f,.085f,.03f),Art.Ink);                // pupil
                p.Cube(new Vector3(s*.115f,2.26f,.235f),new Vector3(.17f,.035f,.03f),Art.Ink,new Vector3(0,0,(index==0?18:-18)*s)); // brow: angry, or worried
                p.Tube(new Vector3(s*.205f,1.97f,.185f),.14f,.03f,Art.Coral,new Vector3(90,0,0));              // cheek
            }
            p.Cube(new Vector3(0,1.25f,0),new Vector3(.72f,.86f,.46f),jacket);                                // jacket
            p.Cube(new Vector3(0,.86f,0),new Vector3(.74f,.07f,.48f),trim);                                   // hem
            p.Tube(new Vector3(0,1.72f,0),.66f,.1f,Art.White);                                                // ruffle
            for(int k=0;k<3;k++)p.Ball(new Vector3(0,1.52f-k*.2f,.235f),Vector3.one*.1f,Art.White);           // buttons
            p.Ball(new Vector3(0,2.06f,.01f),new Vector3(.58f,.66f,.54f),Art.White);                          // head
            p.Ball(new Vector3(0,2.42f,-.04f),Vector3.one*.28f,hair);                                         // tuft
            // The smile: a row of lips along an arc, the same on both faces.
            for(int k=0;k<9;k++)
            {
                float x=Mathf.Lerp(-.2f,.2f,k/8f),y=1.79f+2.75f*x*x,slope=Mathf.Atan(5.5f*x)*Mathf.Rad2Deg;
                p.Cube(new Vector3(x,y,.17f),new Vector3(.075f,.045f,.035f),Art.Coral,new Vector3(0,0,slope));
            }
            MergeClown(p,root,who);
            var nose=Art.Shape("Clown nose",PrimitiveType.Sphere,new Vector3(0,1.99f,.285f),Vector3.one*.26f,Art.Coral,true,root);
            var honk=nose.AddComponent<Interactable>();honk.kind=ActionKind.AmbushNose;honk.director=d;honk.value=index;honk.prompt="Honk the nose";
            // Where a page sent at this clown comes to rest: in front of its chest. Props on the table do not hide it.
            var chest=Art.Group("Chest target",new Vector3(0,1.45f,.5f),root);
            var aim=Target(chest.gameObject,AmbushRoom.Room,Vector3.zero,.6f,"Point at the clown and let go.",null);
            aim.category="paper";aim.seeThrough=true;aim.highlight=Outline("Clown "+who+" highlight",Vector3.zero,new Vector2(1.3f,1.7f),true,chest);
            // Three balloons float from the inner hand, clear of the hair.
            var balloons=new GameObject[3];
            var colours=index==0?new[]{Art.Coral,Art.Gold,Art.Teal}:new[]{Art.Mint,Art.Coral,Art.Gold};
            var spots=new[]{new Vector3(inner*.74f,2.2f,.14f),new Vector3(inner*.64f,2.62f,-.04f),new Vector3(inner*.72f,3f,.1f)};
            var handTop=new Vector3(inner*.47f,.99f,.03f);
            for(int k=0;k<3;k++)
            {
                var group=Art.Group("Balloon "+(k+1),spots[k],root);
                var ball=Art.Shape("Balloon",PrimitiveType.Sphere,Vector3.zero,new Vector3(.36f,.44f,.36f),colours[k],true,group);
                var pop=ball.AddComponent<Interactable>();pop.kind=ActionKind.AmbushBalloon;pop.director=d;pop.value=index*3+k;pop.prompt="Pop the balloon";
                Art.Shape("Balloon knot",PrimitiveType.Sphere,new Vector3(0,-.23f,0),new Vector3(.06f,.05f,.06f),colours[k],false,group);
                Art.Line("Balloon string",new Vector3(0,-.25f,0),handTop-spots[k],.012f,Art.White,group);
                balloons[k]=group.gameObject;
            }
            // The sillier things that appear as feelings are named: a propeller beanie, then a bowtie.
            var hat=Art.Group("Propeller beanie",new Vector3(0,2.52f,-.02f),root);
            Art.Shape("Beanie cap",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.3f,.04f,.3f),cap,false,hat);
            Art.Box("Beanie stem",new Vector3(0,.09f,0),new Vector3(.03f,.1f,.03f),Art.Metal,false,hat,.004f);
            var blades=Art.Group("Blades",new Vector3(0,.15f,0),hat);blades.gameObject.AddComponent<Spin>().degreesPerSecond=540;
            Art.Box("Blade",Vector3.zero,new Vector3(.8f,.015f,.11f),paint,false,blades,.003f);Art.Box("Blade",Vector3.zero,new Vector3(.11f,.015f,.8f),paint,false,blades,.003f);
            hat.gameObject.SetActive(false);
            var bow=Art.Group("Bowtie",new Vector3(0,1.69f,.25f),root);
            for(int s=-1;s<=1;s+=2)Art.Box("Bow wing",new Vector3(s*.14f,0,0),new Vector3(.2f,.15f,.05f),trim,false,bow,.008f).transform.localRotation=Quaternion.Euler(0,0,-s*14);
            Art.Shape("Bow knot",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.09f,Art.White,false,bow);
            bow.gameObject.SetActive(false);
            return new AmbushRoom.Giant{root=root,nose=nose.transform,aim=aim,balloons=balloons,propeller=hat.gameObject,bowtie=bow.gameObject};
        }
        // An office chair. Nothing about it is solid, so it never stands between a player and the table.
        static Transform AmbushChair(string name,Vector3 at,float yaw,Material cloth,Transform parent=null)
        {
            var g=Art.Group(name,at,parent);g.localRotation=Quaternion.Euler(0,yaw,0);
            Art.Box("Seat",new Vector3(0,.46f,0),new Vector3(.6f,.09f,.56f),cloth,false,g,.02f);
            Art.Box("Seat back",new Vector3(0,.88f,-.27f),new Vector3(.58f,.6f,.08f),cloth,false,g,.02f);
            for(int s=-1;s<=1;s+=2)
            {
                Art.Box("Armrest",new Vector3(s*.33f,.66f,-.02f),new Vector3(.06f,.05f,.46f),Art.Metal,false,g,.01f);
                Art.Box("Arm post",new Vector3(s*.33f,.56f,.14f),new Vector3(.04f,.2f,.04f),Art.Metal,false,g,.005f);
            }
            Art.Shape("Chair column",PrimitiveType.Cylinder,new Vector3(0,.22f,0),new Vector3(.07f,.19f,.07f),Art.Metal,false,g);
            for(int k=0;k<5;k++){float a=k*72*Mathf.Deg2Rad;Art.Line("Chair spoke",new Vector3(0,.07f,0),new Vector3(Mathf.Sin(a)*.32f,.07f,Mathf.Cos(a)*.32f),.035f,Art.Metal,g);}
            return g;
        }
        // A sheet that lies on the table, can be carried and tossed, and goes back to its place when it has been hit or torn. Its words lie flat on top.
        static Grabbable AmbushPaper(string label,string words,Vector3 at,Vector2 size,float thick,bool packet)
        {
            var root=Art.Group(label,at);
            root.gameObject.AddComponent<BoxCollider>().size=new Vector3(size.x,thick,size.y);
            var body=root.gameObject.AddComponent<Rigidbody>();body.mass=packet?.25f:.15f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var g=root.gameObject.AddComponent<Grabbable>();g.label=label;g.room=AmbushRoom.Room;g.upright=true;g.tossable=true;g.category="paper";
            Art.Box("Sheet",Vector3.zero,new Vector3(size.x,thick,size.y),Art.White,false,root,.002f);
            if(packet)Art.Box("Packet band",new Vector3(0,thick*.5f+.001f,-size.y*.36f),new Vector3(size.x,.003f,.07f),Art.Coral,false,root,.001f);
            else Art.Box("Sheet corner",new Vector3(size.x*.3f,thick*.5f+.001f,-size.y*.4f),new Vector3(.07f,.003f,.04f),Art.Mint,false,root,.001f);
            var text=Art.Text("Paper text",words,new Vector3(0,thick*.5f+.004f,packet?.04f:0),.04f,dark,size.x*.9f,root,false);
            text.transform.localRotation=Quaternion.Euler(90,0,0);text.rectTransform.sizeDelta=new Vector2(size.x*.88f,size.y*.7f);
            text.enableAutoSizing=true;text.fontSizeMin=.06f;text.fontSizeMax=.26f;
            return g;
        }
        // Words that lie flat on the table, facing the player's chair.
        static TextMeshPro AmbushFlatText(string name,string words,Vector3 at,float size,Color color,float width)
        {
            var text=Art.Text(name,words,at,size,color,width);text.transform.localRotation=Quaternion.Euler(90,0,0);text.rectTransform.sizeDelta=new Vector2(width,.3f);return text;
        }
        static void Ambush()
        {
            var room=d.rooms[AmbushRoom.Room].root.AddComponent<AmbushRoom>();room.director=d;d.ambush=room;d.rooms[AmbushRoom.Room].total=AmbushRoom.Feelings;
            const float W=16,H=10;
            // ----- The hall: a boardroom, tall enough for the clowns, with a night window behind them.
            Floor(new Vector3(0,-.15f,2.5f),new Vector3(W,.3f,15),Art.Ink);
            Art.Tiled("Left wall",new Vector3(-W/2,H/2,2.5f),new Vector3(.2f,H,15),Art.Navy,new Vector3(0,0,2.4f));
            Art.Tiled("Right wall",new Vector3(W/2,H/2,2.5f),new Vector3(.2f,H,15),Art.Navy,new Vector3(0,0,2.4f));
            Art.Tiled("Back wall",new Vector3(0,H/2,10),new Vector3(W,H,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Entrance wall",new Vector3(0,H/2,-5.1f),new Vector3(W,H,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Ceiling",new Vector3(0,H+.075f,2.5f),new Vector3(W,.15f,15),Art.Ink,new Vector3(3,0,3),false);
            for(int k=-1;k<=1;k++)Art.Box("Ceiling luminous strip",new Vector3(k*4f,H-.025f,2.5f),new Vector3(.12f,.035f,13),Art.White,false);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Box("Wainscot",new Vector3(side*(W/2-.13f),.6f,2.5f),new Vector3(.06f,1.2f,15),Art.Cream,false);
                Art.Box("Wall rail",new Vector3(side*(W/2-.12f),1.23f,2.5f),new Vector3(.08f,.05f,15),Art.Gold,false);
                for(float z=-3;z<10;z+=3.8f)Art.Box("Wall column",new Vector3(side*(W/2-.15f),H/2,z),new Vector3(.2f,H,.3f),Art.Ink,false);
            }
            Art.Box("Skirting",new Vector3(0,.07f,-4.96f),new Vector3(W-.4f,.12f,.06f),Art.Gold,false);
            // The window: the office after hours, the city still lit, with the clowns in front of it.
            Art.Box("Night window",new Vector3(0,4.4f,9.87f),new Vector3(7.8f,4.6f,.04f),Art.Ink,false);
            for(int k=0;k<5;k++)Art.Box("Window mullion",new Vector3(-3.9f+k*1.95f,4.4f,9.83f),new Vector3(.08f,4.7f,.05f),Art.Navy,false);
            foreach(float y in new[]{2.05f,4.4f,6.75f})Art.Box("Window transom",new Vector3(0,y,9.83f),new Vector3(7.9f,.08f,.05f),Art.Navy,false);
            Art.Box("Window sill",new Vector3(0,2f,9.74f),new Vector3(8.1f,.08f,.32f),Art.Gold,false);
            var random=new System.Random(8);float R()=>(float)random.NextDouble();
            for(int k=0;k<80;k++)Art.Box("City light",new Vector3(R()*7.4f-3.7f,2.3f+R()*4.2f,9.84f),new Vector3(.07f,.055f,.01f),k%3==0?Art.White:Art.Gold,false);
            for(int k=0;k<3;k++)Art.Plant(new Vector3(k==0?-7.2f:7.2f,0,k==2?-4.2f:9.2f));
            Art.Plant(new Vector3(-7.2f,0,-4.2f));
            // Posters that say what the company says.
            string[] slogans={"WE ARE\nA FAMILY","THERE IS NO\n'I' IN TEAM","PEOPLE ARE OUR\nGREATEST ASSET"};
            for(int k=0;k<slogans.Length;k++)
            {
                float z=-1.1f+k*3.8f; // the middle of the bays between the wall columns, so no column stands in front of a poster
                Art.Box("Wall poster",new Vector3(W/2-.13f,3.4f,z),new Vector3(.03f,1.5f,2.4f),Art.Cream,false);
                var poster=Art.Text("Wall poster text",slogans[k],new Vector3(W/2-.17f,3.4f,z),.2f,dark,2.3f,null,false);poster.transform.localRotation=Quaternion.Euler(0,90,0);
            }
            Header(AmbushRoom.Room,"07 / THE AMBUSH","It was sudden.","Name what happened. Say what you need to say. Then stand up.",new Vector3(-7.55f,4.6f,5f),9,-90);
            // ----- The table: huge, with a dark leather inlay, the chairs of everyone who is not here, and the things a meeting like that leaves.
            Art.Box("Table top",new Vector3(0,.74f,.8f),new Vector3(9,.12f,3.4f),Art.Wood);
            Art.Box("Table apron",new Vector3(0,.62f,.8f),new Vector3(8.7f,.12f,3.1f),Art.Navy,false);
            Art.Box("Table inlay",new Vector3(0,.804f,.75f),new Vector3(8.4f,.008f,3.1f),Art.Navy,false);
            for(int s=-1;s<=1;s+=2)Art.Box("Table support",new Vector3(s*3.4f,.28f,.8f),new Vector3(.5f,.56f,2.8f),Art.Ink);
            foreach(float x in new[]{-4.4f,-3.2f,-1.9f,1.9f,3.2f,4.4f})AmbushChair("Empty chair",new Vector3(x,0,-1.9f),0,Art.Navy);
            AmbushChair("Empty chair",new Vector3(-5.2f,0,.8f),90,Art.Navy);AmbushChair("Empty chair",new Vector3(5.2f,0,.8f),-90,Art.Navy);
            for(int k=0;k<4;k++){float x=k<2?-3.9f+k*.5f:3.4f+(k-2)*.5f;Art.Shape("Water glass",PrimitiveType.Cylinder,new Vector3(x,.9f,-.1f),new Vector3(.08f,.07f,.08f),Art.Glass,false);}
            Art.Shape("Water jug",PrimitiveType.Cylinder,new Vector3(-3.4f,.93f,.35f),new Vector3(.14f,.12f,.14f),Art.Glass,false);Art.Shape("Water jug",PrimitiveType.Cylinder,new Vector3(3.9f,.93f,.35f),new Vector3(.14f,.12f,.14f),Art.Glass,false);
            // A box of tissues at the end of the table, out of the way of the signs lying on it.
            Art.Box("Tissue box",new Vector3(3.05f,.865f,-.78f),new Vector3(.26f,.12f,.14f),Art.Cream,false,null,.01f);
            Art.Shape("Tissue",PrimitiveType.Sphere,new Vector3(3.05f,.94f,-.78f),new Vector3(.12f,.07f,.07f),Art.White,false);
            // Words that lie flat on the table, for anyone who has not met the room before.
            var feelingsSign=AmbushFlatText("Ambush feelings sign","NAME HOW YOU FEEL\n<size=60%>Point at a card. Each one you name makes them smaller.</size>",new Vector3(0,.82f,-.58f),.07f,ivory,2.2f);
            var repliesSign=AmbushFlatText("Ambush replies sign","SAY IT BACK\n<size=60%>Point at a reply.</size>",new Vector3(-2.1f,.82f,-.55f),.06f,ivory,1.5f);
            var paperworkSign=AmbushFlatText("Ambush paperwork sign","THE PAPERWORK\n<size=60%>Carry it. Send it at them. Shake it to tear it.</size>",new Vector3(2.07f,.82f,-.5f),.06f,ivory,1.5f);
            // ----- The room's moving parts.
            var changing=Art.Group("Moving parts",Vector3.zero);room.changing=changing;
            // Your chair, which rolls back when you stand.
            room.chair=AmbushChair("Your chair",new Vector3(0,0,-1.9f),0,Art.Teal,changing);
            // Two giants.
            room.clowns=new[]{AmbushClown(0,changing),AmbushClown(1,changing)};
            // The screen between them: what is named, what you said, what they said.
            Art.Box("Screen leg",new Vector3(-1f,1.3f,2.07f),new Vector3(.05f,1f,.05f),Art.Metal,false);Art.Box("Screen leg",new Vector3(1f,1.3f,2.07f),new Vector3(.05f,1f,.05f),Art.Metal,false);
            Art.Box("Clown board",new Vector3(0,2.3f,2.07f),new Vector3(2.4f,1f,.06f),Art.Ink,false,null,.01f);
            Art.Box("Clown board rim",new Vector3(0,2.3f,2.1f),new Vector3(2.46f,1.06f,.03f),Art.Gold,false,null,.008f);
            var board=Art.Text("Clown board text","<size=62%><color=#ABC4C5>FEELINGS NAMED  0 / 6</color></size>\n<size=62%><color=#ABC4C5>LEFT</color></size>  Thank you for coming in.\n<size=62%><color=#ABC4C5>RIGHT</color></size>  This isn't personal.",new Vector3(0,2.3f,2.01f),.1f,Color.white,2.2f);
            board.rectTransform.sizeDelta=new Vector2(2.2f,.85f);board.enableAutoSizing=true;board.fontSizeMin=.2f;board.fontSizeMax=.75f;room.board=board;
            // Six feelings, in two rows. The back row stands on a step so the front row never hides it.
            var plates=new Renderer[AmbushRoom.Feelings];var labels=new TextMeshPro[AmbushRoom.Feelings];
            for(int i=0;i<AmbushRoom.Feelings;i++)
            {
                int row=i/3,col=i%3;var at=new Vector3((col-1)*.9f,row==0?.937f:1.087f,row==0?.3f:1f);
                if(row==1)Art.Box("Card stand",new Vector3(at.x,.875f,at.z),new Vector3(.8f,.15f,.34f),Art.Navy,false);
                var card=Art.Group("Feeling card "+(i+1),at,changing);card.localRotation=Quaternion.Euler(35,0,0);
                var button=Art.Button("Feeling "+(i+1),AmbushRoom.FeelingNames[i],Vector3.zero,new Vector3(.8f,.3f,.05f),Art.Teal,ActionKind.AmbushFeeling,d,i,card);
                plates[i]=button.GetComponent<Renderer>();labels[i]=card.Find("Feeling "+(i+1)+" label").GetComponent<TextMeshPro>();
            }
            room.cardPlates=plates;room.cardLabels=labels;room.cardOpen=Art.Teal;room.cardNamed=Art.Mint;
            // Five things to say, from confused to furious, rising like steps.
            Material[] heat={Art.Teal,Art.Teal,Art.Gold,Art.Coral,Art.Orange};
            for(int i=0;i<AmbushRoom.Replies;i++)
            {
                float step=.04f+.17f*i,z=-.15f+i*.34f; // each reply stands clear above the one in front, so no label is hidden by the strip before it
                Art.Box("Reply stand",new Vector3(-2.1f,.8f+step/2,z),new Vector3(.5f,step,.3f),Art.Navy,false);
                var strip=Art.Group("Reply strip "+(i+1),new Vector3(-2.1f,.8f+step+.085f,z));strip.localRotation=Quaternion.Euler(35,-35,0);
                Art.Button("Reply "+(i+1),AmbushRoom.ReplyNames[i],Vector3.zero,new Vector3(.92f,.18f,.04f),heat[i],ActionKind.AmbushReply,d,i,strip);
            }
            // The paperwork: five sheets and the severance packet.
            string[] sheets={"TERMINATION\nNOTICE\n<size=60%>Effective today.</size>","EXIT\nINTERVIEW\n<size=60%>Please be brief.</size>","CONFIDENTIALITY\nAGREEMENT\n<size=60%>Please do not discuss.</size>","RESTRUCTURING\nPLAN\n<size=60%>Your role is no longer needed.</size>","BENEFITS\nENDING\n<size=60%>As of this afternoon.</size>"};
            string[] names={"Termination notice","Exit interview","Confidentiality agreement","Restructuring plan","Benefits ending"};
            var slots=new[]{new Vector3(1.85f,0,-.05f),new Vector3(2.3f,0,-.05f),new Vector3(1.85f,0,.6f),new Vector3(2.3f,0,.6f),new Vector3(1.85f,0,1.25f)};
            var papers=new List<Grabbable>();
            for(int i=0;i<sheets.Length;i++){slots[i].y=.809f;papers.Add(AmbushPaper(names[i],sheets[i],slots[i],new Vector2(.3f,.4f),.012f,false));}
            papers.Add(AmbushPaper("Severance packet","SEVERANCE\nPACKET\n<size=60%>Sign here. And here.</size>",new Vector3(2.3f,.815f,1.25f),new Vector2(.36f,.46f),.024f,true));
            room.papers=papers.ToArray();
            // Once everything is named: STAND UP, close to the seat.
            var stand=Art.Group("Stand up",new Vector3(0,.96f,-.25f),changing);stand.localRotation=Quaternion.Euler(35,0,0);
            Art.Button("Stand up button","STAND UP",Vector3.zero,new Vector3(1.2f,.34f,.06f),Art.Coral,ActionKind.StandUp,d,0,stand);
            stand.gameObject.SetActive(false);room.standUpButton=stand.gameObject;
            // The way out, which opens when you stand: back to the lobby.
            var exit=Art.Group("Way out",Vector3.zero,changing);var old=Art.Root;Art.Root=exit;Door("08","The way\nout","Back to the lobby.\nTake your time.",new Vector3(-5.6f,0,9.78f),0,Art.Gold,0);Art.Root=old;
            exit.gameObject.SetActive(false);room.exitDoor=exit.gameObject;
            // Warm light for the end: table lamps, and the city at the window turning to dawn.
            var warm=new List<GameObject>();
            foreach(float x in new[]{-3.9f,3.9f})
            {
                var lamp=Art.Group("Table lamp",new Vector3(x,.8f,-.55f),changing);
                Art.Shape("Lamp base",PrimitiveType.Cylinder,new Vector3(0,.015f,0),new Vector3(.18f,.015f,.18f),Art.Metal,false,lamp);
                Art.Line("Lamp arm",new Vector3(0,.02f,0),new Vector3(0,.36f,0),.02f,Art.Metal,lamp);
                Art.Shape("Lamp shade",PrimitiveType.Cylinder,new Vector3(0,.4f,0),new Vector3(.2f,.06f,.2f),Art.Lantern,false,lamp);
                lamp.gameObject.SetActive(false);warm.Add(lamp.gameObject);
            }
            var dawn=Art.Box("Dawn glow",new Vector3(0,2.25f,9.81f),new Vector3(7.6f,.22f,.02f),Art.Gold,false,changing);dawn.SetActive(false);warm.Add(dawn);
            room.warmThings=warm.ToArray();
            // ----- Paper and balloon bits, thrown from where they were hit.
            var floorPlane=Art.Group("Ambush floor",new Vector3(0,.012f,0));
            var cube=Geometry.Get(PrimitiveType.Cube);var flat=new AnimationCurve(new Keyframe(0,1),new Keyframe(.75f,1),new Keyframe(1,0));
            room.puff=BlastSystem("Paper puff",changing,cube,new[]{Art.White.color,Art.Paper.color},46,new Vector2(.9f,1.5f),new Vector2(1f,3.2f),.06f,.5f,.8f,new Vector2(.03f,.07f),flat,floorPlane,chip:true);
            room.balloonBits=BlastSystem("Balloon bits",changing,cube,new[]{Art.Coral.color,Art.Gold.color,Art.Teal.color,Art.Mint.color},40,new Vector2(.7f,1.2f),new Vector2(2f,5f),.1f,.6f,.6f,new Vector2(.05f,.1f),flat,floorPlane,chip:true);
            room.lightsSound=SoundBank.Get("SFX - Lights");room.squeakSound=SoundBank.Get("SFX - Squeak");room.balloonSound=SoundBank.Get("SFX - Balloon");room.tearSound=SoundBank.Get("SFX - Tear");room.standSound=SoundBank.Get("SFX - Stand");
            Sound("Ambience - Boardroom",.5f);
        }
    }
}
