using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // Original geometry is authored into the scene by the editor builder.
    public static class WorldBuilder
    {
        static GameDirector d;
        static Color ivory=new Color(.92f,.91f,.85f), muted=new Color(.61f,.76f,.77f), dark=new Color(.08f,.15f,.2f);
        public static void Build(GameDirector director)
        {
            d=director;d.rooms=new MindRoom[6];
            NewRoom(0,"The lobby",Vector3.zero,new Vector3(0,.05f,-4));Hub();
            NewRoom(1,"Ocean of shame",new Vector3(80,0,0),new Vector3(0,.3f,-1));Ocean();
            NewRoom(2,"Heavy things",new Vector3(160,0,0),new Vector3(0,.04f,-2.4f));Warehouse();
            NewRoom(3,"A small spill",new Vector3(240,0,0),new Vector3(0,.04f,1.4f));Kitchen();
            NewRoom(4,"The infinite archive",new Vector3(320,0,0),new Vector3(0,.04f,-1.2f));Archive();
            NewRoom(5,"Room for tomorrow",new Vector3(400,0,0),new Vector3(0,.04f,-4));Rooftop();
            Menu();
            for(int i=0;i<6;i++)d.rooms[i].root.SetActive(i==0);
        }
        // Each room has its own soundscape. Sources play when their room is switched on.
        static void Sound(string clip,float volume,Transform parent=null)
        {
            var source=(parent?parent:Art.Root).gameObject.AddComponent<AudioSource>();
            source.clip=SoundBank.Get(clip);source.loop=true;source.playOnAwake=true;source.spatialBlend=0;source.volume=volume;source.priority=64;
        }
        static void NewRoom(int i,string title,Vector3 at,Vector3 spawn)
        {
            Art.Root=new GameObject(i.ToString("00")+" - "+title).transform;Art.Root.position=at;
            d.rooms[i]=new MindRoom{title=title,root=Art.Root.gameObject,spawn=Art.Group("Arrival",spawn)};
        }
        static void Floor(Vector3 p,Vector3 size,Material material)
        {Art.Tiled("Walkable floor",p,size,material,new Vector3(1.6f,0,1.6f),castShadows:false).AddComponent<TeleportSurface>();}
        static void Shell(float width,float depth,Material floor, bool ceiling=true)
        {
            Floor(new Vector3(0,-.15f,depth*.5f-5),new Vector3(width,.3f,depth),floor);
            // Walls are panelled and the ceiling is coffered: the bevelled seams give large surfaces scale and catch the light.
            Art.Tiled("Left wall",new Vector3(-width/2,2.3f,depth*.5f-5),new Vector3(.2f,4.6f,depth),Art.Navy,new Vector3(0,0,2.4f));
            Art.Tiled("Right wall",new Vector3(width/2,2.3f,depth*.5f-5),new Vector3(.2f,4.6f,depth),Art.Navy,new Vector3(0,0,2.4f));
            Art.Tiled("Back wall",new Vector3(0,2.3f,depth-5),new Vector3(width,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Entrance wall",new Vector3(0,2.3f,-5.1f),new Vector3(width,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            if(ceiling)Art.Tiled("Ceiling",new Vector3(0,4.65f,depth*.5f-5),new Vector3(width,.15f,depth),Art.Ink,new Vector3(3,0,3),false);
            for(float z=-2;z<depth-5;z+=4)
            {
                Art.Box("Ceiling luminous strip",new Vector3(0,4.5f,z),new Vector3(width*.7f,.035f,.08f),Art.White,false);
                Art.Box("Skirting left",new Vector3(-width/2+.13f,.14f,z),new Vector3(.06f,.1f,3.8f),Art.Gold,false);
                Art.Box("Skirting right",new Vector3(width/2-.13f,.14f,z),new Vector3(.06f,.1f,3.8f),Art.Gold,false);
            }
        }
        static void Header(int room,string kicker,string title,string sub,Vector3 at,float width=8)
        {
            Art.Text("Chapter",kicker,at+Vector3.up*.65f,.17f,muted,width);
            Art.Text("Room title",title,at,.55f,ivory,width);
            Art.Text("Room subtitle",sub,at+Vector3.down*.55f,.18f,muted,width);
            d.rooms[room].progress=Art.Text("Room progress","TAKE YOUR TIME",at+Vector3.down*.93f,.15f,new Color(.9f,.66f,.38f),width);
        }
        // Comfort, turning and room options in one panel that the menu button opens. It belongs to the player, not a room.
        static void Menu()
        {
            var root=Art.Group("Comfort menu",Vector3.zero,d.player.transform);
            var menu=root.gameObject.AddComponent<ComfortMenu>();menu.director=d;d.menu=menu;d.player.menu=menu;
            // Two columns, about 44 degrees wide at arm's length, so the whole menu fits a narrower field of view.
            Art.Box("Menu panel",new Vector3(0,-.01f,.014f),new Vector3(.53f,.52f,.012f),Art.Ink,true,root,.006f);
            Art.Text("Menu title","AFTER HOURS  /  MENU",new Vector3(-.05f,.222f,-.002f),.019f,muted,.36f,root);
            menu.status=Art.Text("Menu room","THE LOBBY",new Vector3(-.05f,.19f,-.002f),.027f,ivory,.36f,root);
            MenuButton(root,"Menu close","CLOSE",.19f,.205f,Art.Coral,ActionKind.CloseMenu,.12f,.055f);
            MenuButton(root,"Menu turn left","< TURN",-.17f,.125f,Art.Teal,ActionKind.TurnLeft,.12f);
            MenuButton(root,"Menu turn around","TURN AROUND",0,.125f,Art.Teal,ActionKind.TurnAround,.2f);
            MenuButton(root,"Menu turn right","TURN >",.17f,.125f,Art.Teal,ActionKind.TurnRight,.12f);
            menu.seated=MenuButton(root,"Menu seated view","SEATED VIEW",-.12f,.04f,Art.Navy,ActionKind.SeatedMode);
            menu.height=MenuButton(root,"Menu seated height","SEATED HEIGHT",.12f,.04f,Art.Navy,ActionKind.SeatedHeight);
            menu.walking=MenuButton(root,"Menu movement","MOVEMENT",-.12f,-.045f,Art.Navy,ActionKind.SmoothMotion);
            menu.vignette=MenuButton(root,"Menu comfort vignette","COMFORT VIGNETTE",.12f,-.045f,Art.Navy,ActionKind.Vignette);
            menu.crowd=MenuButton(root,"Menu kitchen crowd","KITCHEN CROWD",-.12f,-.13f,Art.Navy,ActionKind.GentleAudience);
            MenuButton(root,"Menu face forward","FACE FORWARD",.12f,-.13f,Art.Teal,ActionKind.FaceForward);
            MenuButton(root,"Menu try again","TRY AGAIN",-.12f,-.215f,Art.Navy,ActionKind.ResetRoom);menu.tryAgain=root.Find("Menu try again").gameObject;
            MenuButton(root,"Menu lobby","LOBBY",.12f,-.215f,Art.Teal,ActionKind.Rest);menu.lobby=root.Find("Menu lobby").gameObject;
            Art.Text("Menu tip","Right stick turns; pull it back to turn around.  B / Y: lobby.",new Vector3(0,-.258f,-.002f),.016f,muted,.5f,root);
            // A panel floating at arm's length should not throw a shadow onto the room.
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            menu.Refresh();root.gameObject.SetActive(false);
        }
        static TextMeshPro MenuButton(Transform menu,string name,string label,float x,float y,Material color,ActionKind kind,float width=.225f,float height=.072f)
        {
            var group=Art.Group(name,new Vector3(x,y,0),menu);
            Art.Button(name,label,Vector3.zero,new Vector3(width,height,.018f),color,kind,d,0,group);
            var text=group.Find(name+" label").GetComponent<TextMeshPro>();text.fontSizeMin=.1f;text.fontSizeMax=.19f;
            return text;
        }
        // The first-visit guide: a content note, then pinch and the menu, taught by doing rather than by reading.
        static void Guide()
        {
            var root=Art.Group("First visit guide",new Vector3(0,1.42f,-2.65f));
            var guide=root.gameObject.AddComponent<IntroGuide>();guide.director=d;d.intro=guide;
            Art.Box("Guide panel",new Vector3(0,0,.014f),new Vector3(.98f,.58f,.02f),Art.Ink,true,root,.008f);
            Art.Box("Guide accent",new Vector3(0,.29f,.0f),new Vector3(.98f,.012f,.022f),Art.Gold,false,root,.002f);
            guide.title=Art.Text("Guide title","Before you begin",new Vector3(0,.2f,-.004f),.06f,ivory,.88f,root);guide.title.rectTransform.sizeDelta=new Vector2(.88f,.1f);
            guide.body=Art.Text("Guide text","",new Vector3(0,.025f,-.004f),.05f,muted,.9f,root);guide.body.rectTransform.sizeDelta=new Vector2(.9f,.27f);guide.body.enableAutoSizing=true;guide.body.fontSizeMin=.24f;guide.body.fontSizeMax=.35f;
            guide.next=GuideButton(root,"Guide next","CONTINUE",new Vector3(.2f,-.205f,0),new Vector3(.38f,.1f,.03f),Art.Teal,ActionKind.IntroNext,out guide.nextLabel);
            guide.skip=GuideButton(root,"Guide skip","SKIP",new Vector3(-.27f,-.205f,0),new Vector3(.2f,.08f,.03f),Art.Navy,ActionKind.IntroSkip,out _);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            root.gameObject.SetActive(false);
        }
        static GameObject GuideButton(Transform parent,string name,string label,Vector3 at,Vector3 size,Material color,ActionKind kind,out TextMeshPro text)
        {
            var group=Art.Group(name,at,parent);
            Art.Button(name,label,Vector3.zero,size,color,kind,d,0,group);
            text=group.Find(name+" label").GetComponent<TextMeshPro>();text.fontSizeMin=.15f;text.fontSizeMax=.32f;
            return group.gameObject;
        }
        // A thin glowing outline that lights up when a held object is pointed at its destination.
        static GameObject Outline(string name,Vector3 centre,Vector2 size,bool vertical,Transform parent=null)
        {
            var root=Art.Group(name,centre,parent);float w=.028f;
            Vector3 along=vertical?new Vector3(size.x,w,w):new Vector3(size.x,w,w),across=vertical?new Vector3(w,size.y,w):new Vector3(w,w,size.y);
            Vector3 edgeA=vertical?new Vector3(0,size.y/2,0):new Vector3(0,0,size.y/2),edgeB=new Vector3(size.x/2,0,0);
            Art.Box("Edge",edgeA,along,d.player.rayMaterial,false,root,.004f);Art.Box("Edge",-edgeA,along,d.player.rayMaterial,false,root,.004f);
            Art.Box("Edge",edgeB,across,d.player.rayMaterial,false,root,.004f);Art.Box("Edge",-edgeB,across,d.player.rayMaterial,false,root,.004f);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            root.gameObject.SetActive(false);return root.gameObject;
        }
        static SendTarget Target(GameObject host,int room,Vector3 landing,float radius,string prompt,GameObject highlight,Grabbable only=null)
        {
            var t=host.AddComponent<SendTarget>();t.room=room;t.landing=landing;t.radius=radius;t.prompt=prompt;t.highlight=highlight;t.only=only;return t;
        }
        static void Door(string number,string title,string description,Vector3 position,float yaw,Material accent,int destination)
        {
            var old=Art.Root;var t=Art.Group(title+" doorway",position);t.localRotation=Quaternion.Euler(0,yaw,0);Art.Root=t;
            Art.Box("Door panel",new Vector3(0,1.4f,.18f),new Vector3(2.3f,2.8f,.15f),Art.Ink);
            Art.Box("Portal glow",new Vector3(-1.18f,1.5f,0),new Vector3(.075f,3,.1f),accent,false);
            Art.Box("Portal glow",new Vector3(1.18f,1.5f,0),new Vector3(.075f,3,.1f),accent,false);
            Art.Box("Portal lintel",new Vector3(0,3,0),new Vector3(2.44f,.08f,.1f),accent,false);
            Art.Text("Chapter number",number,new Vector3(0,2.6f,-.05f),.25f,muted,2);
            Art.Text("Destination",title,new Vector3(0,2.08f,-.06f),.29f,ivory,2.1f);
            Art.Text("Intention",description,new Vector3(0,1.5f,-.07f),.14f,muted,1.95f);
            Art.Button("Enter "+title,"ENTER",new Vector3(0,.95f,-.04f),new Vector3(1.45f,.4f,.15f),accent,ActionKind.Travel,d,destination);
            var threshold=Art.Box("Walk through portal",new Vector3(0,1,-.4f),new Vector3(2,2,.35f),accent,false);Object.DestroyImmediate(threshold.GetComponent<Renderer>());var trigger=threshold.AddComponent<BoxCollider>();trigger.isTrigger=true;var portal=threshold.AddComponent<Portal>();portal.director=d;portal.destination=destination;
            Art.Box("Threshold",new Vector3(0,.012f,-.65f),new Vector3(2.2f,.015f,1.1f),accent,false);
            Art.Root=old;
        }
        static void Hub()
        {
            Shell(14,21,Art.Cream);
            Art.Box("Midnight runner",new Vector3(0,.012f,4.5f),new Vector3(3.5f,.025f,18),Art.Teal,false);
            for(int side=-1;side<=1;side+=2)Art.Box("Runner piping",new Vector3(side*1.74f,.032f,4.5f),new Vector3(.025f,.015f,18),Art.Gold,false);
            Art.Text("Brand eyebrow","A WALK THROUGH THE OFFICE OF YOUR MIND",new Vector3(0,4.02f,13.8f),.2f,muted,11);
            Art.Text("Title","AFTER HOURS",new Vector3(0,3.12f,13.7f),1.0f,ivory,11);
            Art.Text("Tagline","A job ended. Your story did not.",new Vector3(0,2.38f,13.7f),.26f,muted,10);
            Door("01","Ocean of\nshame","A small boat.\nA light ahead.",new Vector3(-6.82f,0,1.1f),-90,Art.Teal,1);
            Door("02","Heavy\nthings","Give your feelings\na place to go.",new Vector3(6.82f,0,1.1f),90,Art.Coral,2);
            Door("03","A small\nspill","The machine is broken.\nYou are allowed help.",new Vector3(-6.82f,0,7),-90,Art.Gold,3);
            Door("04","The infinite\narchive","A fact, a fear,\nand what is still true.",new Vector3(6.82f,0,7),90,Art.Teal,4);
            // A detailed, polished memory occupies the end of each side of the lobby.
            MemoryOffice(new Vector3(-4.6f,0,12));MemoryOffice(new Vector3(4.6f,0,12));
            Art.Box("Reception console",new Vector3(0,.62f,-.3f),new Vector3(2.35f,1.24f,.65f),Art.Navy);
            Art.Box("Brass console cap",new Vector3(0,1.25f,-.3f),new Vector3(2.45f,.06f,.72f),Art.Gold);
            Art.Text("Welcome","YOU CAN BEGIN ANYWHERE",new Vector3(0,1.01f,-.64f),.18f,ivory,2.2f);
            d.lobbyProgress=Art.Text("Journey progress","0 / 4   ROOMS EXPLORED\nYour pace. No score. No deadline.",new Vector3(0,.65f,-.645f),.145f,muted,2.15f);
            Art.Box("Getting started board",new Vector3(3.5f,1.9f,2.85f),new Vector3(2.9f,1.35f,.08f),Art.Navy);
            Art.Text("Move instructions","POINT AND PINCH TO CHOOSE\nPinch an object to hold it. Point where it belongs and let go.\nLeft palm pinch or menu button: the menu.",new Vector3(3.5f,1.9f,2.8f),.14f,ivory,2.7f);
            var resume=Art.Button("Continue last room","BEGIN / OCEAN OF SHAME",new Vector3(0,2.16f,-.3f),new Vector3(2.75f,.4f,.12f),Art.Teal,ActionKind.Resume,d);
            d.resumeLabel=Art.Root.Find("Continue last room label").GetComponent<TextMeshPro>();
            Art.Text("Small sessions","A FEW MINUTES IS ENOUGH\nEach small step saves. Return to the lobby to take a break.",new Vector3(0,2.65f,-.3f),.13f,muted,3.8f);
            Art.Text("Comfort note","SITTING DOWN? OPEN THE MENU\nMenu button or left palm pinch, then SEATED VIEW.\nB / Y returns here from any room.",new Vector3(0,.24f,-.65f),.1f,muted,2.2f);
            Art.Button("Rooftop passage","ROOM FOR TOMORROW  /  ROOFTOP",new Vector3(0,1.1f,13.5f),new Vector3(4.2f,.65f,.16f),Art.Teal,ActionKind.Travel,d,5);
            for(int side=-1;side<=1;side+=2){Art.Plant(new Vector3(side*5.6f,0,-3));Art.Plant(new Vector3(side*2.8f,0,10));}
            for(int i=0;i<4;i++){Art.Box("Stepping marker",new Vector3(0,.034f,3+i*2.2f),new Vector3(.06f,.015f,.22f),Art.Gold,false);}
            Guide();
            Sound("Ambience - Lobby",.5f);Sound("Music - After hours theme",.22f);
        }
        static void MemoryOffice(Vector3 position)
        {
            var old=Art.Root;Art.Root=Art.Group("BEFORE - remembered office",position);
            Art.Box("Warm oak floor",new Vector3(0,.016f,0),new Vector3(3.8f,.03f,3.7f),Art.Wood);
            Art.Box("Desk",new Vector3(0,.77f,0),new Vector3(2.3f,.09f,.95f),Art.Wood);
            for(int s=-1;s<=1;s+=2)Art.Box("Chrome desk leg",new Vector3(s*.9f,.37f,0),new Vector3(.06f,.74f,.8f),Art.Metal);
            Art.Box("Monitor foot",new Vector3(0,.85f,.15f),new Vector3(.45f,.04f,.25f),Art.Metal);
            Art.Box("Monitor stand",new Vector3(0,1.05f,.2f),new Vector3(.06f,.4f,.05f),Art.Metal);
            Art.Box("Monitor",new Vector3(0,1.36f,.22f),new Vector3(1.03f,.59f,.06f),Art.Ink);
            Art.Box("Screen",new Vector3(0,1.36f,.181f),new Vector3(.96f,.52f,.013f),Art.Teal,false);
            Art.Text("Screen text","MONDAY\n09:00",new Vector3(0,1.37f,.168f),.1f,ivory,.9f);
            for(int row=0;row<4;row++)for(int col=0;col<12;col++)Art.Box("Keyboard key",new Vector3(-.37f+col*.063f,.832f,-.21f-row*.05f),new Vector3(.052f,.016f,.038f),Art.White,false);
            Art.Shape("Ceramic cup",PrimitiveType.Cylinder,new Vector3(.83f,.93f,-.2f),new Vector3(.14f,.12f,.14f),Art.White);
            Art.Shape("Mouse",PrimitiveType.Sphere,new Vector3(.62f,.86f,-.32f),new Vector3(.085f,.045f,.14f),Art.White,false);
            Art.Box("Chair seat",new Vector3(0,.48f,-.9f),new Vector3(.58f,.13f,.55f),Art.Navy);
            Art.Shape("Pneumatic base",PrimitiveType.Cylinder,new Vector3(0,.22f,-.9f),new Vector3(.07f,.2f,.07f),Art.Metal,false);
            Art.Shape("Chair back",PrimitiveType.Capsule,new Vector3(0,.88f,-1.1f),new Vector3(.6f,.43f,.15f),Art.Navy,false);
            for(int j=0;j<5;j++){float a=j*Mathf.PI*.4f;Art.Line("Chair spoke",new Vector3(0,.1f,-.9f),new Vector3(Mathf.Sin(a)*.4f,.1f,-.9f+Mathf.Cos(a)*.4f),.04f,Art.Metal);}
            Art.Plant(new Vector3(1.25f,0,.8f),true);
            Art.Box("Memory glass",new Vector3(0,1.25f,-1.9f),new Vector3(3.85f,2.5f,.04f),Art.Glass);
            Art.Text("Memory caption","BEFORE  /  THE WAY YOU REMEMBER IT",new Vector3(0,2.75f,-1.9f),.14f,muted,3.8f);
            Art.Root=old;
        }
        static void Ocean()
        {
            var ocean=d.rooms[1].root.AddComponent<OceanRoom>();ocean.director=d;d.ocean=ocean;
            var horizon=Art.Group("Moving horizon",Vector3.zero);ocean.horizon=horizon;
            WaterMesh(horizon);
            for(int i=0;i<20;i++)
            {float a=i*2.39f;Art.Facet("Far island",new Vector3((i%2==0?-1:1)*(9+i%5*3),-.6f,12+i*1.8f),new Vector3(7,4+i%4,7),i%2==0?Art.Teal:Art.Navy,5,horizon);}
            Art.Facet("The accepting shore",new Vector3(0,-.8f,20),new Vector3(12,3,7),Art.Cream,9,horizon);
            Art.Shape("Lighthouse tower",PrimitiveType.Cylinder,new Vector3(0,2.5f,20),new Vector3(1.1f,2.6f,1.1f),Art.Cream,true,horizon);
            Art.Shape("Lighthouse stripe",PrimitiveType.Cylinder,new Vector3(0,2.9f,20),new Vector3(1.12f,.38f,1.12f),Art.Coral,false,horizon);
            Art.Shape("Lantern",PrimitiveType.Cylinder,new Vector3(0,5.3f,20),new Vector3(1.3f,.35f,1.3f),Art.Gold,false,horizon);
            Art.Facet("Lighthouse roof",new Vector3(0,5.96f,20),new Vector3(2,1,2),Art.Navy,8,horizon);
            Art.Shape("Sun",PrimitiveType.Sphere,new Vector3(-14,13,48),Vector3.one*7,Art.Gold,false,horizon);
            Art.Text("Distant words","YOU ARE STILL WELCOME",new Vector3(0,1.25f,17),.24f,ivory,8,horizon);
            Art.Tiled("Boat deck",new Vector3(0,.12f,-.2f),new Vector3(2.5f,.25f,4.3f),Art.Wood,new Vector3(.42f,0,0)).AddComponent<TeleportSurface>();
            for(int side=-1;side<=1;side+=2){Art.Box("Boat gunwale",new Vector3(side*1.25f,.37f,-.2f),new Vector3(.16f,.55f,4.4f),Art.Coral);}
            Art.Box("Boat prow",new Vector3(0,.37f,1.97f),new Vector3(2.6f,.5f,.18f),Art.Coral);
            Art.Box("Boat stern",new Vector3(0,.37f,-2.4f),new Vector3(2.6f,.5f,.18f),Art.Coral);
            for(int z=0;z<2;z++)Art.Box("Bench",new Vector3(0,.45f,.9f-z*2.7f),new Vector3(2.3f,.14f,.38f),Art.Wood);
            for(int side=-1;side<=1;side+=2)
            {
                var paddle=Art.Box("Paddle "+side,new Vector3(side*1.02f,.76f,-.7f),new Vector3(.07f,.07f,1.75f),Art.Wood);var g=paddle.AddComponent<Grabbable>();g.paddle=true;g.label="Paddle";g.room=1;var rb=paddle.AddComponent<Rigidbody>();rb.isKinematic=true;
                Art.Box("Paddle blade",new Vector3(0,0,.55f),new Vector3(4,1.8f,.35f),Art.Gold,false,paddle.transform);
            }
            Art.Text("Ocean title","01  /  OCEAN OF SHAME",new Vector3(0,3.9f,10),.5f,ivory,12);
            Art.Text("Ocean intention","A feeling can be vast without being forever.",new Vector3(0,3.2f,10),.26f,muted,11);
            ocean.distance=Art.Text("Distance","14   SMALL STROKES TO SHORE",new Vector3(0,1.2f,1.8f),.13f,ivory,2.6f);
            Art.Button("Accessible rowing","ROW",new Vector3(0,.85f,1.6f),new Vector3(.8f,.32f,.1f),Art.Teal,ActionKind.Row,d);
            Art.Text("Rowing instruction","Hold a paddle. Pull toward you.\nOr point at ROW and select.\nBlink steps keep the boat steady.",new Vector3(0,2.2f,3.3f),.11f,muted,2.6f);
            Sound("Ambience - Ocean",.6f);
        }
        static void WaterMesh(Transform parent)
        {
            var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();float H(int x,int z)=>-.35f+Mathf.PerlinNoise(x*.31f+20,z*.31f+20)*.3f;
            for(int x=-16;x<16;x++)for(int z=-8;z<36;z++)
            {
                Vector3 a=new Vector3(x*2,H(x,z),z*2), b=new Vector3((x+1)*2,H(x+1,z),z*2),c=new Vector3(x*2,H(x,z+1),(z+1)*2),e=new Vector3((x+1)*2,H(x+1,z+1),(z+1)*2);
                int n=verts.Count;verts.AddRange(new[]{a,c,b,b,c,e});for(int k=0;k<6;k++){tris.Add(n+k);uv.Add(new Vector2(verts[n+k].x*.04f,verts[n+k].z*.04f));}
            }
            var mesh=new Mesh{name="Original faceted ocean"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();
            var go=new GameObject("Low polygon ocean",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=Art.Water;
        }
        static Grabbable Item(string label,string category,int room,Vector3 p,Vector3 scale,Material mat)
        {
            var box=Art.Box(label,p,scale,mat);var rb=box.AddComponent<Rigidbody>();rb.mass=.45f;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;
            var g=box.AddComponent<Grabbable>();g.label=label;g.category=category;g.room=room;
            Art.Text(label+" tag",label, new Vector3(0,0,-.505f),.14f/scale.y,Color.white,.95f,box.transform);
            return g;
        }
        static DropZone Zone(string name,Vector3 p,Vector3 size,int room,string category,int count)
        {
            var go=Art.Box(name,p,size,Art.Teal,false);Object.DestroyImmediate(go.GetComponent<Renderer>());var col=go.AddComponent<BoxCollider>();col.isTrigger=true;var zone=go.AddComponent<DropZone>();zone.category=category;zone.room=room;zone.slots=new Transform[count];
            for(int i=0;i<count;i++)zone.slots[i]=Art.Group("Accepted position "+i,p+new Vector3(room==2?(i%3-1)*.7f:(i-.5f)*.8f,-size.y*.5f+(room==2?.4f:.31f)+(i/3)*.6f,.1f),Art.Root);
            return zone;
        }
        static void Warehouse()
        {
            Shell(16,21,Art.Cream);d.rooms[2].total=5;
            Header(2,"02 / HEAVY THINGS","Processing, not erasing.","Pick up each feeling, point at the truck, and let it go.",new Vector3(0,3.25f,11));
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=0;z<3;z++)
                {
                    Vector3 at=new Vector3(side*6.1f,0,z*3+1);
                    for(int s=-1;s<=1;s+=2)Art.Box("Shelf upright",at+new Vector3(s*.95f,1.7f,0),new Vector3(.08f,3.4f,.85f),Art.Metal);
                    for(int level=0;level<3;level++){Art.Box("Shelf",at+new Vector3(0,.65f+level*1.1f,0),new Vector3(2.1f,.08f,1),Art.Gold);for(int b=0;b<3;b++)Art.Box("Stored box",at+new Vector3(-.65f+b*.65f,.95f+level*1.1f,0),new Vector3(.51f,.53f,.62f),level%2==0?Art.Teal:Art.Paper);}
                }
            }
            string[] names={"ANGER","FEAR","RELIEF","GRIEF","UNCERTAINTY"};Material[] colors={Art.Coral,Art.Navy,Art.Teal,Art.Gold,Art.Metal};
            for(int i=0;i<5;i++)
            {
                // Rest each box on its plinth (top at 0.72 m). Starting inside the plinth made physics shove the boxes askew.
                Vector3 p=new Vector3(-2.8f+i*1.4f,1.008f,-.3f+(i%2)*.4f);
                Art.Box("Packing plinth",new Vector3(p.x,.36f,p.z),new Vector3(.95f,.72f,.95f),Art.Wood);
                var item=Item(names[i],"",2,p,new Vector3(.6f,.57f,.6f),colors[i]);
                Art.Box("Packing tape",new Vector3(0,.503f,0),new Vector3(.14f,.02f,1.01f),Art.Cream,false,item.transform);
            }
            var truck=Art.Group("Feelings processing truck",new Vector3(0,0,6.6f));
            Art.Box("Truck bed",new Vector3(0,.35f,0),new Vector3(3.2f,.3f,4),Art.Metal,true,truck);
            for(int s=-1;s<=1;s+=2){Art.Box("Truck side",new Vector3(s*1.6f,1.5f,.2f),new Vector3(.15f,2.2f,3.6f),Art.Teal,true,truck);for(int z=-1;z<=1;z+=2){var wheel=Art.Shape("Wheel",PrimitiveType.Cylinder,new Vector3(s*1.6f,.36f,z*1.2f),new Vector3(.73f,.17f,.73f),Art.Ink,true,truck);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}}
            Art.Box("Truck cabin",new Vector3(0,1.4f,2.25f),new Vector3(3.2f,2.2f,1),Art.Teal,true,truck);
            Art.Box("Loading ramp",new Vector3(0,.17f,-2.55f),new Vector3(3.2f,.25f,1.5f),Art.Gold,true,truck);
            var bay=Zone("Processing bay",new Vector3(0,1.25f,6.1f),new Vector3(2.8f,1.7f,2.8f),2,"",5);
            Target(bay.gameObject,2,new Vector3(0,.2f,0),1.3f,"Point at the truck and let go.",Outline("Truck bay highlight",new Vector3(0,1.42f,4.62f),new Vector2(2.95f,1.85f),true));
            Art.Text("Truck message","ROOM TO CARRY ON",new Vector3(0,2.7f,8.23f),.25f,ivory,3);
            Art.Text("Bay sign","SEND BOXES HERE",new Vector3(0,.65f,4.53f),.16f,dark,3);
            for(int i=0;i<5;i++)Art.Box("Loading floor line",new Vector3(0,.015f,1.3f+i*.6f),new Vector3(.45f,.02f,.15f),Art.Gold,false);
            Sound("Ambience - Warehouse",.55f);
        }
        static void Kitchen()
        {
            Shell(13,15,Art.Cream);var k=d.rooms[3].root.AddComponent<KitchenRoom>();d.kitchen=k;k.director=d;
            Header(3,"03 / A SMALL SPILL","Everyone is watching.","At least, that is how it feels. Make a cup of coffee at your own pace.",new Vector3(0,3.5f,8.7f));
            var seat=d.rooms[3].spawn.localPosition;
            Art.Box("Kitchen cabinets",new Vector3(0,.48f,5),new Vector3(8,.96f,1.1f),Art.Teal);
            Art.Box("Detailed stone worktop",new Vector3(0,1,5),new Vector3(8.2f,.08f,1.22f),Art.Cream);
            for(int i=0;i<6;i++) {Art.Box("Cupboard seam",new Vector3(-3.5f+i*1.4f,.5f,4.443f),new Vector3(.015f,.82f,.01f),Art.Navy,false);Art.Box("Cupboard pull",new Vector3(-3+i*1.35f,.81f,4.40f),new Vector3(.3f,.035f,.05f),Art.Gold,false);}
            for(int x=0;x<14;x++)for(int y=0;y<3;y++)Art.Box("Backsplash tile",new Vector3(-4+x*.6f,1.4f+y*.4f,5.55f),new Vector3(.58f,.38f,.04f),x%3==0?Art.White:Art.Cream,false);
            Art.Box("Broken espresso machine",new Vector3(0,1.55f,5),new Vector3(1.75f,1.04f,.8f),Art.Metal);
            Art.Box("Machine front",new Vector3(0,1.63f,4.58f),new Vector3(1.58f,.8f,.035f),Art.Navy,false);
            Art.Box("Drip tray",new Vector3(0,1.08f,4.43f),new Vector3(1.5f,.04f,.53f),Art.Ink);
            Art.Shape("Brass spout",PrimitiveType.Cylinder,new Vector3(0,1.53f,4.36f),new Vector3(.13f,.16f,.13f),Art.Gold,false);
            k.nozzle=Art.Group("Cup placement target",new Vector3(0,1.2f,4.28f));
            var spray=Art.Group("Coffee spray",new Vector3(0,1.38f,4.28f));spray.localRotation=Quaternion.Euler(20,180,0);k.spillSpray=spray.gameObject.AddComponent<ParticleSystem>();k.spillSpray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=k.spillSpray.main;main.playOnAwake=false;main.loop=false;main.duration=.6f;main.startLifetime=.7f;main.startSpeed=1.6f;main.startSize=.045f;main.startColor=new Color(.24f,.12f,.07f);main.gravityModifier=1;main.maxParticles=60;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=k.spillSpray.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,28)});var shape=k.spillSpray.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=24;shape.radius=.025f;var renderer=k.spillSpray.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=Geometry.Get(PrimitiveType.Sphere);renderer.sharedMaterial=Art.Wood;
            var mark=Art.Shape("Cup placement mark",PrimitiveType.Cylinder,new Vector3(0,1.107f,4.28f),new Vector3(.42f,.005f,.42f),Art.Teal,false);
            Art.Button("Brew coffee","BREW",new Vector3(.56f,1.65f,4.52f),new Vector3(.42f,.3f,.08f),Art.Coral,ActionKind.Brew,d);
            k.display=Art.Text("Machine display","READY?\nOne ordinary task.",new Vector3(-.24f,1.82f,4.549f),.078f,ivory,.82f);
            Art.Button("Ask for support","ASK FOR HELP",new Vector3(2.12f,1.42f,4.48f),new Vector3(1.7f,.5f,.14f),Art.Teal,ActionKind.Help,d);
            Art.Text("Help permission","Available from the start.\nYou do not have to earn it.",new Vector3(2.12f,1.89f,4.44f),.12f,dark,2.1f);
            var cup=Art.Shape("Your cup",PrimitiveType.Cylinder,new Vector3(-1.7f,1.23f,4.6f),new Vector3(.25f,.17f,.25f),Art.White);var rb=cup.AddComponent<Rigidbody>();rb.mass=.2f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;k.cup=cup.AddComponent<Grabbable>();k.cup.label="Your cup";k.cup.room=3;k.cup.upright=true;
            // A flat-bottomed collider that matches the faceted cup. The primitive's capsule has a round base and tips over.
            Object.DestroyImmediate(cup.GetComponent<Collider>());var cupCollider=cup.AddComponent<MeshCollider>();cupCollider.sharedMesh=cup.GetComponent<MeshFilter>().sharedMesh;cupCollider.convex=true;
            Art.Shape("Cup interior",PrimitiveType.Cylinder,new Vector3(0,1.001f,0),new Vector3(.88f,.012f,.88f),Art.Ink,false,cup.transform);
            Art.Box("Cup handle",new Vector3(.63f,.1f,0),new Vector3(.45f,.6f,.22f),Art.White,false,cup.transform);
            k.coffee=Art.Shape("Coffee",PrimitiveType.Cylinder,new Vector3(0,.98f,0),new Vector3(.85f,.03f,.85f),Art.Wood,false,cup.transform).transform;k.coffee.gameObject.SetActive(false);
            // Send the cup under the spout from your seat: point at the drip tray while holding it and let go.
            var glow=Art.Shape("Spout highlight",PrimitiveType.Cylinder,new Vector3(0,1.112f,4.28f),new Vector3(.52f,.004f,.52f),d.player.rayMaterial,false);glow.SetActive(false);
            Target(mark,3,new Vector3(0,.18f,0),.45f,"Point under the spout and let go.",glow,k.cup);
            // A table in the audience area echoes the detailed before-world.
            Art.Box("Shared table",new Vector3(3,.76f,.3f),new Vector3(3,.12f,1.7f),Art.Wood);
            for(int s=-1;s<=1;s+=2)Art.Box("Table leg",new Vector3(3+s*1.2f,.37f,.3f),new Vector3(.08f,.74f,1.2f),Art.Metal);
            // The crowd gathers at both sides of the machine, facing the seat, within 60 degrees of it: a seated player
            // facing the coffee sees it grow from the corner of the eye without turning round, and it never blocks the cup or HELP.
            Vector3[] places={new Vector3(-2.7f,0,3.6f),new Vector3(2.9f,0,3.5f),new Vector3(-3.6f,0,4),new Vector3(3.8f,0,4),new Vector3(-2.2f,0,2.9f),new Vector3(2.3f,0,2.8f),new Vector3(-4.4f,0,4),new Vector3(4.2f,0,4)};
            var watchers=new List<GameObject>();for(int i=0;i<8;i++){var group=Art.Group("Audience "+(i+1),places[i]);Art.Person(Vector3.zero,group);var away=places[i]-new Vector3(seat.x,0,seat.z);group.localRotation=Quaternion.LookRotation(new Vector3(away.x,0,away.z));group.gameObject.SetActive(false);watchers.Add(group.gameObject);}k.crowd=watchers.ToArray();
            var spills=new List<GameObject>();for(int i=0;i<8;i++){var s=Art.Shape("Coffee spill "+i,PrimitiveType.Cylinder,new Vector3(-.75f+(i%4)*.55f,.012f,3.8f-(i/4)*.6f),new Vector3(.5f+i*.055f,.006f,.38f),Art.Wood,false);s.SetActive(false);spills.Add(s);}k.spills=spills.ToArray();
            Art.Plant(new Vector3(-4.8f,0,5));
            Sound("Ambience - Kitchen",.5f);
            var murmur=Art.Group("Crowd murmur",new Vector3(0,1.6f,2.5f));Sound("Ambience - Kitchen murmur",0,murmur);k.murmur=murmur.GetComponent<AudioSource>();
        }
        static void Archive()
        {
            Shell(18,53,Art.Navy,false);d.rooms[4].total=6;
            Header(4,"04 / THE INFINITE ARCHIVE","Not every thought is a fact.","Pick up a note, point at its tray, and let go. You do not need to sort the whole room.",new Vector3(0,3.55f,7.4f),9);
            for(int x=-3;x<=3;x++)for(int z=0;z<13;z++)
            {
                if(x==0||Mathf.Abs(x)==1&&z<3)continue;
                var cab=Art.Group("Repeating cabinet",new Vector3(x*2.55f,0,3+z*3.3f));
                Art.Box("Cabinet body",new Vector3(0,1.25f,0),new Vector3(1.5f,2.5f,.95f),z%3==0?Art.Teal:Art.Metal,true,cab);
                for(int drawer=0;drawer<4;drawer++){float y=.33f+drawer*.61f;Art.Box("Drawer",new Vector3(0,y,-.492f),new Vector3(1.39f,.55f,.025f),Art.Navy,false,cab);Art.Box("Label slot",new Vector3(0,y+.11f,-.52f),new Vector3(.39f,.1f,.03f),Art.Paper,false,cab);Art.Box("Handle",new Vector3(0,y-.09f,-.57f),new Vector3(.36f,.04f,.065f),Art.Gold,false,cab);}
            }
            for(int z=0;z<13;z++){Art.Box("Archive ceiling beam",new Vector3(0,4.5f,z*3.3f+3),new Vector3(18,.15f,.18f),Art.Metal,false);Art.Box("Light in the distance",new Vector3(0,4.32f,z*3.3f+3),new Vector3(1.6f,.03f,.12f),Art.White,false);}
            Art.Box("Sorting desk",new Vector3(0,.72f,1.25f),new Vector3(6.9f,.15f,1.2f),Art.Wood);
            for(int x=-1;x<=1;x+=2)Art.Box("Desk support",new Vector3(x*2.8f,.36f,1.25f),new Vector3(.12f,.72f,1),Art.Metal);
            string[] labels={"My job ended.","I lost my routine.","I will never\nwork again.","Everyone must\nthink I failed.","My skills\nstill exist.","I can ask\nfor support."};string[] cats={"FACT","FACT","FEAR","FEAR","STILL TRUE","STILL TRUE"};
            for(int i=0;i<6;i++){var item=Item(labels[i],cats[i],4,new Vector3(-2.8f+i*1.1f,1.05f,1.05f),new Vector3(.9f,.48f,.06f),Art.Paper);var txt=item.GetComponentInChildren<TextMeshPro>();txt.color=dark;
                // Undo the card's flat proportions so letters keep their shape, and keep the words on the card.
                txt.transform.localScale=new Vector3(.48f/.9f,1,1);txt.rectTransform.sizeDelta=new Vector2(1.7f,.84f);
                txt.enableAutoSizing=true;txt.fontSizeMin=1.1f;txt.fontSizeMax=2.35f;txt.fontSize=2.35f;}
            for(int i=0;i<3;i++)
            {
                string name=new[]{"FACT","FEAR","STILL TRUE"}[i];Vector3 p=new Vector3(-2.3f+i*2.3f,0,4.45f);
                Art.Box(name+" plinth",p+Vector3.up*.45f,new Vector3(1.8f,.9f,1.15f),i==1?Art.Coral:Art.Teal);
                Art.Text(name+" sign",name,p+new Vector3(0,1.5f,-.2f),.25f,ivory,2.1f);
                Art.Text(name+" description",new[]{"What happened","What my mind predicts","What I still carry"}[i],p+new Vector3(0,1.13f,-.59f),.13f,muted,2);
                var tray=Zone(name+" tray",p+new Vector3(0,1.13f,0),new Vector3(1.65f,.6f,1.04f),4,name,2);
                Target(tray.gameObject,4,new Vector3(0,.12f,0),.75f,"Point at a tray and let go.",Outline(name+" tray highlight",p+new Vector3(0,.93f,0),new Vector2(1.84f,1.19f),false));
            }
            Sound("Ambience - Archive",.6f);
        }
        static void Rooftop()
        {
            Sound("Ambience - Rooftop",.55f);Sound("Music - After hours theme",.2f);
            Floor(new Vector3(0,-.15f,3),new Vector3(17,.3f,19),Art.Cream);
            for(int s=-1;s<=1;s+=2)Art.Box("Rooftop parapet",new Vector3(s*8.5f,.6f,3),new Vector3(.3f,1.2f,19),Art.Teal);
            Art.Box("Rooftop edge",new Vector3(0,.6f,12.5f),new Vector3(17,1.2f,.3f),Art.Teal);
            d.ending=Art.Text("Ending","You can rest\nbefore you are ready.",new Vector3(0,3.6f,9),.78f,dark,12);
            Art.Text("Epilogue","The office is only one room in your life.",new Vector3(0,2.3f,9),.28f,dark,11);
            Art.Text("Next step","For now: a glass of water. A message to someone. A little daylight.\nYou get to choose the next small thing.",new Vector3(0,1.45f,9),.19f,dark,10);
            for(int i=0;i<26;i++){float x=-30+i*2.4f;float height=3+(i*7%9);Art.Box("Low poly city",new Vector3(x,-2+height*.5f,25+(i%3)*5),new Vector3(1.8f,height,2),i%2==0?Art.Teal:Art.Navy,false);}
            Art.Shape("Tomorrow's sun",PrimitiveType.Sphere,new Vector3(13,16,45),Vector3.one*9,Art.Gold,false);
            for(int side=-1;side<=1;side+=2){Art.Plant(new Vector3(side*5.5f,0,5));Art.Plant(new Vector3(side*5.8f,0,7));Art.Box("Bench seat",new Vector3(side*4,.5f,2.5f),new Vector3(2.3f,.15f,.65f),Art.Wood);for(int leg=-1;leg<=1;leg+=2)Art.Box("Bench leg",new Vector3(side*4+leg*.8f,.24f,2.5f),new Vector3(.1f,.48f,.55f),Art.Metal);}
        }
    }
}
