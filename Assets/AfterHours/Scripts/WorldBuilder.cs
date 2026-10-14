using System.Collections.Generic;
using System.Linq;
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
            d=director;d.rooms=new MindRoom[8];backings.Clear();fits.Clear();
            NewRoom(0,"The lobby",Vector3.zero,new Vector3(0,.05f,-4));Hub();
            NewRoom(1,"Ocean of shame",new Vector3(80,0,0),new Vector3(0,.3f,-1));Ocean();
            NewRoom(2,"Heavy things",new Vector3(160,0,0),new Vector3(0,.04f,-2.4f));Warehouse();
            NewRoom(3,"A small spill",new Vector3(240,0,0),new Vector3(0,.04f,1.4f));Kitchen();
            NewRoom(4,"The infinite archive",new Vector3(320,0,0),new Vector3(0,.04f,-1.2f));Archive();
            NewRoom(5,"Room for tomorrow",new Vector3(400,0,0),new Vector3(0,.04f,-4));Rooftop();
            NewRoom(6,"The old résumé",new Vector3(480,0,0),new Vector3(0,.04f,-1.4f));Shredding();
            NewRoom(7,"The rage room",new Vector3(560,0,0),new Vector3(0,.04f,-1.25f));Rage();
            Menu();
            FitPlates();
            // Signs that only guide or reassure can be switched off together (clean mode). Labels on buttons, doors and objects stay.
            foreach(var room in d.rooms)foreach(var t in room.root.GetComponentsInChildren<TextMeshPro>(true))if(GuidanceNames.Contains(t.name)&&!t.GetComponent<Guidance>())t.gameObject.AddComponent<Guidance>();
            // A plate behind a guidance sign is part of that guidance, so clean mode removes both.
            foreach(var (room,text,plate) in backings)foreach(var g in d.rooms[room].root.GetComponentsInChildren<Guidance>(true))if(g.name==text){var all=new List<GameObject>(g.backing??new GameObject[0]){plate};g.backing=all.ToArray();}
            for(int i=0;i<d.rooms.Length;i++)d.rooms[i].root.SetActive(i==0);
        }
        static readonly List<(int room,string text,GameObject plate)> backings=new List<(int,string,GameObject)>();
        // Registers a plate that backs the guidance sign of that name in that room.
        static GameObject Backed(int room,string text,GameObject plate){backings.Add((room,text,plate));return plate;}

        // ----- Plates that fit their words -----
        // A plate is drawn roughly the right size where it is placed. Once the room is built, FitPlates measures the words that stand on it
        // and settles the size: the words grow (up to 'grow' times) or shrink until they fill the plate's outline less 'pad' metres on every
        // side, then the plate closes in around them, so the margin is the same all the way round. A plate never ends up bigger than it was
        // drawn, so nothing it covered before is covered now. Plates in one 'group' (the three archive trays) share a size.
        sealed class Fit{public GameObject plate;public float pad,grow;public string group;public List<TextMeshPro> texts=new List<TextMeshPro>();public Vector2 size;public Vector3 centre;}
        static readonly List<Fit> fits=new List<Fit>();
        static GameObject Fitted(GameObject plate,float pad=.07f,float grow=1f,string group=null){fits.Add(new Fit{plate=plate,pad=pad,grow=grow,group=group});return plate;}
        // Signs whose words change while playing: every wording is measured, so the plate holds the longest.
        static readonly Dictionary<string,string[]> Wordings=new Dictionary<string,string[]>{{"Distance",new[]{"14   SMALL STROKES TO SHORE","YOU MADE IT TO THE SHORE"}}};
        static List<string> WordingsOf(TextMeshPro t)
        {
            var list=new List<string>{t.text};var input=t.GetComponent<InputText>();
            if(input){list.Add(input.hands);list.Add(input.controllers);}
            if(Wordings.TryGetValue(t.name,out var more))list.AddRange(more);
            return list.Where(w=>!string.IsNullOrEmpty(w)).Distinct().ToList();
        }
        // The rectangle the words cover, in the plate's own frame: x right, y up, metres from the plate's centre.
        static Vector4 Extent(List<TextMeshPro> texts,Quaternion inverse,Vector3 centre)
        {
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var t in texts)
            {
                var wordings=WordingsOf(t);var original=t.text;var toParent=Matrix4x4.TRS(t.transform.localPosition,t.transform.localRotation,t.transform.localScale);
                foreach(var words in wordings)
                {
                    t.text=words;t.ForceMeshUpdate(true,true);var b=t.textBounds;
                    for(int k=0;k<4;k++)
                    {
                        var corner=toParent.MultiplyPoint3x4(new Vector3(k%2==0?b.min.x:b.max.x,k<2?b.min.y:b.max.y,0));var l=inverse*(corner-centre);
                        minX=Mathf.Min(minX,l.x);maxX=Mathf.Max(maxX,l.x);minY=Mathf.Min(minY,l.y);maxY=Mathf.Max(maxY,l.y);
                    }
                }
                t.text=original;t.ForceMeshUpdate(true,true);
            }
            return new Vector4(minX,minY,maxX,maxY);
        }
        static void FitPlates()
        {
            foreach(var f in fits)
            {
                var tr=f.plate.transform;var parent=tr.parent;var rot=tr.localRotation;var inverse=Quaternion.Inverse(rot);var centre=tr.localPosition;var size=tr.localScale;
                // The words that stand on this plate: same parent and facing, inside its outline, just in front of it.
                foreach(var t in parent.GetComponentsInChildren<TextMeshPro>(false))
                {
                    if(t.transform.parent!=parent||t.enableAutoSizing||Quaternion.Angle(t.transform.localRotation,rot)>3f)continue;
                    var off=inverse*(t.transform.localPosition-centre);
                    if(Mathf.Abs(off.x)>size.x/2+.03f||Mathf.Abs(off.y)>size.y/2+.03f||off.z>.02f||off.z<-.3f)continue;
                    f.texts.Add(t);
                }
                if(f.texts.Count==0){Debug.LogWarning("Plate '"+f.plate.name+"' has no words to fit.");continue;}
                var e=Extent(f.texts,inverse,centre);
                float s=Mathf.Min(f.grow,(size.x-2*f.pad)/(e.z-e.x),(size.y-2*f.pad)/(e.w-e.y));
                if(s<.6f)Debug.LogWarning("Plate '"+f.plate.name+"' is small for its words; they were shrunk to "+Mathf.RoundToInt(s*100)+"%.");
                if(Mathf.Abs(s-1)>.01f)
                {
                    foreach(var t in f.texts)
                    {
                        t.fontSize*=s;t.rectTransform.sizeDelta*=s;
                        var off=inverse*(t.transform.localPosition-centre);off.x*=s;off.y*=s;t.transform.localPosition=centre+rot*off;
                    }
                    e=Extent(f.texts,inverse,centre);
                }
                f.centre=centre+rot*new Vector3((e.x+e.z)/2,(e.y+e.w)/2,0);f.size=new Vector2(e.z-e.x+2*f.pad,e.w-e.y+2*f.pad);
            }
            foreach(var group in fits.Where(f=>f.group!=null&&f.texts.Count>0).GroupBy(f=>f.group))
            {
                var wide=new Vector2(group.Max(f=>f.size.x),group.Max(f=>f.size.y));foreach(var f in group)f.size=wide;
            }
            foreach(var f in fits)
            {
                if(f.texts.Count==0)continue;
                var tr=f.plate.transform;tr.localPosition=f.centre;tr.localScale=new Vector3(f.size.x,f.size.y,tr.localScale.z);
                f.plate.GetComponent<MeshFilter>().sharedMesh=Geometry.Get(PrimitiveType.Cube,tr.lossyScale,Geometry.Bevel);
            }
            fits.Clear();
        }
        static readonly string[] GuidanceNames={"Chapter","Room title","Room subtitle","Room progress","Brand eyebrow","Title","Tagline","Welcome","Journey progress","Move instructions","Small sessions","Comfort note","Desk note","Rules","Rowing instruction","Distance","Ocean title","Ocean intention","Help permission"};
        // Each room has its own soundscape. Sources play when their room is switched on.
        static AudioSource Sound(string clip,float volume,Transform parent=null)
        {
            var source=(parent?parent:Art.Root).gameObject.AddComponent<AudioSource>();
            source.clip=SoundBank.Get(clip);source.loop=true;source.playOnAwake=true;source.spatialBlend=0;source.volume=volume;source.priority=64;
            return source;
        }
        static void NewRoom(int i,string title,Vector3 at,Vector3 spawn)
        {
            Art.Root=new GameObject(i.ToString("00")+" - "+title).transform;Art.Root.position=at;
            d.rooms[i]=new MindRoom{title=title,root=Art.Root.gameObject,spawn=Art.Group("Arrival",spawn)};
        }
        static void Floor(Vector3 p,Vector3 size,Material material)
        {Art.Tiled("Walkable floor",p,size,material,new Vector3(1.6f,0,1.6f),castShadows:false).AddComponent<TeleportSurface>();}
        // An opening in the left wall from z=gapFrom to z=gapTo leaves room for something built into it (the lobby mirror).
        static void Shell(float width,float depth,Material floor, bool ceiling=true,float gapFrom=0,float gapTo=0)
        {
            Floor(new Vector3(0,-.15f,depth*.5f-5),new Vector3(width,.3f,depth),floor);
            // Walls are panelled and the ceiling is coffered: the bevelled seams give large surfaces scale and catch the light.
            bool gap=gapTo>gapFrom;
            if(!gap)Art.Tiled("Left wall",new Vector3(-width/2,2.3f,depth*.5f-5),new Vector3(.2f,4.6f,depth),Art.Navy,new Vector3(0,0,2.4f));
            else
            {
                if(gapFrom>-5)Art.Tiled("Left wall",new Vector3(-width/2,2.3f,(gapFrom-5)*.5f),new Vector3(.2f,4.6f,gapFrom+5),Art.Navy,new Vector3(0,0,2.4f));
                Art.Tiled("Left wall",new Vector3(-width/2,2.3f,(gapTo+depth-5)*.5f),new Vector3(.2f,4.6f,depth-5-gapTo),Art.Navy,new Vector3(0,0,2.4f));
            }
            Art.Tiled("Right wall",new Vector3(width/2,2.3f,depth*.5f-5),new Vector3(.2f,4.6f,depth),Art.Navy,new Vector3(0,0,2.4f));
            Art.Tiled("Back wall",new Vector3(0,2.3f,depth-5),new Vector3(width,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Entrance wall",new Vector3(0,2.3f,-5.1f),new Vector3(width,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            if(ceiling)Art.Tiled("Ceiling",new Vector3(0,4.65f,depth*.5f-5),new Vector3(width,.15f,depth),Art.Ink,new Vector3(3,0,3),false);
            for(float z=-2;z<depth-5;z+=4)
            {
                Art.Box("Ceiling luminous strip",new Vector3(0,4.5f,z),new Vector3(width*.7f,.035f,.08f),Art.White,false);
                if(!gap||z-1.9f>gapTo||z+1.9f<gapFrom)Art.Box("Skirting left",new Vector3(-width/2+.13f,.14f,z),new Vector3(.06f,.1f,3.8f),Art.Gold,false);
                Art.Box("Skirting right",new Vector3(width/2-.13f,.14f,z),new Vector3(.06f,.1f,3.8f),Art.Gold,false);
            }
        }
        // The four lines of a room's heading. A heading that has to clear furniture can turn toward the player (yaw), pack its lines closer (spacing) and shrink (scale).
        static void Header(int room,string kicker,string title,string sub,Vector3 at,float width=8,float yaw=0,float spacing=1,float scale=1,float subScale=1)
        {
            var turn=Quaternion.Euler(0,yaw,0);
            Art.Text("Chapter",kicker,at+Vector3.up*.65f*spacing,.17f*scale,muted,width).transform.localRotation=turn;
            Art.Text("Room title",title,at,.55f*scale,ivory,width).transform.localRotation=turn;
            Art.Text("Room subtitle",sub,at+Vector3.down*.55f*spacing,.18f*scale*subScale,muted,width).transform.localRotation=turn;
            var progress=Art.Text("Room progress","TAKE YOUR TIME",at+Vector3.down*.93f*spacing,.15f*scale,new Color(.9f,.66f,.38f),width);progress.transform.localRotation=turn;d.rooms[room].progress=progress;
        }
        // Comfort, turning and room options in one panel that the menu button opens. It belongs to the player, not a room.
        static void Menu()
        {
            var root=Art.Group("Comfort menu",Vector3.zero,d.player.transform);
            var menu=root.gameObject.AddComponent<ComfortMenu>();menu.director=d;d.menu=menu;d.player.menu=menu;
            // Two columns, about 44 degrees wide at arm's length, so the whole menu fits a narrower field of view.
            Art.Box("Menu panel",new Vector3(0,-.068f,.014f),new Vector3(.53f,.636f,.012f),Art.Ink,true,root,.006f);
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
            menu.clean=MenuButton(root,"Menu clean mode","CLEAN MODE",-.12f,-.215f,Art.Navy,ActionKind.CleanMode);
            menu.night=MenuButton(root,"Menu ocean at night","OCEAN AT NIGHT",.12f,-.215f,Art.Navy,ActionKind.NightOcean);
            MenuButton(root,"Menu try again","TRY AGAIN",-.12f,-.30f,Art.Navy,ActionKind.ResetRoom);menu.tryAgain=root.Find("Menu try again").gameObject;
            MenuButton(root,"Menu lobby","LOBBY",.12f,-.30f,Art.Teal,ActionKind.Rest);menu.lobby=root.Find("Menu lobby").gameObject;
            Words(Art.Text("Menu tip","",new Vector3(0,-.348f,-.002f),.016f,muted,.5f,root),"Palm pinch again closes everything.  LOBBY: back to the start.\nTeleports turn you to face where you point.","Menu button again closes everything.  B / Y: lobby.\nRight stick turns; pull it back to turn around.");
            // A panel floating at arm's length should not throw a shadow onto the room.
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            menu.Refresh();root.gameObject.SetActive(false);
        }
        // A sign that says one thing to a player using hands and another to a player using controllers, and follows them if they switch.
        static TextMeshPro Words(TextMeshPro text,string hands,string controllers)
        {
            var words=text.gameObject.AddComponent<InputText>();words.hands=hands;words.controllers=controllers;text.text=hands;return text;
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
            Shell(14,21,Art.Cream,true,-5,-.4f);
            Art.Box("Midnight runner",new Vector3(0,.012f,4.5f),new Vector3(3.5f,.025f,18),Art.Teal,false);
            for(int side=-1;side<=1;side+=2)Art.Box("Runner piping",new Vector3(side*1.74f,.032f,4.5f),new Vector3(.025f,.015f,18),Art.Gold,false);
            // Everything on the far wall sits above the line of sight over the console (about 2.45 m at the arrival point), so nothing hides it.
            Art.Text("Brand eyebrow","A WALK THROUGH THE OFFICE OF YOUR MIND",new Vector3(0,4.4f,13.7f),.28f,muted,12);
            Art.Text("Title","AFTER HOURS",new Vector3(0,3.78f,13.7f),1.0f,ivory,11);
            Art.Text("Tagline","A job ended. Your story did not.",new Vector3(0,3.22f,13.7f),.34f,muted,11);
            Door("01","Ocean of\nshame","A small boat.\nA light ahead.",new Vector3(-6.82f,0,1.1f),-90,Art.Teal,1);
            Door("02","Heavy\nthings","Give your feelings\na place to go.",new Vector3(6.82f,0,1.1f),90,Art.Coral,2);
            Door("03","A small\nspill","The machine is broken.\nYou are allowed help.",new Vector3(-6.82f,0,7),-90,Art.Gold,3);
            Door("04","The infinite\narchive","A fact, a fear,\nand what is still true.",new Vector3(6.82f,0,7),90,Art.Teal,4);
            Door("05","The old\nrésumé","Let go of who you\nwere for them.",new Vector3(6.82f,0,-2.6f),90,Art.Gold,6);
            Door("06","The rage\nroom","Break the old work\ncomputer. Let it out.",new Vector3(-6.82f,0,4.05f),-90,Art.Coral,RageRoom.Room);
            // A detailed, polished memory occupies the end of each side of the lobby.
            MemoryOffice(new Vector3(-4.6f,0,12));MemoryOffice(new Vector3(4.6f,0,12));
            Art.Box("Reception console",new Vector3(0,.62f,-.3f),new Vector3(4f,1.24f,.65f),Art.Navy);
            Art.Box("Brass console cap",new Vector3(0,1.25f,-.3f),new Vector3(4.1f,.06f,.72f),Art.Gold);
            Art.Text("Welcome","YOU CAN BEGIN ANYWHERE",new Vector3(0,1.07f,-.64f),.17f,ivory,3.4f);
            d.lobbyProgress=Art.Text("Journey progress","0 / 6   ROOMS EXPLORED\nYour pace. No score. No deadline.",new Vector3(0,.55f,-.645f),.13f,muted,3.4f);
            // The board hangs above the lines of sight from the arrival point to the side doors (they pass between 1.1 and 2.4 m here).
            Backed(0,"Move instructions",Fitted(Art.Box("Getting started board",new Vector3(3.5f,3.2f,2.85f),new Vector3(2.9f,1.35f,.08f),Art.Navy),.14f,1.4f));
            Words(Art.Text("Move instructions","",new Vector3(3.5f,3.2f,2.8f),.14f,ivory,2.7f),"POINT AND PINCH TO CHOOSE\nPinch an object to hold it. Point where it belongs and let go.\nLeft palm pinch: the menu.","POINT AND SQUEEZE TO CHOOSE\nTrigger or grip holds an object. Point where it belongs and let go.\nLeft menu button: the menu.");
            // BEGIN and the mirror button stand side by side on the console, no higher than 1.65 m, so they never cover the far wall.
            var resume=Art.Button("Continue last room","BEGIN / OCEAN OF SHAME",new Vector3(-1.02f,1.47f,-.3f),new Vector3(1.9f,.36f,.12f),Art.Teal,ActionKind.Resume,d);
            d.resumeLabel=Art.Root.Find("Continue last room label").GetComponent<TextMeshPro>();
            Art.Text("Small sessions","A FEW MINUTES IS ENOUGH\nEach small step saves. Return to the lobby to take a break.",new Vector3(0,.82f,-.64f),.105f,muted,4.4f);
            Words(Art.Text("Comfort note","",new Vector3(0,.2f,-.65f),.095f,muted,3.4f),"SITTING DOWN? OPEN THE MENU\nLeft palm pinch, then SEATED VIEW.\nThe menu's LOBBY button returns here from any room.","SITTING DOWN? OPEN THE MENU\nMenu button, then SEATED VIEW.\nB / Y returns here from any room.");
            Art.Button("Rooftop passage","ROOM FOR TOMORROW  /  ROOFTOP",new Vector3(0,2.72f,13.5f),new Vector3(4.2f,.55f,.16f),Art.Teal,ActionKind.Travel,d,5);
            Art.Button("Mirror and wardrobe","MIRROR  /  YOUR WARDROBE",new Vector3(1.02f,1.45f,-.3f),new Vector3(1.9f,.3f,.1f),Art.Coral,ActionKind.GoToMirror,d);
            d.mirrorLabel=Art.Root.Find("Mirror and wardrobe label").GetComponent<TextMeshPro>();
            // Door 06 stands between doors 01 and 03, so the left wall's plant moves on toward the memory office.
            Art.Plant(new Vector3(5.9f,0,4));Art.Plant(new Vector3(-6.2f,0,9.3f));
            for(int side=-1;side<=1;side+=2)Art.Plant(new Vector3(side*2.8f,0,10));
            Mirror();
            for(int i=0;i<4;i++){Art.Box("Stepping marker",new Vector3(0,.034f,3+i*2.2f),new Vector3(.06f,.015f,.22f),Art.Gold,false);}
            Guide();
            Sound("Ambience - Lobby",.5f);Sound("Music - After hours theme",.22f);
        }
        // A full-length mirror built into the lobby's left wall, with the wardrobe on either side of it. Behind the glass
        // is a mirror-image copy of this end of the lobby, and the player's reflection stands in it (see MirrorAvatar).
        static void Mirror()
        {
            // Group axes: +x runs along the wall toward door 01, -z faces into the lobby, +z goes behind the glass.
            var old=Art.Root;var wall=Art.Group("Mirror wall",new Vector3(-7,0,-2.85f));wall.localRotation=Quaternion.Euler(0,-90,0);Art.Root=wall;
            Art.Tiled("Mirror wall",new Vector3(-1.475f,2.3f,0),new Vector3(1.35f,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Mirror wall",new Vector3(1.625f,2.3f,0),new Vector3(1.65f,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            Art.Tiled("Mirror wall",new Vector3(0,3.5f,0),new Vector3(1.6f,2.2f,.2f),Art.Navy,new Vector3(2.4f,0,0));
            // The glass is solid: it stops walking, teleporting and pointing through it.
            // Tapping it three times is a small secret (see MirrorAvatar.Tap).
            var glass=Art.Box("Mirror glass",new Vector3(0,1.21f,-.03f),new Vector3(1.6f,2.38f,.04f),Art.Glass,true,null,.002f);
            var tap=glass.AddComponent<Interactable>();tap.kind=ActionKind.MirrorTap;tap.director=d;
            for(int s=-1;s<=1;s+=2)Art.Box("Mirror frame",new Vector3(s*.83f,1.22f,-.11f),new Vector3(.06f,2.44f,.06f),Art.Gold,false);
            Art.Box("Mirror frame",new Vector3(0,2.42f,-.11f),new Vector3(1.72f,.06f,.06f),Art.Gold,false);
            var wardrobe=wall.gameObject.AddComponent<Wardrobe>();wardrobe.director=d;d.wardrobe=wardrobe;
            wardrobe.nameplate=Art.Text("Mirror nameplate","YOUR WARDROBE",new Vector3(0,2.66f,-.13f),.11f,ivory,2.4f);
            // The wardrobe: how you look on the left, what you wear on the right. Each row steps back or forward.
            wardrobe.values=new TextMeshPro[Wardrobe.Rows.Length];
            for(int column=0;column<2;column++)
            {
                float x=column==0?-1.475f:1.55f;
                Art.Box("Wardrobe board",new Vector3(x,1.5f,-.13f),new Vector3(1.18f,1.5f,.04f),Art.Ink,false);
                Art.Text("Wardrobe heading",column==0?"YOU":"WHAT YOU WEAR",new Vector3(x,2.13f,-.16f),.085f,muted,1.1f);
                for(int k=0;k<4;k++)
                {
                    int row=column*4+k;float y=1.86f-k*.28f;
                    Art.Text("Wardrobe row",Wardrobe.Rows[row],new Vector3(x,y+.08f,-.16f),.06f,muted,1f);
                    Art.Button("Wardrobe back "+row,"<",new Vector3(x-.42f,y-.04f,-.17f),new Vector3(.22f,.15f,.06f),Art.Teal,ActionKind.WardrobePrev,d,row);
                    Art.Button("Wardrobe next "+row,">",new Vector3(x+.42f,y-.04f,-.17f),new Vector3(.22f,.15f,.06f),Art.Teal,ActionKind.WardrobeNext,d,row);
                    var value=Art.Text("Wardrobe value "+row,"",new Vector3(x,y-.04f,-.16f),.07f,ivory,.58f);
                    value.rectTransform.sizeDelta=new Vector2(.58f,.2f);value.enableAutoSizing=true;value.fontSizeMin=.25f;value.fontSizeMax=.5f;
                    wardrobe.values[row]=value;
                }
                Art.Box("Wardrobe note board",new Vector3(x,.47f,-.13f),new Vector3(1.18f,.5f,.04f),Art.Navy,false);
                var note=Art.Text(column==0?"Still to find":"Wardrobe status","",new Vector3(x,.47f,-.16f),.05f,column==0?muted:ivory,1.08f);
                note.rectTransform.sizeDelta=new Vector2(1.1f,.46f);note.enableAutoSizing=true;note.fontSizeMin=.22f;note.fontSizeMax=.52f;
                if(column==0)wardrobe.toFind=note;else wardrobe.status=note;
            }
            // Where to stand: MIRROR on the desk brings the player here, facing the glass.
            d.mirrorSpot=Art.Group("Mirror spot",new Vector3(0,.04f,-1.7f));
            for(int s=-1;s<=1;s+=2)
            {
                Art.Box("Mirror spot marker",new Vector3(0,.02f,-1.7f+s*.35f),new Vector3(.8f,.012f,.05f),Art.Gold,false);
                Art.Box("Mirror spot marker",new Vector3(s*.4f,.02f,-1.7f),new Vector3(.05f,.012f,.7f),Art.Gold,false);
            }
            // The lobby seen in the mirror: a mirror-image copy of this end of the hall, only visible through the glass.
            Art.Tiled("Reflected floor",new Vector3(3.375f,-.15f,7.05f),new Vector3(11.25f,.3f,13.9f),Art.Cream,new Vector3(1.6f,0,1.6f),false,null,.035f,false);
            Art.Tiled("Reflected wall",new Vector3(-2.35f,2.3f,7.05f),new Vector3(.2f,4.6f,13.9f),Art.Navy,new Vector3(0,0,2.4f),false,null,.035f,false);
            Art.Tiled("Reflected wall",new Vector3(9.1f,2.3f,7.05f),new Vector3(.2f,4.6f,13.9f),Art.Navy,new Vector3(0,0,2.4f),false,null,.035f,false);
            Art.Tiled("Reflected wall",new Vector3(3.375f,2.3f,14.1f),new Vector3(11.25f,4.6f,.2f),Art.Navy,new Vector3(2.4f,0,0),false,null,.035f,false);
            Art.Tiled("Reflected ceiling",new Vector3(3.375f,4.65f,7.05f),new Vector3(11.25f,.15f,13.9f),Art.Ink,new Vector3(3,0,3),false,null,.035f,false);
            foreach(float z in new[]{-2f,2f,6f})Art.Box("Reflected light",new Vector3(z+2.85f,4.5f,7),new Vector3(.08f,.035f,9.8f),Art.White,false);
            Art.Box("Reflected runner",new Vector3(3.675f,.012f,7),new Vector3(10.65f,.025f,3.5f),Art.Teal,false);
            Art.Box("Reflected console",new Vector3(2.55f,.62f,7),new Vector3(.65f,1.24f,4f),Art.Navy,false);
            Art.Box("Reflected console cap",new Vector3(2.55f,1.25f,7),new Vector3(.72f,.06f,4.1f),Art.Gold,false);
            Art.Box("Reflected sign",new Vector3(2.55f,1.47f,5.98f),new Vector3(.12f,.36f,1.9f),Art.Teal,false);
            Art.Box("Reflected sign",new Vector3(2.55f,1.45f,8.02f),new Vector3(.1f,.3f,1.9f),Art.Coral,false);
            Art.Box("Reflected board",new Vector3(5.7f,1.9f,10.5f),new Vector3(.08f,1.35f,2.9f),Art.Navy,false);
            foreach(var (x,accent) in new[]{(.25f,Art.Gold),(3.95f,Art.Coral)})
            {
                Art.Box("Reflected door",new Vector3(x,1.4f,13.92f),new Vector3(2.3f,2.8f,.15f),Art.Ink,false);
                for(int s=-1;s<=1;s+=2)Art.Box("Reflected glow",new Vector3(x+s*1.18f,1.5f,13.8f),new Vector3(.075f,3,.1f),accent,false);
                Art.Box("Reflected lintel",new Vector3(x,3,13.8f),new Vector3(2.44f,.08f,.1f),accent,false);
                Art.Box("Reflected button",new Vector3(x,.95f,13.76f),new Vector3(1.45f,.4f,.15f),accent,false);
            }
            // The reflection itself: posed every frame in mirror space, then flipped across the glass by a negative scale.
            var plane=Art.Group("Reflection",Vector3.zero);plane.localRotation=Quaternion.Euler(0,180,0);
            var avatar=plane.gameObject.AddComponent<MirrorAvatar>();avatar.director=d;wardrobe.avatar=avatar;
            avatar.reflection=Art.Group("Reflected you",Vector3.zero,plane);
            Avatar(avatar);
            avatar.reflection.localScale=new Vector3(1,1,-1);
            // The X-ray's dark backdrop sits just behind the glass, switched off until the secret is found.
            var veil=Art.Box("X-ray backdrop",new Vector3(0,1.21f,.012f),new Vector3(1.58f,2.36f,.006f),Art.XrayVeil,false,null,0f);veil.SetActive(false);avatar.backdrop=veil;avatar.glass=glass.GetComponent<Renderer>();
            avatar.scanOn=SoundBank.Get("SFX - X-ray on");avatar.scanOff=SoundBank.Get("SFX - X-ray off");
            Art.Root=old;
            wardrobe.Refresh();
            avatar.PoseFor(d.mirrorSpot.position+Vector3.up*1.61f,Quaternion.LookRotation(Vector3.left),0);
        }
        // A faceted figure in the game's style, with every hair style and outfit piece built on and switched off.
        static void Avatar(MirrorAvatar m)
        {
            var root=m.reflection;var skin=new List<Renderer>();var looks=new List<OutfitLook>();
            Material tone=Art.Skins[2],hair=Art.Hairs[1];
            void Skin(GameObject g){skin.Add(g.GetComponent<Renderer>());}
            m.head=Art.Group("Avatar head",Vector3.zero,root);m.neck=Art.Group("Avatar neck",Vector3.zero,root);
            m.torso=Art.Group("Avatar torso",Vector3.zero,root);m.legs=Art.Group("Avatar legs",Vector3.zero,root);
            Skin(Art.Shape("Face",PrimitiveType.Sphere,Vector3.zero,new Vector3(.19f,.235f,.215f),tone,false,m.head));
            for(int s=-1;s<=1;s+=2)
            {
                Skin(Art.Shape("Ear",PrimitiveType.Sphere,new Vector3(s*.096f,-.005f,-.005f),new Vector3(.035f,.06f,.04f),tone,false,m.head));
                Art.Shape("Eye",PrimitiveType.Sphere,new Vector3(s*.042f,.015f,.099f),new Vector3(.026f,.03f,.018f),Art.Ink,false,m.head);
                Art.Box("Brow",new Vector3(s*.045f,.05f,.1f),new Vector3(.045f,.01f,.012f),Art.Ink,false,m.head,.003f);
            }
            Skin(Art.Box("Nose",new Vector3(0,-.015f,.108f),new Vector3(.03f,.05f,.03f),tone,false,m.head,.006f));
            Art.Box("Smile",new Vector3(0,-.062f,.098f),new Vector3(.05f,.008f,.01f),Art.Coral,false,m.head,.002f);
            // Hair styles, in Wardrobe.HairStyles order. Parts named Crown hide under a hat.
            var styles=new GameObject[Wardrobe.HairStyles.Length];
            for(int i=0;i<styles.Length;i++)styles[i]=Art.Group("Hair - "+Wardrobe.HairStyles[i],Vector3.zero,m.head).gameObject;
            Transform H(int i)=>styles[i].transform;
            foreach(int i in new[]{0,1,2,3})Art.Shape("Hair",PrimitiveType.Sphere,new Vector3(0,.045f,-.03f),new Vector3(.205f,.17f,.22f),hair,false,H(i));
            Art.Box("Hair fringe",new Vector3(0,.095f,.075f),new Vector3(.16f,.04f,.05f),hair,false,H(0),.01f);
            for(int k=0;k<8;k++){float a=k*Mathf.PI/4;Art.Shape("Crown curl",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.075f,.105f,Mathf.Sin(a)*.08f-.03f),Vector3.one*.075f,hair,false,H(1));}
            Art.Box("Hair back",new Vector3(0,-.1f,-.08f),new Vector3(.2f,.3f,.08f),hair,false,H(2),.02f);
            for(int s=-1;s<=1;s+=2)Art.Box("Hair side",new Vector3(s*.095f,-.07f,-.015f),new Vector3(.035f,.22f,.12f),hair,false,H(2),.01f);
            Art.Shape("Crown bun",PrimitiveType.Sphere,new Vector3(0,.115f,-.1f),Vector3.one*.09f,hair,false,H(3));
            m.hairStyles=styles;
            OutfitLook Look(string id,Transform parent,Material sleeve=null,bool longSleeves=false)
            {var group=Art.Group("Wear - "+id,Vector3.zero,parent);var look=new OutfitLook{id=id,group=group.gameObject,sleeve=sleeve,longSleeves=longSleeves};looks.Add(look);return look;}
            // Hats
            var cap=Look("cap",m.head).group.transform;
            Art.Shape("Cap crown",PrimitiveType.Sphere,new Vector3(0,.07f,-.02f),new Vector3(.225f,.16f,.235f),Art.Coral,false,cap);
            Art.Box("Cap brim",new Vector3(0,.075f,.1f),new Vector3(.19f,.018f,.13f),Art.Coral,false,cap,.006f).transform.localRotation=Quaternion.Euler(10,0,0);
            Art.Shape("Cap button",PrimitiveType.Sphere,new Vector3(0,.15f,-.02f),Vector3.one*.025f,Art.Gold,false,cap);
            var beanie=Look("beanie",m.head).group.transform;
            Art.Shape("Beanie",PrimitiveType.Sphere,new Vector3(0,.075f,-.025f),new Vector3(.23f,.19f,.24f),Art.Teal,false,beanie);
            Art.Shape("Beanie band",PrimitiveType.Cylinder,new Vector3(0,.07f,-.022f),new Vector3(.225f,.025f,.232f),Art.Navy,false,beanie);
            Art.Shape("Pompom",PrimitiveType.Sphere,new Vector3(0,.18f,-.03f),Vector3.one*.065f,Art.Gold,false,beanie);
            Skin(Art.Shape("Neck",PrimitiveType.Cylinder,new Vector3(0,.07f,-.01f),new Vector3(.085f,.075f,.085f),tone,false,m.neck));
            // The torso's pivot is mid-spine, so its top (+.305) is the base of the neck and its bottom (-.305) the hips.
            Art.Box("Belt",new Vector3(0,-.29f,0),new Vector3(.34f,.07f,.19f),Art.Ink,false,m.torso,.015f);
            Art.Box("Hips",new Vector3(0,-.36f,0),new Vector3(.33f,.14f,.18f),Art.Ink,false,m.torso,.02f);
            Transform Top(string id,Material body,bool longSleeves)
            {
                var t=Look(id,m.torso,body,longSleeves).group.transform;
                Art.Box("Top",Vector3.zero,new Vector3(.36f,.58f,.2f),body,false,t,.035f);
                for(int s=-1;s<=1;s+=2)Art.Shape("Top shoulder",PrimitiveType.Sphere,new Vector3(s*.165f,.235f,0),new Vector3(.14f,.12f,.19f),body,false,t);
                return t;
            }
            Top("tee",Art.Teal,false);
            var shirt=Top("shirt",Art.White,true);
            for(int s=-1;s<=1;s+=2)Art.Box("Collar",new Vector3(s*.045f,.27f,.085f),new Vector3(.075f,.05f,.02f),Art.White,false,shirt,.005f).transform.localRotation=Quaternion.Euler(0,0,-s*25);
            Art.Box("Tie knot",new Vector3(0,.245f,.104f),new Vector3(.04f,.035f,.02f),Art.Navy,false,shirt,.005f);
            Art.Box("Tie",new Vector3(0,.09f,.103f),new Vector3(.055f,.28f,.012f),Art.Navy,false,shirt,.004f);
            var jumper=Top("jumper",Art.Navy,true);
            foreach(float y in new[]{.11f,0f})Art.Box("Jumper stripe",new Vector3(0,y,0),new Vector3(.366f,.045f,.206f),Art.Cream,false,jumper,.01f);
            Art.Shape("Jumper collar",PrimitiveType.Cylinder,new Vector3(0,.29f,0),new Vector3(.15f,.02f,.13f),Art.Cream,false,jumper);
            var cardigan=Top("cardigan",Art.Wood,true);
            Art.Box("Shirt beneath",new Vector3(0,.04f,.1f),new Vector3(.09f,.5f,.012f),Art.Teal,false,cardigan,.003f);
            foreach(float y in new[]{.13f,.03f,-.07f})Art.Shape("Cardigan button",PrimitiveType.Sphere,new Vector3(.06f,y,.104f),Vector3.one*.02f,Art.Gold,false,cardigan);
            for(int s=-1;s<=1;s+=2)Art.Box("Cardigan pocket",new Vector3(s*.11f,-.17f,.102f),new Vector3(.08f,.065f,.01f),Art.Paper,false,cardigan,.004f);
            var jacket=Top("jacket",Art.Coral,true);
            Art.Box("Jacket zip",new Vector3(0,-.01f,.103f),new Vector3(.014f,.54f,.01f),Art.Gold,false,jacket,.002f);
            for(int s=-1;s<=1;s+=2)Art.Box("Jacket collar",new Vector3(s*.06f,.285f,.06f),new Vector3(.09f,.08f,.06f),Art.Coral,false,jacket,.01f).transform.localRotation=Quaternion.Euler(-15,0,-s*15);
            Art.Box("Jacket band",new Vector3(0,-.27f,0),new Vector3(.37f,.05f,.21f),Art.Gold,false,jacket,.01f);
            // Around the neck
            var scarf=Look("scarf",m.torso).group.transform;
            Art.Shape("Scarf",PrimitiveType.Cylinder,new Vector3(0,.31f,0),new Vector3(.21f,.045f,.2f),Art.Coral,false,scarf);
            Art.Box("Scarf end",new Vector3(.07f,.15f,.11f),new Vector3(.075f,.26f,.025f),Art.Coral,false,scarf,.008f).transform.localRotation=Quaternion.Euler(0,0,8);
            foreach(float y in new[]{.04f,.06f})Art.Box("Scarf fringe",new Vector3(.085f,y,.125f),new Vector3(.07f,.008f,.006f),Art.Gold,false,scarf,.001f);
            var lanyard=Look("lanyard",m.torso).group.transform;
            for(int s=-1;s<=1;s+=2)Art.Line("Lanyard strap",new Vector3(s*.065f,.3f,.08f),new Vector3(0,.04f,.11f),.014f,Art.Navy,lanyard);
            Art.Box("Lanyard badge",new Vector3(0,-.02f,.112f),new Vector3(.09f,.12f,.008f),Art.Cream,false,lanyard,.004f);
            Art.Box("Badge clip",new Vector3(0,.045f,.114f),new Vector3(.025f,.02f,.01f),Art.Gold,false,lanyard,.002f);
            foreach(float y in new[]{0f,-.025f,-.05f})Art.Box("Badge line",new Vector3(0,y,.117f),new Vector3(y==0?.06f:.045f,.007f,.003f),Art.Teal,false,lanyard,.001f);
            // Pins
            var stepPin=Look("step-pin",m.torso).group.transform;
            Art.Shape("Pin",PrimitiveType.Cylinder,new Vector3(-.095f,.15f,.106f),new Vector3(.045f,.005f,.045f),Art.Gold,false,stepPin).transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Shape("Pin centre",PrimitiveType.Sphere,new Vector3(-.095f,.15f,.11f),Vector3.one*.016f,Art.Coral,false,stepPin);
            var truePin=Look("true-pin",m.torso).group.transform;
            Art.Facet("Pin gem",new Vector3(-.095f,.15f,.11f),new Vector3(.045f,.06f,.022f),Art.Teal,4,truePin);
            Art.Shape("Pin spark",PrimitiveType.Sphere,new Vector3(-.095f,.158f,.118f),Vector3.one*.012f,Art.Gold,false,truePin);
            // The rage room's pin: a burst of coral with a gold centre.
            var letItOut=Look("letitout-pin",m.torso).group.transform;
            for(int k=0;k<4;k++)Art.Box("Pin burst",new Vector3(-.095f,.15f,.108f),new Vector3(.052f,.014f,.006f),Art.Coral,false,letItOut,.002f).transform.localRotation=Quaternion.Euler(0,0,k*45);
            Art.Shape("Pin centre",PrimitiveType.Sphere,new Vector3(-.095f,.15f,.113f),Vector3.one*.018f,Art.Gold,false,letItOut);
            // The kitchen's hidden pin: a little orange mug on a brass disc, with a dab of coffee on top.
            var orangePin=Look("orange-pin",m.torso).group.transform;
            Art.Shape("Pin disc",PrimitiveType.Cylinder,new Vector3(-.095f,.15f,.104f),new Vector3(.06f,.004f,.06f),Art.Gold,false,orangePin).transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Shape("Pin mug",PrimitiveType.Cylinder,new Vector3(-.098f,.15f,.11f),new Vector3(.036f,.02f,.036f),Art.Orange,false,orangePin);
            Art.Box("Pin mug handle",new Vector3(-.071f,.152f,.11f),new Vector3(.014f,.022f,.008f),Art.Orange,false,orangePin,.002f);
            Art.Shape("Pin coffee",PrimitiveType.Cylinder,new Vector3(-.098f,.1695f,.11f),new Vector3(.028f,.002f,.028f),Art.Wood,false,orangePin);
            // Arms are two fixed-length segments each, bent by the pose; hands follow tracked hands or controllers.
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                m.upperArm[i]=Art.Group(side+" upper arm",Vector3.zero,root);
                m.sleeveUpper[i]=Art.Box("Upper sleeve",new Vector3(0,0,.145f),new Vector3(.1f,.1f,.32f),Art.Teal,false,m.upperArm[i],.03f).GetComponent<Renderer>();
                m.foreArm[i]=Art.Group(side+" forearm",Vector3.zero,root);
                m.sleeveFore[i]=Art.Box("Forearm",new Vector3(0,0,.135f),new Vector3(.085f,.085f,.29f),tone,false,m.foreArm[i],.025f).GetComponent<Renderer>();
                m.hand[i]=Art.Group(side+" hand",Vector3.zero,root);
                Skin(Art.Box("Hand",new Vector3(0,0,.055f),new Vector3(.08f,.035f,.1f),tone,false,m.hand[i],.012f));
                Skin(Art.Box("Thumb",new Vector3((i==0?1:-1)*.045f,0,.03f),new Vector3(.025f,.025f,.055f),tone,false,m.hand[i],.008f));
                float x=i==0?-.1f:.1f;
                m.leg[i]=Art.Group(side+" leg",new Vector3(x,0,0),m.legs);
                Art.Box("Trouser leg",new Vector3(0,.47f,0),new Vector3(.14f,.86f,.16f),Art.Ink,false,m.leg[i],.03f);
                Art.Box("Shoe",new Vector3(x,.045f,.045f),new Vector3(.12f,.09f,.25f),Art.Navy,false,m.legs,.02f);
            }
            m.skin=skin.ToArray();m.looks=looks.ToArray();m.skins=Art.Skins;m.hairs=Art.Hairs;
            foreach(var look in looks)look.group.SetActive(false);
            m.body=root.GetComponentsInChildren<Renderer>(true);Skeleton(m);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // The X-ray skeleton, hidden until the mirror's secret is found. Every bone is a thin bar of white light with a wider, fainter copy as its glow,
        // and the parts hang from the same joints as the body, so the skeleton poses itself. Eye sockets and a nose hollow are dark.
        static void Skeleton(MirrorAvatar m)
        {
            var halos=new List<Renderer>();var parts=new List<GameObject>();
            Transform Part(string name,Transform parent){var g=Art.Group("X-ray - "+name,Vector3.zero,parent);g.gameObject.SetActive(false);parts.Add(g.gameObject);return g;}
            void Bone(Transform p,string name,Vector3 a,Vector3 b,float width,float glow)
            {
                Art.Line(name,a,b,width,Art.XrayBone,p);
                if(glow>0)halos.Add(Art.Line(name+" glow",a,b,width*glow,Art.XrayHalo,p).GetComponent<Renderer>());
            }
            void Knob(Transform p,string name,Vector3 at,Vector3 size,float glow)
            {
                Art.Shape(name,PrimitiveType.Sphere,at,size,Art.XrayBone,false,p);
                if(glow>0)halos.Add(Art.Shape(name+" glow",PrimitiveType.Sphere,at,size*glow,Art.XrayHalo,false,p).GetComponent<Renderer>());
            }
            void Slab(Transform p,string name,Vector3 at,Vector3 size,float glow,float roll=0)
            {
                Art.Box(name,at,size,Art.XrayBone,false,p,.01f).transform.localRotation=Quaternion.Euler(0,0,roll);
                if(glow>0)halos.Add(Art.Box(name+" glow",at,size*glow,Art.XrayHalo,false,p,.01f).GetComponent<Renderer>());
                if(glow>0)halos[halos.Count-1].transform.localRotation=Quaternion.Euler(0,0,roll);
            }
            void Hollow(Transform p,string name,PrimitiveType type,Vector3 at,Vector3 size){Art.Shape(name,type,at,size,Art.XrayHollow,false,p);}

            // Skull and jaw, with dark eye sockets, a nose hollow and the line between the teeth.
            var skull=Part("skull",m.head);
            Knob(skull,"Cranium",new Vector3(0,.025f,-.01f),new Vector3(.17f,.18f,.19f),1.35f);
            Slab(skull,"Cheekbones",new Vector3(0,-.05f,.03f),new Vector3(.1f,.05f,.1f),1.3f);
            Slab(skull,"Jaw",new Vector3(0,-.1f,.02f),new Vector3(.085f,.045f,.095f),1.3f);
            for(int s=-1;s<=1;s+=2)Hollow(skull,"Eye socket",PrimitiveType.Sphere,new Vector3(s*.04f,.012f,.088f),new Vector3(.04f,.045f,.03f));
            Hollow(skull,"Nose hollow",PrimitiveType.Sphere,new Vector3(0,-.032f,.098f),new Vector3(.02f,.032f,.02f));
            Hollow(skull,"Teeth gap",PrimitiveType.Cube,new Vector3(0,-.073f,.1f),new Vector3(.07f,.006f,.02f));
            var neck=Part("neck",m.neck);
            foreach(float y in new[]{.025f,.055f,.085f})Slab(neck,"Neck bone",new Vector3(0,y,-.012f),new Vector3(.04f,.022f,.042f),1.8f);
            // Spine, ribs, breastbone, collarbones and pelvis hang from the torso, which stretches with the player's height.
            var torso=Part("torso",m.torso);
            for(int k=0;k<9;k++)Slab(torso,"Vertebra",new Vector3(0,.265f-k*.066f,-.058f),new Vector3(.046f,.034f,.05f),1.6f);
            for(int k=0;k<6;k++)
            {
                float y=.235f-k*.05f,reach=k==5?.88f:1;
                for(int s=-1;s<=1;s+=2)
                {
                    var a=new Vector3(s*.012f,y,-.058f);var b=new Vector3(s*.14f*reach,y-.012f,-.02f);var c=new Vector3(s*.14f*reach,y-.03f,.06f);var e=new Vector3(s*.024f,y-.046f,.094f);
                    Bone(torso,"Rib",a,b,.013f,0);Bone(torso,"Rib",b,c,.013f,0);Bone(torso,"Rib",c,e,.013f,0);
                }
            }
            halos.Add(Art.Shape("Ribcage glow",PrimitiveType.Sphere,new Vector3(0,.1f,.015f),new Vector3(.4f,.4f,.26f),Art.XrayHalo,false,torso).GetComponent<Renderer>());
            Slab(torso,"Breastbone",new Vector3(0,.1f,.096f),new Vector3(.034f,.2f,.014f),1.8f);
            for(int s=-1;s<=1;s+=2)
            {
                Bone(torso,"Collarbone",new Vector3(s*.014f,.283f,.07f),new Vector3(s*.175f,.262f,.015f),.02f,2.2f);
                Slab(torso,"Hip bone",new Vector3(s*.095f,-.27f,-.005f),new Vector3(.1f,.11f,.05f),1.4f,s*20);
            }
            Slab(torso,"Sacrum",new Vector3(0,-.325f,-.04f),new Vector3(.08f,.1f,.04f),1.4f);
            Slab(torso,"Pelvis front",new Vector3(0,-.36f,.065f),new Vector3(.1f,.04f,.04f),1.4f);
            // Arms: a bone and joint at each segment, and a hand of fingers.
            var lengths=new[]{.068f,.08f,.074f,.06f};
            for(int i=0;i<2;i++)
            {
                float t=i==0?1:-1;
                var upper=Part("upper arm "+i,m.upperArm[i]);
                Knob(upper,"Shoulder joint",new Vector3(0,0,.005f),Vector3.one*.055f,1.8f);
                Bone(upper,"Upper arm bone",new Vector3(0,0,.02f),new Vector3(0,0,.27f),.03f,2.6f);
                var fore=Part("forearm "+i,m.foreArm[i]);
                Knob(fore,"Elbow",Vector3.zero,Vector3.one*.045f,1.8f);
                Bone(fore,"Radius",new Vector3(-.01f,0,.025f),new Vector3(-.008f,0,.255f),.016f,2.4f);
                Bone(fore,"Ulna",new Vector3(.012f,0,.025f),new Vector3(.01f,0,.265f),.016f,2.4f);
                var hand=Part("hand "+i,m.hand[i]);
                Knob(hand,"Wrist",new Vector3(0,0,.005f),new Vector3(.04f,.03f,.03f),1.6f);
                for(int f=0;f<4;f++)
                {
                    float x=(f-1.5f)*.02f;
                    Bone(hand,"Palm bone",new Vector3(x*.6f,0,.015f),new Vector3(x,0,.058f),.01f,0);
                    Bone(hand,"Finger bone",new Vector3(x,0,.06f),new Vector3(x*1.25f,0,.06f+lengths[f]),.008f,0);
                }
                Bone(hand,"Thumb bone",new Vector3(t*.015f,0,.012f),new Vector3(t*.052f,0,.06f),.011f,0);
                halos.Add(Art.Shape("Hand glow",PrimitiveType.Sphere,new Vector3(0,0,.085f),new Vector3(.085f,.04f,.17f),Art.XrayHalo,false,hand).GetComponent<Renderer>());
            }
            // Legs hang from the hips; feet stand on the floor with the shoes.
            var feet=Part("feet",m.legs);
            for(int i=0;i<2;i++)
            {
                float t=i==0?1:-1,x=i==0?-.1f:.1f;
                var leg=Part("leg "+i,m.leg[i]);
                Knob(leg,"Hip joint",new Vector3(0,.85f,0),Vector3.one*.07f,1.7f);
                Bone(leg,"Thigh bone",new Vector3(0,.84f,0),new Vector3(0,.49f,.012f),.04f,2.4f);
                Knob(leg,"Knee",new Vector3(0,.475f,.012f),Vector3.one*.06f,1.8f);
                Bone(leg,"Shin bone",new Vector3(0,.46f,.01f),new Vector3(0,.11f,0),.032f,2.4f);
                Bone(leg,"Calf bone",new Vector3(t*.022f,.44f,.008f),new Vector3(t*.022f,.11f,0),.014f,2.4f);
                Knob(leg,"Ankle",new Vector3(0,.09f,0),Vector3.one*.05f,1.7f);
                Knob(feet,"Heel",new Vector3(x,.035f,-.03f),Vector3.one*.05f,1.7f);
                Bone(feet,"Foot bone",new Vector3(x,.05f,-.02f),new Vector3(x,.03f,.12f),.03f,2.2f);
                for(int f=-1;f<=1;f++)Bone(feet,"Toe bone",new Vector3(x+f*.02f,.028f,.12f),new Vector3(x+f*.024f,.026f,.19f),.014f,0);
            }
            // The pieces share one transparent queue, so their draw order is set here: glow, then bones over it, then the dark hollows over those. The backdrop (order 0) draws before all.
            foreach(var g in parts)foreach(var r in g.GetComponentsInChildren<Renderer>(true))r.sortingOrder=r.sharedMaterial==Art.XrayHalo?1:r.sharedMaterial==Art.XrayBone?2:3;
            m.xray=parts.ToArray();m.halos=halos.ToArray();m.haloMaterial=Art.XrayHalo;
        }
        // The old résumé: five pages on a rack, a shredder, and a blank page at the end.
        static void Shredding()
        {
            Shell(12,15,Art.Cream);d.rooms[6].total=5;
            Header(6,"05 / THE OLD RÉSUMÉ","You are more than a page.","Let each old résumé go, one at a time. Then keep one true line.",new Vector3(0,3.45f,9.6f));
            // A night window: the office after hours, the city still lit.
            Art.Box("Night window",new Vector3(0,1.55f,9.86f),new Vector3(6,1.5f,.05f),Art.Ink,false);
            for(int i=0;i<4;i++)Art.Box("Window mullion",new Vector3(-3+i*2f,1.55f,9.82f),new Vector3(.06f,1.56f,.04f),Art.Navy,false);
            Art.Box("Window sill",new Vector3(0,.78f,9.78f),new Vector3(6.2f,.05f,.18f),Art.Gold,false);
            Art.Box("Window head",new Vector3(0,2.32f,9.82f),new Vector3(6.1f,.05f,.04f),Art.Navy,false);
            var random=new System.Random(6);float R()=>(float)random.NextDouble();
            for(int i=0;i<46;i++)Art.Box("City light",new Vector3(R()*5.6f-2.8f,.9f+R()*1.2f,9.83f),new Vector3(.05f,.04f,.01f),i%3==0?Art.White:Art.Gold,false);
            Art.Box("Desk",new Vector3(0,.74f,.45f),new Vector3(4.4f,.08f,.9f),Art.Wood);
            for(int s=-1;s<=1;s+=2)Art.Box("Desk leg",new Vector3(s*2.05f,.36f,.45f),new Vector3(.08f,.72f,.8f),Art.Metal);
            Art.Box("Page rack",new Vector3(0,.795f,.45f),new Vector3(4.1f,.03f,.2f),Art.Navy);
            for(int s=-1;s<=1;s+=2)Art.Box("Rack lip",new Vector3(0,.835f,.45f+s*.06f),new Vector3(4.1f,.05f,.02f),Art.Navy);
            string[] resumes={"SENIOR COORDINATOR\nsince 2016","Always available.\nNights and weekends.","Exceeded every\ntarget.","Ten years.\nOne company.","Reason for leaving:\n__________"};
            string[] farewells={"A title described a role. It was never the whole of you.","You were never meant to be reachable all the time.","Your worth was never a quarterly number.","Your loyalty is still yours. It goes with you.","You do not owe anyone a perfect explanation."};
            var pages=new Grabbable[resumes.Length];for(int i=0;i<pages.Length;i++)pages[i]=Page(resumes[i],new Vector3(-1.44f+i*.72f,1.087f,.45f));
            // The shredder: a bin with a window onto the paper, the cutting head and its slot. Big enough to read from the seat.
            const float z=3.3f,top=1.4f;
            Art.Box("Bin back",new Vector3(0,.39f,z+.297f),new Vector3(.875f,.775f,.03f),Art.Ink);
            for(int s=-1;s<=1;s+=2)Art.Box("Bin side",new Vector3(s*.42f,.39f,z),new Vector3(.035f,.775f,.625f),Art.Ink);
            Art.Box("Bin floor",new Vector3(0,.015f,z),new Vector3(.875f,.03f,.625f),Art.Ink);
            Art.Box("Bin rail",new Vector3(0,.06f,z-.297f),new Vector3(.875f,.12f,.03f),Art.Ink);
            Art.Box("Bin window",new Vector3(0,.42f,z-.3f),new Vector3(.8f,.58f,.01f),Art.Glass,true,null,.002f);
            Art.Box("Shredder head",new Vector3(0,top-.3125f,z),new Vector3(1f,.625f,.775f),Art.Metal);
            Art.Box("Shredder top",new Vector3(0,top+.015f,z),new Vector3(1.02f,.03f,.8f),Art.Navy);
            Art.Box("Feed slot",new Vector3(0,top+.032f,z),new Vector3(.7f,.006f,.05f),Art.Ink,false,null,.001f);
            for(int s=-1;s<=1;s+=2)Art.Box("Slot rim",new Vector3(0,top+.033f,z+s*.037f),new Vector3(.75f,.008f,.014f),Art.Gold,false,null,.002f);
            Art.Text("Shredder label","LET IT GO",new Vector3(0,top-.09f,z-.4f),.11f,ivory,.9f);
            Art.Shape("Ready light",PrimitiveType.Sphere,new Vector3(.38f,top-.1f,z-.39f),Vector3.one*.035f,Art.Mint,false);
            // Paper in the bin: each page adds a heap of strips and chips. A heap is two meshes (white and ivory), not dozens of small objects.
            var piles=new GameObject[pages.Length];
            for(int i=0;i<piles.Length;i++)
            {
                var pile=Art.Group("Paper pile "+(i+1),new Vector3(0,.05f+i*.095f,z));
                var heapWhite=new List<CombineInstance>();var heapPaper=new List<CombineInstance>();
                for(int k=0;k<56;k++)
                {
                    bool chip=k%4==3;float length=chip?.03f+R()*.035f:.09f+R()*.13f,width=chip?.02f+R()*.025f:.012f+R()*.012f;
                    Piece(k%2==0?heapWhite:heapPaper,new Vector3(R()*.44f-.22f,R()*.06f,R()*.28f-.14f),new Vector3(width,.012f,length),Quaternion.Euler(R()*16-8,R()*360,R()*16-8));
                }
                Scraps("Paper strips white",pile,heapWhite,Art.White);Scraps("Paper strips",pile,heapPaper,Art.Paper);
                pile.gameObject.SetActive(false);piles[i]=pile.gameObject;
            }
            var fall=Art.Group("Paper strips falling",new Vector3(0,.74f,z));fall.localRotation=Quaternion.Euler(90,0,0);
            var strips=fall.gameObject.AddComponent<ParticleSystem>();strips.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=strips.main;main.playOnAwake=false;main.loop=false;main.duration=1.3f;main.startLifetime=.6f;main.startSpeed=.25f;main.gravityModifier=.4f;main.maxParticles=60;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSize3D=true;main.startSizeX=.018f;main.startSizeY=.013f;main.startSizeZ=.14f;main.startRotation3D=true;main.startRotationY=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=strips.emission;emission.rateOverTime=26;var shape=strips.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.6f,.08f,.01f);
            var stripRenderer=strips.GetComponent<ParticleSystemRenderer>();stripRenderer.renderMode=ParticleSystemRenderMode.Mesh;stripRenderer.mesh=Geometry.Get(PrimitiveType.Cube);stripRenderer.sharedMaterial=Art.Paper;
            // Each page also sends a flurry of loose bits up out of the head. They tumble down across the room and come to rest on the floor...
            var floorPlane=Art.Group("Paper floor",new Vector3(0,.012f,0));
            var flurry=new[]{Flurry("Paper bits flying",new Vector3(0,top+.05f,z),Art.White,floorPlane),Flurry("Paper bits flying",new Vector3(0,top+.05f,z),Art.Paper,floorPlane)};
            // ...and leaves a set of scraps behind (they appear once the flurry has come down): over the floor, thickest around the shredder,
            // on the desk, on the shredder's top and on the window sill. Each page adds a set, so the room fills with paper as the pages go.
            float Gauss()=>Mathf.Sqrt(-2*Mathf.Log(Mathf.Max(R(),.0001f)))*Mathf.Cos(2*Mathf.PI*R());
            bool OnFloor(float x,float zz)
            {
                if(Mathf.Abs(x)<.6f&&zz>2.85f&&zz<3.75f)return false;                                                  // inside the shredder's bin
                if(x>-5.1f&&x<-4.1f&&zz>2.6f&&zz<3.5f)return false;if(x>4f&&x<5f&&zz>2.2f&&zz<3f)return false;       // under the archive boxes
                return Mathf.Abs(Mathf.Abs(x)-4.9f)>.5f||Mathf.Abs(zz-8.4f)>.5f;                                       // and the plants
            }
            var litter=new GameObject[pages.Length];
            for(int i=0;i<litter.Length;i++)
            {
                var scraps=Art.Group("Paper scraps "+(i+1),Vector3.zero);
                var scrapWhite=new List<CombineInstance>();var scrapPaper=new List<CombineInstance>();
                void Drop(float x,float y,float zz)
                {
                    bool chip=R()<.45f;float length=chip?.034f+R()*.04f:.09f+R()*.16f,width=chip?.026f+R()*.03f:.013f+R()*.012f;
                    Piece(R()<.5f?scrapWhite:scrapPaper,new Vector3(x,y,zz),new Vector3(width,.003f,length),Quaternion.Euler(R()*10-5,R()*360,R()*10-5));
                }
                for(int k=0;k<210;k++)
                {
                    // Three in five fall around the shredder and the desk, the rest anywhere on the floor.
                    float x=k%5<3?Gauss()*1.9f:R()*11f-5.5f,zz=k%5<3?1.6f+Gauss()*2.2f:R()*13.8f-4.4f;
                    x=Mathf.Clamp(x,-5.5f,5.5f);zz=Mathf.Clamp(zz,-4.4f,9.4f);
                    if(OnFloor(x,zz))Drop(x,.004f+R()*.006f,zz);
                }
                for(int k=0;k<20;k++)Drop(R()*4.2f-2.1f,.783f+R()*.003f,k%2==0?.03f+R()*.27f:.6f+R()*.27f);                  // on the desk, either side of the page rack
                for(int k=0;k<9;k++)Drop(R()*.9f-.45f,1.434f+R()*.004f,k%2==0?3f+R()*.22f:3.38f+R()*.24f);                    // on the shredder's top, clear of the slot
                for(int k=0;k<7;k++)Drop(R()*5.8f-2.9f,.809f+R()*.003f,9.72f+R()*.12f);                                      // on the window sill
                Scraps("Paper scraps white",scraps,scrapWhite,Art.White);Scraps("Paper scraps",scraps,scrapPaper,Art.Paper);
                scraps.gameObject.SetActive(false);litter[i]=scraps.gameObject;
            }
            // The slot's trigger runs the shredder. Pointing at the slot while holding a page lights its outline.
            var slot=Art.Box("Shredder slot",new Vector3(0,top+.23f,z),new Vector3(.9f,.4f,.45f),Art.Teal,false);
            Object.DestroyImmediate(slot.GetComponent<Renderer>());slot.AddComponent<BoxCollider>().isTrigger=true;
            var shredder=slot.AddComponent<Shredder>();d.shredder=shredder;shredder.director=d;shredder.pages=pages;shredder.farewells=farewells;shredder.piles=piles;shredder.strips=strips;shredder.bits=flurry;shredder.litter=litter;
            Confetti(shredder,pages,floorPlane);
            // A page stands in the slot, then sinks out of sight into the head.
            shredder.feed=Art.Group("Feed point",new Vector3(0,top+.305f,z));
            Target(slot,6,new Vector3(0,.09f,0),.55f,"Point at the shredder and let go.",Outline("Shredder highlight",new Vector3(0,top+.045f,z),new Vector2(1.15f,.88f),false));
            // After the last page: a blank page and four true lines to choose from.
            var blank=Art.Group("Blank page",Vector3.zero);
            Art.Box("Page stand",new Vector3(0,.88f,.62f),new Vector3(.5f,.16f,.12f),Art.Navy,false,blank);
            Art.Box("Blank page",new Vector3(0,1.27f,.6f),new Vector3(.5f,.64f,.02f),Art.White,false,blank,.005f);
            var blankText=Art.Text("Blank page text",Shredder.Blank,new Vector3(0,1.27f,.585f),.07f,dark,.44f,blank);
            blankText.rectTransform.sizeDelta=new Vector2(.44f,.58f);blankText.enableAutoSizing=true;blankText.fontSizeMin=.2f;blankText.fontSizeMax=.5f;
            shredder.blankText=blankText;blank.gameObject.SetActive(false);shredder.blankPage=blank.gameObject;
            var choices=Art.Group("Still true choices",Vector3.zero);
            shredder.lines=new[]{"I learn fast.","I care about people.","I keep going.","I make things better."};
            for(int i=0;i<shredder.lines.Length;i++)Art.Button("Choose line "+(i+1),shredder.lines[i],new Vector3(-1.5f+i,.9f,.12f),new Vector3(.92f,.2f,.06f),Art.Teal,ActionKind.ChooseLine,d,i,choices);
            choices.gameObject.SetActive(false);shredder.choices=choices.gameObject;
            // A desk lamp, archive boxes and plants.
            Art.Shape("Lamp base",PrimitiveType.Cylinder,new Vector3(1.9f,.79f,.78f),new Vector3(.16f,.015f,.16f),Art.Metal,false);
            Art.Line("Lamp arm",new Vector3(1.9f,.8f,.78f),new Vector3(1.75f,1.24f,.68f),.025f,Art.Metal);
            Art.Shape("Lamp shade",PrimitiveType.Cylinder,new Vector3(1.72f,1.24f,.66f),new Vector3(.18f,.07f,.18f),Art.Gold,false);
            for(int i=0;i<3;i++)Art.Box("Archive box",new Vector3(-4.6f,.2f+i*.4f,3+i*.05f),new Vector3(.8f,.38f,.55f),i==1?Art.Paper:Art.Cream);
            for(int i=0;i<2;i++)Art.Box("Archive box",new Vector3(4.5f,.2f+i*.4f,2.6f),new Vector3(.8f,.38f,.55f),Art.Paper);
            Art.Plant(new Vector3(-4.9f,0,8.4f));Art.Plant(new Vector3(4.9f,0,8.4f));
            Sound("Ambience - Print room",.55f);
        }
        // ---- Loose paper. Every scrap is one box, stretched and turned, and each heap or set of scraps is merged into one mesh.
        static Mesh unitBox;
        static Mesh UnitBox()
        {
            if(unitBox)return unitBox;
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            void Face(Vector3 normal,Vector3 up)
            {
                var right=Vector3.Cross(up,normal);int b=v.Count;
                v.Add(normal*.5f-right*.5f-up*.5f);v.Add(normal*.5f-right*.5f+up*.5f);v.Add(normal*.5f+right*.5f+up*.5f);v.Add(normal*.5f+right*.5f-up*.5f);
                for(int k=0;k<4;k++)n.Add(normal);
                if(Vector3.Dot(Vector3.Cross(v[b+1]-v[b],v[b+2]-v[b]),normal)>0)t.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});else t.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});
            }
            Face(Vector3.right,Vector3.up);Face(Vector3.left,Vector3.up);Face(Vector3.forward,Vector3.up);Face(Vector3.back,Vector3.up);Face(Vector3.up,Vector3.forward);Face(Vector3.down,Vector3.forward);
            unitBox=new Mesh{name="Paper scrap box"};unitBox.SetVertices(v);unitBox.SetNormals(n);unitBox.SetTriangles(t,0);return unitBox;
        }
        static void Piece(List<CombineInstance> into,Vector3 at,Vector3 size,Quaternion rotation){into.Add(new CombineInstance{mesh=UnitBox(),transform=Matrix4x4.TRS(at,rotation,size)});}
        static void Scraps(string name,Transform parent,List<CombineInstance> pieces,Material material)
        {
            if(pieces.Count==0)return;
            var mesh=new Mesh{name=name};mesh.CombineMeshes(pieces.ToArray(),true,true);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // A flurry of loose paper bits: a fountain out of the shredder's head that tumbles and drifts down, spreading across the room, and rests on the floor plane
        // until its time is up. About 160 bits a page from each of two systems. The plane keeps the cost low (no world collision).
        static ParticleSystem Flurry(string name,Vector3 at,Material material,Transform floor)
        {
            var group=Art.Group(name,at);group.localRotation=Quaternion.Euler(-90,0,0);
            var ps=group.gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=1.3f;main.startLifetime=new ParticleSystem.MinMaxCurve(2.8f,4.4f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.4f,4.8f);
            main.gravityModifier=.32f;main.maxParticles=300;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(.02f,.07f);main.startSizeY=new ParticleSystem.MinMaxCurve(.012f,.04f);main.startSizeZ=.003f;
            var anyTurn=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotation3D=true;main.startRotationX=anyTurn;main.startRotationY=anyTurn;main.startRotationZ=anyTurn;
            var emission=ps.emission;emission.rateOverTime=80;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)60)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=62;shape.radius=.24f;
            var tumble=ps.rotationOverLifetime;tumble.enabled=true;tumble.separateAxes=true;var turning=new ParticleSystem.MinMaxCurve(-4,4);tumble.x=turning;tumble.y=turning;tumble.z=turning;
            var noise=ps.noise;noise.enabled=true;noise.strength=.8f;noise.frequency=.5f;noise.quality=ParticleSystemNoiseQuality.Low;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;collision.AddPlane(floor);collision.dampen=1;collision.bounce=0;collision.lifetimeLoss=0;collision.radiusScale=.05f;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=Geometry.Get(PrimitiveType.Cube);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
        // The confetti easter egg: throw a résumé page onto the floor and it bursts into a shower of confetti, then is back on its stand. See ConfettiEgg.
        static void Confetti(Shredder shredder,Grabbable[] pages,Transform floor)
        {
            var group=Art.Group("Confetti",Vector3.zero);var egg=group.gameObject.AddComponent<ConfettiEgg>();
            egg.director=d;egg.pages=pages;egg.sound=SoundBank.Get("SFX - Confetti");
            egg.pop=ConfettiSystem("Confetti burst",group,floor,true);egg.shower=ConfettiSystem("Confetti shower",group,floor,false);
            foreach(var page in pages){page.tossable=true;page.gameObject.AddComponent<ConfettiPage>().egg=egg;}
            shredder.egg=egg;
        }
        // Seven bright colours; each bit takes one at random. The burst flies up from the floor and slows in the air; the shower falls from two metres up.
        // Both drift down slowly (drag, a little noise), tumble, rest on the floor plane, and shrink away at the end of their time. Each is one draw.
        static ParticleSystem ConfettiSystem(string name,Transform parent,Transform floor,bool burst)
        {
            var group=Art.Group(name,Vector3.zero,parent);group.localRotation=Quaternion.Euler(burst?-90:90,0,0);
            var ps=group.gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=burst?.2f:1.4f;
            main.startLifetime=burst?new ParticleSystem.MinMaxCurve(5f,7f):new ParticleSystem.MinMaxCurve(5.5f,8f);
            main.startSpeed=burst?new ParticleSystem.MinMaxCurve(2.5f,7f):new ParticleSystem.MinMaxCurve(0f,.5f);
            main.gravityModifier=burst?.35f:.25f;main.maxParticles=burst?320:260;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(.05f,.11f);main.startSizeY=new ParticleSystem.MinMaxCurve(.03f,.06f);main.startSizeZ=.004f;
            var anyTurn=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotation3D=true;main.startRotationX=anyTurn;main.startRotationY=anyTurn;main.startRotationZ=anyTurn;
            string[] hex={"#FF4D6D","#FFB703","#2FD38A","#23B5F0","#8B5CF6","#FF7A1A","#F9F871"};
            var colours=new Gradient{mode=GradientMode.Fixed};var keys=new GradientColorKey[hex.Length];
            for(int i=0;i<hex.Length;i++){ColorUtility.TryParseHtmlString(hex[i],out var c);keys[i]=new GradientColorKey(c,(i+1f)/hex.Length);}
            colours.SetKeys(keys,new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
            var pick=new ParticleSystem.MinMaxGradient(colours);pick.mode=ParticleSystemGradientMode.RandomColor;main.startColor=pick;
            var emission=ps.emission;
            if(burst){emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)220)});}
            else emission.rateOverTime=110;
            var shape=ps.shape;
            if(burst){shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=48;shape.radius=.1f;}
            else{shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(2f,2f,.05f);}
            var drag=ps.limitVelocityOverLifetime;drag.enabled=true;drag.limit=new ParticleSystem.MinMaxCurve(50f);drag.drag=new ParticleSystem.MinMaxCurve(burst?2.2f:1.4f);drag.multiplyDragByParticleSize=false;drag.multiplyDragByParticleVelocity=false;
            var tumble=ps.rotationOverLifetime;tumble.enabled=true;tumble.separateAxes=true;var turning=new ParticleSystem.MinMaxCurve(-5,5);tumble.x=turning;tumble.y=turning;tumble.z=turning;
            var noise=ps.noise;noise.enabled=true;noise.strength=.5f;noise.frequency=.5f;noise.quality=ParticleSystemNoiseQuality.Low;
            var fade=ps.sizeOverLifetime;fade.enabled=true;fade.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.8f,1),new Keyframe(1,0)));
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;collision.AddPlane(floor);collision.dampen=1;collision.bounce=0;collision.lifetimeLoss=0;collision.radiusScale=.05f;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=Geometry.Get(PrimitiveType.Cube);renderer.sharedMaterial=Art.ConfettiPaper;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        // The easter egg in the heavy room: hold a box in each hand and slam them together, and both burst. See BoxBlast.
        static void Blast(Grabbable[] boxes)
        {
            var group=Art.Group("Box blast",Vector3.zero);var blast=group.gameObject.AddComponent<BoxBlast>();
            blast.director=d;blast.boxes=boxes;blast.sound=SoundBank.Get("SFX - Blast");d.boxBlast=blast;
            var floor=Art.Group("Blast floor",new Vector3(0,.012f,0));
            Color Hex(string h){ColorUtility.TryParseHtmlString(h,out var c);return c;}
            var cube=Geometry.Get(PrimitiveType.Cube);var ball=Geometry.Get(PrimitiveType.Sphere);
            // Chips come in the boxes' own colours, the tape and the plinth wood; sparks and smoke are fire and dust. All of it is flat, unlit and opaque.
            var scrap=new[]{Art.Coral.color,Art.Navy.color,Art.Teal.color,Art.Gold.color,Art.Metal.color,Art.Cream.color,Art.Wood.color};
            var fire=new[]{Hex("#FFF3B0"),Hex("#FFD23F"),Hex("#FF9F1C"),Hex("#FF6B1A")};
            var dust=new[]{Hex("#3A3F47"),Hex("#565C66"),Hex("#2B2F36"),Hex("#6B717A")};
            var flat=new AnimationCurve(new Keyframe(0,1),new Keyframe(.75f,1),new Keyframe(1,0));
            // A small flash where they met, over in a fifth of a second. Brief and warm, so it never fills the view.
            blast.flash=BlastSystem("Blast flash",group,ball,new[]{Hex("#FFE9A8")},1,new Vector2(.2f,.22f),Vector2.zero,.01f,0,0,new Vector2(.55f,.6f),new AnimationCurve(new Keyframe(0,.2f),new Keyframe(.25f,1),new Keyframe(1,0)));
            blast.sparks=BlastSystem("Blast sparks",group,cube,fire,70,new Vector2(.45f,.9f),new Vector2(5,11),.05f,.5f,.4f,new Vector2(.03f,.06f),flat,stretch:true);
            blast.smoke=BlastSystem("Blast smoke",group,ball,dust,9,new Vector2(1,1.5f),new Vector2(.6f,1.8f),.1f,-.05f,1.6f,new Vector2(.18f,.34f),new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.3f,1),new Keyframe(1,0)));
            blast.chips=BlastSystem("Blast chips",group,cube,scrap,110,new Vector2(1.6f,2.6f),new Vector2(2.5f,7.5f),.12f,1.1f,.8f,new Vector2(.05f,.11f),flat,floor,chip:true);
        }
        // One one-shot system for the blast: count bits thrown out of a small ball, each in a colour taken at random from the list, slowed by drag, shrinking away at the end.
        // Chips tumble and bounce on the floor plane (no world collision) and keep their full life however often they bounce, so they lie there a moment before they shrink away; sparks are stretched along their flight. The shared flat unlit material tints each bit, so each is one draw.
        static ParticleSystem BlastSystem(string name,Transform parent,Mesh mesh,Color[] colours,int count,Vector2 life,Vector2 speed,float radius,float gravity,float drag,Vector2 size,AnimationCurve shrink,Transform floor=null,bool chip=false,bool stretch=false)
        {
            var group=Art.Group(name,Vector3.zero,parent);
            var ps=group.gameObject.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.1f;
            main.startLifetime=new ParticleSystem.MinMaxCurve(life.x,life.y);main.startSpeed=new ParticleSystem.MinMaxCurve(speed.x,speed.y);
            main.gravityModifier=gravity;main.maxParticles=count+20;main.simulationSpace=ParticleSystemSimulationSpace.World;
            if(chip)
            {
                main.startSize3D=true;main.startSizeX=new ParticleSystem.MinMaxCurve(size.x,size.y);main.startSizeY=new ParticleSystem.MinMaxCurve(size.x*.6f,size.y*.6f);main.startSizeZ=new ParticleSystem.MinMaxCurve(size.x*.35f,size.y*.35f);
                var anyTurn=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotation3D=true;main.startRotationX=anyTurn;main.startRotationY=anyTurn;main.startRotationZ=anyTurn;
            }
            else main.startSize=new ParticleSystem.MinMaxCurve(size.x,size.y);
            var gradient=new Gradient{mode=GradientMode.Fixed};var keys=new GradientColorKey[colours.Length];
            for(int i=0;i<colours.Length;i++)keys[i]=new GradientColorKey(colours[i],(i+1f)/colours.Length);
            gradient.SetKeys(keys,new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
            var pick=new ParticleSystem.MinMaxGradient(gradient);pick.mode=ParticleSystemGradientMode.RandomColor;main.startColor=pick;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=radius;
            if(drag>0){var slow=ps.limitVelocityOverLifetime;slow.enabled=true;slow.limit=new ParticleSystem.MinMaxCurve(50f);slow.drag=new ParticleSystem.MinMaxCurve(drag);slow.multiplyDragByParticleSize=false;slow.multiplyDragByParticleVelocity=false;}
            var fade=ps.sizeOverLifetime;fade.enabled=true;fade.size=new ParticleSystem.MinMaxCurve(1,shrink);
            if(chip)
            {
                var tumble=ps.rotationOverLifetime;tumble.enabled=true;tumble.separateAxes=true;var turning=new ParticleSystem.MinMaxCurve(-8,8);tumble.x=turning;tumble.y=turning;tumble.z=turning;
                var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.Planes;collision.AddPlane(floor);collision.dampen=.5f;collision.bounce=.35f;collision.lifetimeLoss=0;collision.radiusScale=.05f;
            }
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Art.ConfettiPaper;
            if(stretch){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.035f;renderer.lengthScale=1;renderer.cameraVelocityScale=0;}
            else{renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;}
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        // The rage room: the old work computer on a desk, a bat on its rack, and a quiet moment once it is all broken.
        // Every part that breaks is within a short bat swing of the arrival point, so the room plays from an airplane seat.
        static void Rage()
        {
            var room=d.rooms[RageRoom.Room].root.AddComponent<RageRoom>();room.director=d;d.rage=room;d.rooms[RageRoom.Room].total=4;
            var coral=new Color(.78f,.47f,.39f);
            Shell(10,12,Art.Ink);
            Header(RageRoom.Room,"06 / THE RAGE ROOM","Let it out.","Break the old work computer. Short swings count. Nothing here can be hurt.",new Vector3(0,3.75f,6.75f));
            // Plywood over the back wall, sprayed with what was said on the way out.
            // It stops below the room title, so the title and its progress line stay clear.
            Art.Tiled("Plywood",new Vector3(0,1.4f,6.86f),new Vector3(7.2f,2f,.05f),Art.Wood,new Vector3(1.2f,1,0),false);
            var ink=new Color(.07f,.12f,.19f);
            Art.Text("Spray paint","<b>LAST DAY</b>",new Vector3(-2.5f,1.9f,6.82f),.38f,ink,2.1f).transform.localRotation=Quaternion.Euler(0,0,6);
            Art.Text("Spray paint","<b>\"IT'S NOT PERSONAL\"</b>",new Vector3(2.2f,.95f,6.82f),.3f,ink,2.6f);
            Art.Box("Spray strike",new Vector3(2.2f,.96f,6.81f),new Vector3(2.5f,.035f,.01f),Art.Coral,false).transform.localRotation=Quaternion.Euler(0,0,-3);
            // A taped safety line marks where things break.
            for(float x=-1.2f;x<=1.21f;x+=.3f)foreach(float z in new[]{-.82f,.82f})Art.Box("Safety tape",new Vector3(x,.012f,z),new Vector3(.18f,.012f,.07f),Art.Gold,false,null,.003f);
            for(float z=-.6f;z<=.61f;z+=.3f)foreach(float x in new[]{-1.35f,1.35f})Art.Box("Safety tape",new Vector3(x,.012f,z),new Vector3(.07f,.012f,.18f),Art.Gold,false,null,.003f);
            // The desk. Its top is at .745 m, just in front of the seat.
            Art.Box("Desk top",new Vector3(0,.72f,-.25f),new Vector3(1.6f,.05f,.7f),Art.Wood);
            foreach(float x in new[]{-.74f,.74f})foreach(float z in new[]{-.55f,.05f})Art.Box("Desk leg",new Vector3(x,.35f,z),new Vector3(.05f,.7f,.05f),Art.Metal);
            Art.Box("Desk modesty panel",new Vector3(0,.42f,-.57f),new Vector3(1.5f,.42f,.02f),Art.Navy,false);
            Art.Text("Desk note","SHORT SWINGS COUNT\n<size=70%>Nothing here can be hurt.</size>",new Vector3(0,.45f,-.59f),.075f,ivory,1.4f);
            Art.Box("Monitor foot",new Vector3(0,.755f,-.18f),new Vector3(.24f,.02f,.17f),Art.Metal);
            Art.Box("Monitor neck",new Vector3(0,.86f,-.16f),new Vector3(.05f,.22f,.03f),Art.Metal,false);

            // The monitor: four hits. It cracks, argues, goes dark, then falls. Every hit throws glass, and more besides:
            // sticky notes, the bezel's chin and buttons, the back cover, screws, the power cable.
            const string inbox="<b>INBOX</b>   <color=#5B6F7A>1,284 unread</color>\n<size=78%>Re: Re: Re: quick sync?\nMandatory fun, 4:30 pm\n<color=#C87863>Your access has been revoked</color>\nPlease return your laptop by Friday\nPer my last email</size>";
            var monitor=Part("Monitor",new Vector3(0,1.13f,-.2f),"the monitor",BreakSound.Glass,"That screen saw every late night. It is dark now.",new Vector3(.66f,.44f,.1f),Vector3.zero);
            var mv=monitor.visual;
            Art.Box("Monitor body",Vector3.zero,new Vector3(.62f,.4f,.05f),Art.Ink,false,mv,.01f);
            var screen=Art.Box("Screen",new Vector3(0,.005f,-.027f),new Vector3(.57f,.345f,.006f),Art.White,false,mv,.002f);
            var screenText=Art.Text("Screen text",inbox,new Vector3(0,.005f,-.032f),.03f,dark,.52f,mv);
            screenText.rectTransform.sizeDelta=new Vector2(.52f,.31f);screenText.enableAutoSizing=true;screenText.fontSizeMin=.08f;screenText.fontSizeMax=.3f;screenText.alignment=TextAlignmentOptions.Left;
            monitor.screenText=screenText;monitor.screenStart=inbox;
            var crackA=Crack(mv,new Vector3(.1f,.06f,-.035f),5,.15f,Art.Navy,1);
            var crackB=Crack(mv,new Vector3(-.13f,-.05f,-.035f),6,.19f,Art.Navy,2);
            var crackDark=Crack(mv,new Vector3(.02f,.01f,-.03f),9,.3f,Art.Mint,3);
            Rigidbody[] Shards(int count,Vector3 around,Vector3 spread,int seed)=>Bits("Glass shard",count,around,spread,new Vector3(.032f,.024f,.004f),Art.Mint,mv,.012f,seed);
            Rigidbody[] Chips(int count,Vector3 around,Vector3 spread,int seed)=>Bits("Bezel chip",count,around,spread,new Vector3(.03f,.02f,.012f),Art.Ink,mv,.02f,seed);
            // Sticky notes hang off the bezel: the first things to go. Paper drifts rather than flies.
            Rigidbody Note(Vector3 at,float roll){var n=Piece("Sticky note",at,new Vector3(.06f,.06f,.003f),Art.Gold,mv,.004f,true);n.transform.localRotation=Quaternion.Euler(0,0,roll);n.linearDamping=3;n.angularDamping=1.5f;return n;}
            var chin=Piece("Monitor chin",new Vector3(0,-.188f,-.028f),new Vector3(.4f,.022f,.012f),Art.Ink,mv,.03f,true,PrimitiveType.Cube,.003f);
            var buttons=new Rigidbody[3];for(int k=0;k<3;k++)buttons[k]=Piece("Monitor button",new Vector3(.2f+k*.022f,-.189f,-.036f),new Vector3(.012f,.006f,.006f),Art.Gold,mv,.003f,true,PrimitiveType.Cube,0);
            var backCover=Piece("Monitor back cover",new Vector3(0,0,.045f),new Vector3(.38f,.26f,.05f),Art.Ink,mv,.25f,true,PrimitiveType.Cube,.01f);
            var fallen=Body("Fallen monitor",Vector3.zero,new Vector3(.62f,.4f,.06f),mv,3);
            Art.Box("Monitor body",Vector3.zero,new Vector3(.62f,.4f,.05f),Art.Ink,false,fallen.transform,.01f);
            Crack(fallen.transform,new Vector3(.02f,.01f,-.03f),9,.3f,Art.Mint,3).SetActive(true);
            monitor.stages=new[]{
                new BreakStage{show=new[]{crackA},debris=Shards(7,new Vector3(.1f,.06f,-.03f),new Vector3(.07f,.05f,0),21).Concat(new[]{Note(new Vector3(.31f,.15f,-.03f),8),Note(new Vector3(.31f,.07f,-.03f),-5)}).Concat(Chips(1,new Vector3(.29f,.19f,-.01f),Vector3.zero,22)).ToArray(),
                    screen="<b>ARE YOU SURE?</b>\nUnsaved feelings will be lost.\n\n<size=85%>[ OK ]        [ OK ]</size>"},
                new BreakStage{show=new[]{crackB},tilt=new Vector3(6,0,-7),debris=new[]{Piece("Bezel chip",new Vector3(.3f,-.19f,-.01f),new Vector3(.05f,.04f,.05f),Art.Ink,mv,.05f),chin,Note(new Vector3(-.31f,-.15f,-.03f),4)}.Concat(buttons).Concat(Shards(7,new Vector3(-.13f,-.05f,-.03f),new Vector3(.08f,.06f,0),23)).ToArray(),
                    screen="<b>SYSTEM ERROR</b>\nThe role you held is no longer available.\n\n<size=80%>Please contact HR.\nHR has been contacted.</size>"},
                new BreakStage{show=new[]{crackDark},hide=new[]{screen,screenText.gameObject,crackA,crackB},tilt=new Vector3(14,0,-12),shift=new Vector3(0,-.02f,.02f),
                    debris=Shards(9,new Vector3(0,0,-.03f),new Vector3(.22f,.13f,0),24).Concat(new[]{backCover}).Concat(Bits("Screw",3,new Vector3(0,0,.075f),new Vector3(.15f,.1f,0),new Vector3(.008f,.006f,.008f),Art.Metal,mv,.003f,25,PrimitiveType.Cylinder)).ToArray()},
                new BreakStage{hide=new[]{mv.gameObject},debris=new[]{fallen,Piece("Power cable",new Vector3(0,-.05f,.08f),new Vector3(.008f,.008f,.3f),Art.Ink,mv,.05f,false,PrimitiveType.Cube,0),Piece("Cable plug",new Vector3(0,-.05f,.24f),new Vector3(.02f,.015f,.03f),Art.Ink,mv,.02f)}
                    .Concat(Shards(12,new Vector3(0,0,-.03f),new Vector3(.25f,.15f,0),26)).Concat(Chips(4,new Vector3(0,-.19f,-.01f),new Vector3(.28f,.01f,0),27)).ToArray()},
            };

            // The keyboard: three hits. Keys fly on every hit, the space bar with them, then it snaps in two.
            var keyboard=Part("Keyboard",new Vector3(-.02f,.765f,-.42f),"the keyboard",BreakSound.Plastic,"All the words you held back. Some of them are out now.",new Vector3(.5f,.07f,.21f),new Vector3(0,.01f,0));
            var kv=keyboard.visual;var whole=Art.Group("Keyboard whole",Vector3.zero,kv);
            Art.Box("Keyboard base",new Vector3(0,-.004f,0),new Vector3(.46f,.018f,.18f),Art.Ink,false,whole,.004f);
            var looseKeys=new List<Rigidbody>();
            for(int row=0;row<4;row++)for(int col=0;col<12;col++)
            {
                var at=new Vector3(-.198f+col*.036f,.011f,-.036f+row*.036f);var size=new Vector3(.03f,.012f,.03f);
                // Three keys in four are loose. The rest stay with the halves.
                if((row*12+col)%4!=3)looseKeys.Add(Piece("Key",at,size,Art.Cream,kv,.008f,true));
                else Art.Box("Key",at,size,Art.Cream,false,whole,.003f);
            }
            // Shuffled, so every hit takes keys from all over the board.
            var keyOrder=new System.Random(7);looseKeys=looseKeys.OrderBy(_=>keyOrder.Next()).ToList();
            var spaceBar=Piece("Space bar",new Vector3(-.02f,.011f,-.072f),new Vector3(.16f,.012f,.03f),Art.Cream,kv,.02f,true);
            Rigidbody Half(string name,float x)
            {
                var half=Body(name,new Vector3(x,-.004f,0),new Vector3(.23f,.03f,.18f),kv,.35f);
                Art.Box("Keyboard half",Vector3.zero,new Vector3(.225f,.018f,.18f),Art.Ink,false,half.transform,.004f);
                for(int k=0;k<6;k++)Art.Box("Key",new Vector3(-.072f+(k%3)*.072f,.015f,-.036f+(k/3)*.072f),new Vector3(.03f,.012f,.03f),Art.Cream,false,half.transform,.003f);
                return half;
            }
            keyboard.stages=new[]{
                new BreakStage{tilt=new Vector3(0,-6,0),shift=new Vector3(0,0,.01f),debris=looseKeys.Take(14).ToArray()},
                new BreakStage{tilt=new Vector3(0,-10,4),shift=new Vector3(.01f,0,.02f),debris=looseKeys.Skip(14).Take(14).Concat(new[]{spaceBar}).ToArray()},
                new BreakStage{hide=new[]{whole.gameObject},debris=new[]{Half("Keyboard left half",-.115f),Half("Keyboard right half",.115f),
                    Piece("Keyboard cable",new Vector3(0,0,.1f),new Vector3(.006f,.006f,.12f),Art.Ink,kv,.02f,false,PrimitiveType.Cube,0),
                    Piece("Keyboard foot",new Vector3(-.18f,-.014f,.07f),new Vector3(.03f,.008f,.012f),Art.Ink,kv,.004f,false,PrimitiveType.Cube,0),
                    Piece("Keyboard foot",new Vector3(.18f,-.014f,.07f),new Vector3(.03f,.008f,.012f),Art.Ink,kv,.004f,false,PrimitiveType.Cube,0)}.Concat(looseKeys.Skip(28)).ToArray()},
            };

            // The tower: four hits. A dent, the side panel, the fan and the light, then it topples. Screws on every hit,
            // and the insides come out: memory, the battery, a graphics card, a drive, the power supply.
            var tower=Part("Tower",new Vector3(.56f,.97f,-.3f),"the tower",BreakSound.Metal,"You carried their systems for years. You can set that down.",new Vector3(.24f,.46f,.44f),Vector3.zero);
            var tv=tower.visual;
            Art.Box("Tower case",Vector3.zero,new Vector3(.2f,.42f,.4f),Art.Navy,false,tv,.008f);
            Art.Box("Front panel",new Vector3(0,0,-.2f),new Vector3(.18f,.4f,.008f),Art.Metal,false,tv,.002f);
            var powerButton=Piece("Power button",new Vector3(0,.15f,-.205f),Vector3.one*.028f,Art.Gold,tv,.01f,true,PrimitiveType.Sphere);
            var light=Art.Shape("Power light",PrimitiveType.Sphere,new Vector3(.05f,.15f,-.206f),Vector3.one*.013f,Art.Mint,false,tv);
            var bayCover=Piece("Drive bay cover",new Vector3(0,.07f,-.206f),new Vector3(.14f,.025f,.004f),Art.Ink,tv,.02f,true,PrimitiveType.Cube,0);
            for(int k=0;k<3;k++)Art.Box("Vent",new Vector3(0,-.1f-k*.03f,-.205f),new Vector3(.12f,.008f,.004f),Art.Ink,false,tv,.001f);
            var dent=Art.Box("Dent",new Vector3(0,-.02f,-.206f),new Vector3(.15f,.012f,.004f),Art.Ink,false,tv,.001f);dent.transform.localRotation=Quaternion.Euler(0,0,32);dent.SetActive(false);
            var side=Piece("Side panel",new Vector3(.104f,0,0),new Vector3(.006f,.4f,.38f),Art.Metal,tv,.6f,true);
            var fan=Piece("Fan",new Vector3(.085f,.1f,.06f),new Vector3(.1f,.012f,.1f),Art.Ink,tv,.15f,true,PrimitiveType.Cylinder);fan.transform.localRotation=Quaternion.Euler(0,0,90);
            Rigidbody[] Screws(int count,Vector3 around,Vector3 spread,int seed)=>Bits("Screw",count,around,spread,new Vector3(.008f,.006f,.008f),Art.Metal,tv,.003f,seed,PrimitiveType.Cylinder);
            var toppled=Body("Fallen tower",Vector3.zero,new Vector3(.2f,.42f,.4f),tv,4);
            Art.Box("Tower case",Vector3.zero,new Vector3(.2f,.42f,.4f),Art.Navy,false,toppled.transform,.008f);
            Art.Box("Front panel",new Vector3(0,0,-.2f),new Vector3(.18f,.4f,.008f),Art.Metal,false,toppled.transform,.002f);
            Art.Box("Motherboard",new Vector3(.098f,0,0),new Vector3(.006f,.36f,.34f),Art.Teal,false,toppled.transform,.002f);
            tower.stages=new[]{
                new BreakStage{show=new[]{dent},tilt=new Vector3(0,-5,4),debris=new[]{powerButton,bayCover}.Concat(Screws(4,new Vector3(0,0,-.205f),new Vector3(.08f,.19f,0),31))
                    .Concat(Bits("Front panel chip",2,new Vector3(0,-.1f,-.205f),new Vector3(.06f,.05f,0),new Vector3(.03f,.02f,.008f),Art.Metal,tv,.006f,32)).ToArray()},
                new BreakStage{tilt=new Vector3(0,-9,7),debris=new[]{side,Piece("Memory",new Vector3(.095f,-.04f,-.05f),new Vector3(.01f,.03f,.12f),Art.Gold,tv,.02f),Piece("Memory",new Vector3(.095f,-.08f,-.05f),new Vector3(.01f,.03f,.12f),Art.Gold,tv,.02f),
                    Piece("Clock battery",new Vector3(.09f,-.14f,.08f),new Vector3(.02f,.002f,.02f),Art.Metal,tv,.003f,false,PrimitiveType.Cylinder,0)}
                    .Concat(Screws(4,new Vector3(.104f,0,0),new Vector3(0,.19f,.18f),33)).Concat(Bits("Case clip",2,new Vector3(.1f,0,0),new Vector3(0,.18f,.17f),new Vector3(.02f,.012f,.006f),Art.Ink,tv,.004f,34)).ToArray()},
                new BreakStage{hide=new[]{light},tilt=new Vector3(0,-12,10),shift=new Vector3(.01f,0,.02f),debris=new[]{fan,
                    Piece("Graphics card",new Vector3(.04f,-.05f,0),new Vector3(.02f,.1f,.24f),Art.Teal,tv,.2f),Piece("Hard drive",new Vector3(0,-.14f,.05f),new Vector3(.1f,.025f,.14f),Art.Metal,tv,.25f),
                    Piece("Heat sink",new Vector3(.05f,.05f,-.02f),new Vector3(.05f,.06f,.06f),Art.Metal,tv,.1f)}
                    .Concat(Bits("Cable",2,new Vector3(.03f,0,.05f),new Vector3(.03f,.1f,.1f),new Vector3(.006f,.006f,.08f),Art.Coral,tv,.005f,35)).Concat(Screws(4,new Vector3(.05f,0,0),new Vector3(.04f,.18f,.17f),36)).ToArray()},
                new BreakStage{hide=new[]{tv.gameObject},debris=new[]{toppled,Piece("Power supply",new Vector3(0,.15f,.1f),new Vector3(.14f,.08f,.15f),Art.Ink,tv,.4f),
                    Piece("Front grille",new Vector3(0,-.12f,-.2f),new Vector3(.17f,.12f,.006f),Art.Metal,tv,.05f,false,PrimitiveType.Cube,.002f),Piece("Motherboard piece",new Vector3(.09f,-.1f,.1f),new Vector3(.006f,.12f,.12f),Art.Teal,tv,.03f)}
                    .Concat(Bits("Case chip",3,new Vector3(0,0,-.1f),new Vector3(.08f,.18f,.1f),new Vector3(.04f,.03f,.008f),Art.Navy,tv,.01f,37)).Concat(Screws(5,Vector3.zero,new Vector3(.09f,.19f,.18f),38)).ToArray()},
            };

            // The mouse: two hits. Flattened, its battery out, then in pieces.
            var mouse=Part("Mouse",new Vector3(.33f,.762f,-.46f),"the mouse",BreakSound.Plastic,"Click. Done.",new Vector3(.11f,.07f,.15f),new Vector3(0,.015f,0));
            var ov=mouse.visual;
            Art.Shape("Mouse",PrimitiveType.Sphere,new Vector3(0,.014f,0),new Vector3(.06f,.032f,.1f),Art.White,false,ov);
            Art.Box("Mouse buttons",new Vector3(0,.029f,.025f),new Vector3(.003f,.004f,.04f),Art.Ink,false,ov,.001f);
            Art.Line("Mouse cable",new Vector3(0,.008f,.05f),new Vector3(-.06f,.002f,.2f),.006f,Art.Ink,ov);
            var mouseCrack=Art.Box("Mouse crack",new Vector3(.005f,.026f,-.01f),new Vector3(.003f,.005f,.06f),Art.Ink,false,ov,.001f);mouseCrack.transform.localRotation=Quaternion.Euler(0,25,0);mouseCrack.SetActive(false);
            Rigidbody[] MouseChips(int seed)=>Bits("Mouse chip",3,new Vector3(0,.014f,0),new Vector3(.02f,.01f,.03f),new Vector3(.015f,.01f,.006f),Art.White,ov,.003f,seed);
            mouse.stages=new[]{
                new BreakStage{show=new[]{mouseCrack},tilt=new Vector3(0,20,0),shift=new Vector3(0,-.006f,0),scale=new Vector3(1.15f,.55f,1),
                    debris=new[]{Piece("Battery cover",new Vector3(0,.004f,-.01f),new Vector3(.04f,.004f,.05f),Art.White,ov,.004f,false,PrimitiveType.Cube,0),Piece("Battery",new Vector3(0,.012f,0),new Vector3(.014f,.02f,.014f),Art.Gold,ov,.02f,false,PrimitiveType.Cylinder)}.Concat(MouseChips(41)).ToArray()},
                new BreakStage{hide=new[]{ov.gameObject},debris=new[]{Piece("Mouse half",new Vector3(-.015f,.014f,0),new Vector3(.03f,.03f,.1f),Art.White,ov,.03f,false,PrimitiveType.Sphere),Piece("Mouse half",new Vector3(.015f,.014f,0),new Vector3(.03f,.03f,.1f),Art.White,ov,.03f,false,PrimitiveType.Sphere),Piece("Mouse wheel",new Vector3(0,.03f,.03f),new Vector3(.012f,.004f,.012f),Art.Ink,ov,.01f,false,PrimitiveType.Cylinder),
                    Piece("Mouse button",new Vector3(-.015f,.03f,.025f),new Vector3(.026f,.004f,.04f),Art.White,ov,.004f),Piece("Mouse button",new Vector3(.015f,.03f,.025f),new Vector3(.026f,.004f,.04f),Art.White,ov,.004f),
                    Piece("Mouse circuit board",new Vector3(0,.008f,0),new Vector3(.03f,.003f,.05f),Art.Teal,ov,.005f,false,PrimitiveType.Cube,0),Piece("Mouse cable end",new Vector3(0,.008f,.06f),new Vector3(.006f,.006f,.06f),Art.Ink,ov,.004f,false,PrimitiveType.Cube,0)}.Concat(MouseChips(42)).ToArray()},
            };
            room.parts=new[]{monitor,keyboard,tower,mouse};

            // Desk things around the computer. They break too, and throw their own pieces, but only the computer counts.
            // The coffee mug: one hit, and it shatters.
            var mug=Part("Mug",new Vector3(-.35f,.745f,-.48f),"the mug",BreakSound.Glass,"You deserved better than the break room coffee.",new Vector3(.12f,.12f,.1f),new Vector3(0,.055f,0));
            var mugWhole=Art.Group("Mug whole",Vector3.zero,mug.visual);
            Art.Shape("Mug body",PrimitiveType.Cylinder,new Vector3(0,.05f,0),new Vector3(.08f,.05f,.08f),Art.White,false,mugWhole);
            Art.Shape("Mug stripe",PrimitiveType.Cylinder,new Vector3(0,.08f,0),new Vector3(.082f,.004f,.082f),Art.Coral,false,mugWhole);
            Art.Box("Mug handle",new Vector3(.05f,.05f,0),new Vector3(.02f,.06f,.012f),Art.White,false,mugWhole,.004f);
            Art.Text("Mug words","TEAM\nPLAYER",new Vector3(0,.05f,-.042f),.013f,dark,.07f,mugWhole);
            mug.stages=new[]{new BreakStage{hide=new[]{mugWhole.gameObject},debris=Bits("Mug shard",9,new Vector3(0,.05f,0),new Vector3(.035f,.04f,.035f),new Vector3(.03f,.025f,.008f),Art.White,mug.visual,.012f,51)
                .Concat(new[]{Piece("Mug handle",new Vector3(.05f,.05f,0),new Vector3(.02f,.06f,.012f),Art.White,mug.visual,.02f)}).ToArray()}};

            // The desk phone: the handset and some buttons fly first, then the base splits.
            var phone=Part("Desk phone",new Vector3(-.6f,.745f,-.42f),"the desk phone",BreakSound.Plastic,"No more calls after hours.",new Vector3(.24f,.11f,.21f),new Vector3(0,.05f,0));
            phone.transform.localRotation=Quaternion.Euler(0,-40,0);
            var pv=phone.visual;var phoneBase=Art.Group("Phone base",Vector3.zero,pv);
            Art.Box("Phone body",new Vector3(0,.025f,0),new Vector3(.2f,.05f,.17f),Art.Ink,false,phoneBase,.008f);
            Art.Box("Phone cradle",new Vector3(-.05f,.052f,0),new Vector3(.07f,.006f,.17f),Art.Navy,false,phoneBase,.002f);
            var phoneButtons=new List<Rigidbody>();
            for(int k=0;k<12;k++)phoneButtons.Add(Piece("Phone button",new Vector3(.02f+(k%3)*.024f,.054f,-.055f+(k/3)*.024f),new Vector3(.018f,.008f,.016f),Art.Cream,pv,.004f,true,PrimitiveType.Cube,.002f));
            var display=Piece("Phone display",new Vector3(.045f,.053f,.065f),new Vector3(.07f,.004f,.025f),Art.Mint,pv,.005f,true,PrimitiveType.Cube,0);
            var handset=Body("Handset",new Vector3(-.05f,.07f,0),new Vector3(.055f,.035f,.19f),pv,.2f);handset.gameObject.SetActive(true);
            Art.Box("Handset grip",Vector3.zero,new Vector3(.045f,.02f,.13f),Art.Ink,false,handset.transform,.006f);
            foreach(float z in new[]{-.075f,.075f})Art.Box("Handset end",new Vector3(0,-.004f,z),new Vector3(.055f,.032f,.04f),Art.Ink,false,handset.transform,.008f);
            Rigidbody PhoneHalf(string name,float x)
            {
                var half=Body(name,new Vector3(x,.025f,0),new Vector3(.1f,.05f,.17f),pv,.25f);
                Art.Box("Phone body half",Vector3.zero,new Vector3(.1f,.05f,.17f),Art.Ink,false,half.transform,.008f);return half;
            }
            phone.stages=new[]{
                new BreakStage{tilt=new Vector3(0,8,4),debris=new[]{handset,Piece("Phone cord",new Vector3(-.09f,.04f,.08f),new Vector3(.008f,.008f,.1f),Art.Ink,pv,.01f,false,PrimitiveType.Cube,0)}.Concat(phoneButtons.Take(5)).ToArray()},
                new BreakStage{hide=new[]{phoneBase.gameObject},debris=new[]{PhoneHalf("Phone half",-.05f),PhoneHalf("Phone half",.05f),display}.Concat(phoneButtons.Skip(5))
                    .Concat(Bits("Phone chip",2,new Vector3(0,.03f,0),new Vector3(.06f,.01f,.06f),new Vector3(.025f,.015f,.008f),Art.Ink,pv,.004f,52)).ToArray()},
            };

            // The stapler: one hit, and it comes apart in a spray of staples.
            var stapler=Part("Stapler",new Vector3(.3f,.745f,-.28f),"the stapler",BreakSound.Metal,"You held so many things together. You can let go now.",new Vector3(.08f,.07f,.19f),new Vector3(0,.03f,0));
            var stapleWhole=Art.Group("Stapler whole",Vector3.zero,stapler.visual);
            Art.Box("Stapler base",new Vector3(0,.006f,0),new Vector3(.04f,.012f,.17f),Art.Ink,false,stapleWhole,.004f);
            Art.Box("Stapler arm",new Vector3(0,.03f,.005f),new Vector3(.036f,.03f,.16f),Art.Coral,false,stapleWhole,.008f).transform.localRotation=Quaternion.Euler(-3,0,0);
            stapler.stages=new[]{new BreakStage{hide=new[]{stapleWhole.gameObject},debris=new[]{
                Piece("Stapler arm",new Vector3(0,.03f,.005f),new Vector3(.036f,.03f,.16f),Art.Coral,stapler.visual,.12f),Piece("Stapler base",new Vector3(0,.006f,0),new Vector3(.04f,.012f,.17f),Art.Ink,stapler.visual,.15f),
                Piece("Stapler spring",new Vector3(0,.03f,-.04f),new Vector3(.01f,.02f,.01f),Art.Metal,stapler.visual,.005f,false,PrimitiveType.Cylinder,0)}
                .Concat(Bits("Staple",10,new Vector3(0,.03f,0),new Vector3(.012f,.008f,.06f),new Vector3(.012f,.002f,.003f),Art.Metal,stapler.visual,.002f,53)).ToArray()}};

            // The pen cup: the cup and every pen go, with the paper clips that were at the bottom.
            var penCup=Part("Pen cup",new Vector3(-.47f,.745f,-.54f),"the pen cup",BreakSound.Plastic,"Every form is signed and filed. Your hands can rest.",new Vector3(.1f,.2f,.1f),new Vector3(0,.08f,0));
            var cv=penCup.visual;
            var cup=Piece("Cup",new Vector3(0,.05f,0),new Vector3(.07f,.05f,.07f),Art.Navy,cv,.05f,true,PrimitiveType.Cylinder);
            var pens=new Rigidbody[6];var penColours=new[]{Art.Coral,Art.Teal,Art.Ink,Art.Gold,Art.Coral,Art.Ink};
            for(int k=0;k<6;k++){float a=k*Mathf.PI/3;pens[k]=Piece("Pen",new Vector3(Mathf.Cos(a)*.016f,.105f,Mathf.Sin(a)*.016f),new Vector3(.008f,.07f,.008f),penColours[k],cv,.008f,true,PrimitiveType.Cylinder,0);pens[k].transform.localRotation=Quaternion.Euler(Mathf.Sin(a)*9,0,-Mathf.Cos(a)*9);}
            penCup.stages=new[]{new BreakStage{debris=new[]{cup}.Concat(pens).Concat(Bits("Paper clip",4,new Vector3(0,.02f,0),new Vector3(.02f,.01f,.02f),new Vector3(.01f,.002f,.025f),Art.Metal,cv,.002f,54)).ToArray()}};

            // The name plate, with the old title on it.
            var namePlate=Part("Name plate",new Vector3(.57f,.745f,-.565f),"the name plate",BreakSound.Metal,"A title is a job. It was never the whole of you.",new Vector3(.24f,.08f,.07f),new Vector3(0,.03f,0));
            var plate=Body("Plate",new Vector3(0,.037f,0),new Vector3(.22f,.055f,.012f),namePlate.visual,.12f);plate.gameObject.SetActive(true);plate.transform.localRotation=Quaternion.Euler(20,0,0);
            Art.Box("Plate face",Vector3.zero,new Vector3(.22f,.055f,.01f),Art.Navy,false,plate.transform,.003f);
            Art.Text("Plate title","<b>SENIOR ASSOCIATE</b>",new Vector3(0,0,-.007f),.022f,new Color(.86f,.7f,.38f),.21f,plate.transform);
            var plateStand=Piece("Plate stand",new Vector3(0,.005f,.01f),new Vector3(.2f,.01f,.04f),Art.Metal,namePlate.visual,.08f,true,PrimitiveType.Cube,.002f);
            namePlate.stages=new[]{new BreakStage{debris=new[]{plate,plateStand}.Concat(Bits("Screw",3,new Vector3(0,.01f,.01f),new Vector3(.08f,0,0),new Vector3(.008f,.006f,.008f),Art.Metal,namePlate.visual,.003f,55,PrimitiveType.Cylinder)).ToArray()}};

            room.extras=new[]{mug,phone,stapler,penCup,namePlate};
            // Every piece remembers what it is made of, for the sound it makes when the bat knocks it about later.
            // The sticky notes are paper, whatever they were stuck to.
            foreach(var note in mv.GetComponentsInChildren<Rigidbody>(true).Where(r=>r.name=="Sticky note"))note.gameObject.AddComponent<Loose>().sound=BreakSound.Paper;
            foreach(var thing in room.parts.Concat(room.extras))foreach(var stage in thing.stages)foreach(var piece in stage.debris)
                if(piece&&!piece.GetComponent<Loose>())piece.gameObject.AddComponent<Loose>().sound=thing.sound;

            // The bat stands on its rack to the left of the desk. Pointing at it and pinching puts it in your hand.
            var bat=Art.Group("Bat",new Vector3(-.62f,.11f,-.78f));bat.localRotation=Quaternion.Euler(-84,8,0);
            void Segment(string name,float z,float length,float diameter,Material material)
            {Art.Shape(name,PrimitiveType.Cylinder,new Vector3(0,0,z),new Vector3(diameter,length/2,diameter),material,false,bat).transform.localRotation=Quaternion.Euler(90,0,0);}
            Segment("Bat knob",-.075f,.02f,.052f,Art.Ink);Segment("Bat grip",.075f,.28f,.033f,Art.Ink);Segment("Bat taper",.31f,.2f,.046f,Art.Wood);
            Segment("Bat barrel",.62f,.44f,.066f,Art.Wood);Segment("Bat band",.47f,.025f,.07f,Art.Coral);
            Art.Shape("Bat end",PrimitiveType.Sphere,new Vector3(0,0,.84f),new Vector3(.066f,.066f,.03f),Art.Wood,false,bat);
            var batCollider=bat.gameObject.AddComponent<BoxCollider>();batCollider.center=new Vector3(0,0,.38f);batCollider.size=new Vector3(.09f,.09f,.96f);
            var batBody=bat.gameObject.AddComponent<Rigidbody>();batBody.isKinematic=true;batBody.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=bat.gameObject.AddComponent<Grabbable>();grab.label="The bat";grab.room=RageRoom.Room;grab.tool=true;room.bat=grab;
            var swing=bat.gameObject.AddComponent<Bat>();swing.room=room;swing.grabbable=grab;
            Art.Shape("Bat stand",PrimitiveType.Cylinder,new Vector3(-.62f,.015f,-.78f),new Vector3(.22f,.015f,.22f),Art.Navy);
            Art.Box("Rack post",new Vector3(-.72f,.33f,-.78f),new Vector3(.03f,.64f,.03f),Art.Metal);
            Art.Box("Rack hook",new Vector3(-.67f,.56f,-.76f),new Vector3(.1f,.02f,.02f),Art.Gold,false);
            var rack=Art.Group("Bat rack sign",new Vector3(-1.02f,.9f,-.5f));rack.localRotation=Quaternion.Euler(0,-40,0);
            Art.Button("Bat rack","PUT THE BAT BACK",Vector3.zero,new Vector3(.3f,.085f,.03f),Art.Navy,ActionKind.PutBatBack,d,0,rack);

            // When it is all broken: the quiet, a soft light panel at reading distance beyond the desk, and a way to wheel in a new one.
            var calm=Art.Group("The quiet",new Vector3(0,1.62f,1.35f));
            Art.Box("Calm light",Vector3.zero,new Vector3(1.6f,.56f,.02f),Art.White,false,calm,.004f);
            Art.Box("Calm edge",new Vector3(0,-.29f,0),new Vector3(1.6f,.02f,.03f),Art.Gold,false,calm,.002f);
            Art.Text("Calm words","IT IS ALLOWED TO BE ANGRY",new Vector3(0,.13f,-.02f),.085f,dark,1.5f,calm);
            Art.Text("Calm words","about losing something that mattered.",new Vector3(0,-.01f,-.02f),.06f,dark,1.5f,calm);
            Art.Text("Calm breath","Breathe in.   And out.",new Vector3(0,-.15f,-.02f),.055f,coral,1.5f,calm);
            foreach(var r in calm.GetComponentsInChildren<Renderer>(true))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            calm.gameObject.SetActive(false);room.calm=calm.gameObject;
            var fresh=Art.Group("New computer",new Vector3(0,1.13f,-.29f));
            Art.Button("Wheel in a new computer","WHEEL IN A NEW ONE",Vector3.zero,new Vector3(.62f,.16f,.05f),Art.Teal,ActionKind.NewComputer,d,0,fresh);
            fresh.gameObject.SetActive(false);room.newComputer=fresh.gameObject;

            // The room around it: a hanging lamp, a box of desk things, a knocked-over chair, the rules and a clock at five.
            Art.Line("Lamp cord",new Vector3(0,4.58f,-.2f),new Vector3(0,2.75f,-.2f),.012f,Art.Ink);
            Art.Shape("Lamp shade",PrimitiveType.Cylinder,new Vector3(0,2.72f,-.2f),new Vector3(.38f,.08f,.38f),Art.Gold,false);
            Art.Shape("Lamp bulb",PrimitiveType.Sphere,new Vector3(0,2.62f,-.2f),Vector3.one*.11f,Art.White,false);
            Art.Box("Box of your things",new Vector3(1.15f,.17f,-1.05f),new Vector3(.45f,.32f,.35f),Art.Paper);
            foreach(float s in new[]{-1f,1f})Art.Box("Box flap",new Vector3(1.15f+s*.24f,.36f,-1.05f),new Vector3(.02f,.12f,.33f),Art.Paper,false).transform.localRotation=Quaternion.Euler(0,0,s*-30);
            Art.Text("Box label","MY THINGS",new Vector3(1.15f,.2f,-1.235f),.06f,dark,.4f);
            Art.Shape("Mug",PrimitiveType.Cylinder,new Vector3(1.05f,.37f,-1.02f),new Vector3(.08f,.05f,.08f),Art.White,false);
            Art.Facet("Desk plant",new Vector3(1.25f,.42f,-1.02f),new Vector3(.12f,.2f,.12f),Art.Teal,5);
            var chair=Art.Group("Knocked-over chair",new Vector3(-1.9f,0,1.3f));chair.localRotation=Quaternion.Euler(0,35,0);
            Art.Box("Chair seat",new Vector3(0,.3f,0),new Vector3(.55f,.1f,.5f),Art.Navy,true,chair).transform.localRotation=Quaternion.Euler(0,0,78);
            Art.Shape("Chair back",PrimitiveType.Capsule,new Vector3(-.32f,.3f,.35f),new Vector3(.55f,.38f,.14f),Art.Navy,false,chair).transform.localRotation=Quaternion.Euler(90,0,0);
            Fitted(Art.Box("Rules board",new Vector3(-4.88f,2,1),new Vector3(1.7f,1.1f,.04f),Art.Navy,false),.1f,1.25f).transform.localRotation=Quaternion.Euler(0,-90,0);
            Art.Text("Rules","RAGE ROOM RULES\n<size=70%>1. Break things, never people.\n2. Short swings count.\n3. Rest whenever you like.</size>",new Vector3(-4.85f,2,1),.13f,ivory,1.55f).transform.localRotation=Quaternion.Euler(0,-90,0);
            Art.Shape("Clock",PrimitiveType.Cylinder,new Vector3(4.88f,2.6f,1.5f),new Vector3(.5f,.02f,.5f),Art.Cream,false).transform.localRotation=Quaternion.Euler(0,0,90);
            Art.Line("Clock hand",new Vector3(4.85f,2.6f,1.5f),new Vector3(4.85f,2.6f+.866f*.12f,1.5f+.5f*.12f),.014f,Art.Ink);
            Art.Line("Clock hand",new Vector3(4.85f,2.6f,1.5f),new Vector3(4.85f,2.78f,1.5f),.01f,Art.Ink);
            room.shards=Burst("Shards",Art.Mint,.026f,1);room.sparks=Burst("Sparks",d.player.rayMaterial,.013f,.5f);
            Sound("Ambience - Rage room",.5f);room.ambience=d.rooms[RageRoom.Room].root.GetComponent<AudioSource>();room.ambienceVolume=.5f;
            room.hitPlastic=SoundBank.Get("SFX - Hit plastic");room.hitGlass=SoundBank.Get("SFX - Hit glass");room.hitMetal=SoundBank.Get("SFX - Hit metal");
            room.hitPaper=SoundBank.Get("SFX - Hit paper");room.clatter=SoundBank.Get("SFX - Clatter");
            room.smash=SoundBank.Get("SFX - Smash");room.swing=SoundBank.Get("SFX - Swing");room.cart=SoundBank.Get("SFX - Cart");room.calmSound=SoundBank.Get("SFX - Calm");
        }
        // A part of the computer: a solid volume the bat can hit, and a group of pieces that breaks.
        static Breakable Part(string name,Vector3 at,string label,BreakSound sound,string line,Vector3 volume,Vector3 centre)
        {
            var root=Art.Group(name,at);var part=root.gameObject.AddComponent<Breakable>();part.label=label;part.sound=sound;part.brokenLine=line;
            var box=root.gameObject.AddComponent<BoxCollider>();box.center=centre;box.size=volume;part.hitVolumes=new Collider[]{box};
            part.visual=Art.Group(name+" - pieces",Vector3.zero,root);return part;
        }
        // A loose piece: parked inside its part until a hit throws it. Pieces that start overlapping ease apart
        // rather than burst apart.
        static Rigidbody Piece(string name,Vector3 at,Vector3 size,Material material,Transform parent,float mass,bool visible=false,PrimitiveType type=PrimitiveType.Cube,float bevel=.004f)
        {
            var go=Art.Shape(name,type,at,size,material,true,parent,bevel);
            var body=go.AddComponent<Rigidbody>();body.mass=mass;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.maxDepenetrationVelocity=1.5f;
            go.SetActive(visible);return body;
        }
        // Small loose bits around a point of a part: shards, screws, chips. Square-edged, so all of them share one mesh.
        static Rigidbody[] Bits(string name,int count,Vector3 around,Vector3 spread,Vector3 size,Material material,Transform parent,float mass,int seed,PrimitiveType type=PrimitiveType.Cube)
        {
            var random=new System.Random(seed);float R()=>(float)random.NextDouble()*2-1;var bits=new Rigidbody[count];
            for(int i=0;i<count;i++)
            {
                bits[i]=Piece(name,around+Vector3.Scale(spread,new Vector3(R(),R(),R())),size*(1+.3f*R()),material,parent,mass,false,type,0);
                bits[i].transform.localRotation=Quaternion.Euler(R()*180,R()*180,R()*180);
            }
            return bits;
        }
        // A larger loose piece built from several shapes, with one box to collide.
        static Rigidbody Body(string name,Vector3 at,Vector3 size,Transform parent,float mass)
        {
            var group=Art.Group(name,at,parent);group.gameObject.AddComponent<BoxCollider>().size=size;
            var body=group.gameObject.AddComponent<Rigidbody>();body.mass=mass;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.maxDepenetrationVelocity=1.5f;
            group.gameObject.SetActive(false);return body;
        }
        // Cracks radiating across a screen from a point of impact. Hidden until a hit shows them.
        static GameObject Crack(Transform parent,Vector3 at,int lines,float reach,Material material,int seed)
        {
            var group=Art.Group("Crack",Vector3.zero,parent);var random=new System.Random(seed);float R()=>(float)random.NextDouble();
            Vector3 Clamp(Vector3 p)=>new Vector3(Mathf.Clamp(p.x,-.28f,.28f),Mathf.Clamp(p.y,-.165f,.17f),p.z);
            for(int i=0;i<lines;i++)
            {
                float a=(i+R()*.6f)*Mathf.PI*2/lines,length=reach*(.5f+R()*.6f);
                var end=Clamp(at+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*length);Art.Line("Crack line",at,end,.0035f,material,group);
                var mid=Vector3.Lerp(at,end,.6f);float b=a+(R()-.5f)*1.3f;
                Art.Line("Crack line",mid,Clamp(mid+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*length*.4f),.0025f,material,group);
            }
            group.gameObject.SetActive(false);return group.gameObject;
        }
        // Shards and sparks thrown by a hit. Emitted from code, so the system plays continuously with no emission of its own.
        static ParticleSystem Burst(string name,Material material,float size,float gravity)
        {
            var go=Art.Group(name,Vector3.zero).gameObject;var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=true;main.loop=true;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(.6f,1.2f);main.startSpeed=0;main.gravityModifier=gravity;main.maxParticles=480;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size);
            main.startRotation3D=true;main.startRotationX=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationY=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationZ=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;collision.dampen=.5f;collision.bounce=.25f;collision.quality=ParticleSystemCollisionQuality.Medium;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=Geometry.Get(PrimitiveType.Cube);renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
        static Grabbable Page(string text,Vector3 p)
        {
            var page=Art.Box("Old résumé",p,new Vector3(.42f,.55f,.03f),Art.White,true,null,.006f);
            var rb=page.AddComponent<Rigidbody>();rb.mass=.15f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var g=page.AddComponent<Grabbable>();g.label=text.Replace("\n"," ");g.room=6;g.upright=true;
            // The face is laid out in metres: its group cancels the page's own scale.
            var face=Art.Group("Page face",new Vector3(0,0,-.52f),page.transform);face.localScale=new Vector3(1/.42f,1/.55f,1/.03f);
            Art.Text("Page heading","RÉSUMÉ",new Vector3(0,.215f,0),.034f,dark,.36f,face);
            Art.Box("Page rule",new Vector3(0,.18f,0),new Vector3(.34f,.004f,.002f),Art.Coral,false,face,.001f);
            var line=Art.Text("Page line",text,new Vector3(0,.05f,0),.06f,dark,.36f,face);
            line.rectTransform.sizeDelta=new Vector2(.36f,.2f);line.enableAutoSizing=true;line.fontSizeMin=.2f;line.fontSizeMax=.42f;
            for(int k=0;k<4;k++)Art.Box("Page text",new Vector3(-.03f+(k%2)*.02f,-.1f-k*.035f,0),new Vector3(.3f-(k%3)*.05f,.008f,.002f),Art.Mint,false,face,.001f);
            return g;
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
            ocean.water=WaterMesh(horizon,out var waves);ocean.waves=waves;waves.room=ocean.transform;ocean.dayWater=Art.Water;ocean.nightWater=Art.NightWater;
            for(int i=0;i<20;i++)
            {float a=i*2.39f;Art.Facet("Far island",new Vector3((i%2==0?-1:1)*(9+i%5*3),-.6f,12+i*1.8f),new Vector3(7,4+i%4,7),i%2==0?Art.Teal:Art.Navy,5,horizon);}
            Art.Facet("The accepting shore",new Vector3(0,-.8f,20),new Vector3(12,3,7),Art.Cream,9,horizon);
            Art.Shape("Lighthouse tower",PrimitiveType.Cylinder,new Vector3(0,2.5f,20),new Vector3(1.1f,2.6f,1.1f),Art.Cream,true,horizon);
            Art.Shape("Lighthouse stripe",PrimitiveType.Cylinder,new Vector3(0,2.9f,20),new Vector3(1.12f,.38f,1.12f),Art.Coral,false,horizon);
            var lantern=Art.Shape("Lantern",PrimitiveType.Cylinder,new Vector3(0,5.3f,20),new Vector3(1.3f,.35f,1.3f),Art.Gold,false,horizon);ocean.lantern=lantern.GetComponent<MeshRenderer>();ocean.dayLantern=Art.Gold;ocean.nightLantern=Art.Lantern;
            Art.Facet("Lighthouse roof",new Vector3(0,5.96f,20),new Vector3(2,1,2),Art.Navy,8,horizon);
            // The light turns clockwise seen from above: a beam and a fainter one behind it, about nine seconds a turn.
            var beacon=Art.Group("Lighthouse light",new Vector3(0,5.3f,20),horizon);var spin=beacon.gameObject.AddComponent<Spin>();spin.degreesPerSecond=40;ocean.beacon=spin;
            var beam=Art.Beam("Lighthouse beam",Vector3.zero,70,9,Art.BeamGlow,beacon);beam.transform.localRotation=Quaternion.Euler(2,0,0);
            var beamBack=Art.Beam("Lighthouse beam behind",Vector3.zero,70,9,Art.BeamGlow,beacon);beamBack.GetComponent<MeshFilter>().sharedMesh=beam.GetComponent<MeshFilter>().sharedMesh;beamBack.transform.localRotation=Quaternion.Euler(0,180,0)*Quaternion.Euler(2,0,0);
            ocean.beams=new[]{beam.GetComponent<MeshRenderer>(),beamBack.GetComponent<MeshRenderer>()};ocean.dayBeam=Art.BeamGlow;ocean.nightBeam=Art.BeamGlowNight;
            ocean.sun=Art.Shape("Sun",PrimitiveType.Sphere,new Vector3(-14,13,48),Vector3.one*7,Art.Gold,false,horizon);
            // A sign standing in the sea in front of the shore, so its words never sit on the pale shore or the tower.
            Fitted(Art.Box("Welcome sign",new Vector3(0,1.25f,17.03f),new Vector3(2.1f,.46f,.05f),Art.Navy,false,horizon),.08f);
            for(int side=-1;side<=1;side+=2)Art.Box("Welcome sign post",new Vector3(side*.9f,.55f,17.03f),new Vector3(.08f,1.4f,.08f),Art.Wood,false,horizon);
            Art.Halo(Art.Text("Distant words","YOU ARE STILL WELCOME",new Vector3(0,1.25f,17),.24f,ivory,8,horizon));
            // Night: a moon and a dome of stars, switched on by the OCEAN AT NIGHT option. They stay put while the shore moves.
            var sky=Art.Group("Night sky",new Vector3(0,.3f,0));
            Art.Stars("Stars",260,66,Art.Star,sky);
            Art.Shape("Moon",PrimitiveType.Sphere,new Vector3(-16,27,50),Vector3.one*5.5f,Art.Moon,false,sky);
            sky.gameObject.SetActive(false);ocean.nightSky=sky.gameObject;
            Art.Tiled("Boat deck",new Vector3(0,.12f,-.2f),new Vector3(2.5f,.25f,4.3f),Art.Wood,new Vector3(.42f,0,0)).AddComponent<TeleportSurface>();
            for(int side=-1;side<=1;side+=2){Art.Box("Boat gunwale",new Vector3(side*1.25f,.37f,-.2f),new Vector3(.16f,.55f,4.4f),Art.Coral);}
            Art.Box("Boat prow",new Vector3(0,.37f,1.97f),new Vector3(2.6f,.5f,.18f),Art.Coral);
            Art.Box("Boat stern",new Vector3(0,.37f,-2.4f),new Vector3(2.6f,.5f,.18f),Art.Coral);
            for(int z=0;z<2;z++)Art.Box("Bench",new Vector3(0,.45f,.9f-z*2.7f),new Vector3(2.3f,.14f,.38f),Art.Wood);
            // A lantern on a post at the bow. It glows by itself, with no real-time light.
            Art.Box("Boat lantern post",new Vector3(-.95f,.9f,1.9f),new Vector3(.06f,.6f,.06f),Art.Wood,false);
            Art.Box("Boat lantern",new Vector3(-.95f,1.28f,1.9f),new Vector3(.2f,.26f,.2f),Art.Lantern,false);
            // Two oars, each held in an oarlock on the gunwale. The oar turns about its lock; the handle and blade swing opposite ways.
            var oars=new List<Oar>();
            for(int side=-1;side<=1;side+=2)
            {
                var lockAt=new Vector3(side*1.25f,.78f,-.55f);
                Art.Box("Oarlock block",new Vector3(side*1.25f,.665f,-.55f),new Vector3(.22f,.04f,.22f),Art.Metal);
                Art.Box("Oarlock pin",new Vector3(side*1.25f,.725f,-.55f),new Vector3(.05f,.16f,.05f),Art.Metal,false);
                var oar=Art.Group("Oar "+side,lockAt);
                var g=oar.gameObject.AddComponent<Grabbable>();g.paddle=true;g.label="Oar";g.room=1;var rb=oar.gameObject.AddComponent<Rigidbody>();rb.isKinematic=true;
                var o=oar.gameObject.AddComponent<Oar>();o.side=side;oars.Add(o);
                // A generous grip zone around the handle, so a hand does not have to find a thin shaft.
                var grip=oar.gameObject.AddComponent<BoxCollider>();grip.center=new Vector3(0,0,-Oar.InboardLength+.25f);grip.size=new Vector3(.22f,.22f,.55f);
                Art.Box("Oar shaft",new Vector3(0,0,(Oar.OutboardLength-Oar.InboardLength)/2),new Vector3(.065f,.065f,Oar.InboardLength+Oar.OutboardLength),Art.Wood,false,oar);
                Art.Box("Oar grip",new Vector3(0,0,-Oar.InboardLength+.17f),new Vector3(.095f,.095f,.34f),Art.Coral,false,oar);
                Art.Box("Oar collar",Vector3.zero,new Vector3(.13f,.13f,.17f),Art.Gold,false,oar);
                Art.Box("Oar blade",new Vector3(0,0,Oar.OutboardLength-.32f),new Vector3(.05f,.26f,.64f),Art.Gold,false,oar);
                o.ResetPose();
            }
            ocean.oars=oars.ToArray();
            // The lighthouse sails up the middle of the view as you row, so the heading stands off to one side, turned toward the boat, where it never passes behind the tower.
            // It is on the right because the sun and the moon are on the left.
            var oceanTurn=Quaternion.Euler(0,21,0);
            Art.Text("Ocean title","01  /  OCEAN OF SHAME",new Vector3(4.3f,3.9f,10),.55f,ivory,7.2f).transform.localRotation=oceanTurn;
            Art.Text("Ocean intention","A feeling can be vast without being forever.",new Vector3(4.3f,3.2f,10),.3f,ivory,7.2f).transform.localRotation=oceanTurn;
            Backed(1,"Distance",Fitted(Art.Box("Distance plate",new Vector3(0,1.2f,1.84f),new Vector3(1.55f,.26f,.03f),Art.Navy,false),.06f));
            ocean.distance=Art.Text("Distance","14   SMALL STROKES TO SHORE",new Vector3(0,1.2f,1.8f),.13f,ivory,2.6f);
            Art.Button("Accessible rowing","ROW",new Vector3(0,.85f,1.6f),new Vector3(.8f,.32f,.1f),Art.Teal,ActionKind.Row,d);
            // The how-to-row note sits off to the left on its own plate, turned toward the boat. Straight ahead it would sail across the lighthouse and its beam.
            var rowTurn=Quaternion.Euler(0,-30.7f,0);
            var rowPlate=Backed(1,"Rowing instruction",Fitted(Art.Box("Rowing plate",new Vector3(-2.6f,2.3f,3.45f),new Vector3(2.9f,.72f,.03f),Art.Navy,false),.1f,1.6f));rowPlate.transform.localRotation=rowTurn;
            Art.Text("Rowing instruction","Hold an oar. Push it forward, then draw it back.\nOr point at ROW and select.\nBlink steps keep the boat steady.",new Vector3(-2.577f,2.3f,3.411f),.105f,ivory,2.7f).transform.localRotation=rowTurn;
            // The sea itself: waves that roll in step with the moving water, a bed of open sea, lapping at the hull and wind, and surf from the
            // shore that grows as the boat nears it, with gulls by day and a bell buoy at night (see SeaSounds and OceanWaves).
            var sea=ocean.gameObject.AddComponent<SeaSounds>();ocean.sea=sea;waves.sea=sea;
            sea.swell=Sound("Ambience - Waves",.8f);sea.wash=Sound("Ambience - Ocean",.2f);sea.lapping=Sound("Ambience - Sea lapping",.3f);sea.wind=Sound("Ambience - Night sea",0f);
            // The surf is a real place: it is on the shore, and the shore is part of the moving horizon, so it gets louder and clearer as you row.
            var beach=Art.Group("Surf on the shore",new Vector3(0,.6f,17.5f),horizon);
            var surf=beach.gameObject.AddComponent<AudioSource>();surf.clip=SoundBank.Get("Ambience - Surf");surf.loop=true;surf.playOnAwake=true;surf.volume=.8f;surf.priority=72;
            surf.spatialBlend=1;surf.rolloffMode=AudioRolloffMode.Logarithmic;surf.minDistance=7;surf.maxDistance=90;surf.dopplerLevel=0;sea.surf=surf;
            sea.gulls=new[]{SoundBank.Get("SFX - Gull"),SoundBank.Get("SFX - Gull far")};sea.bell=SoundBank.Get("SFX - Buoy bell");sea.creak=SoundBank.Get("SFX - Oarlock");
        }
        // The sea: a grid of flat facets that the swells move (OceanWaves). The picture saved in the scene shows it mid-swell.
        static MeshRenderer WaterMesh(Transform parent,out OceanWaves waves)
        {
            var go=new GameObject("Low polygon ocean",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=OceanWaves.CreateMesh(OceanWaves.PreviewTime);
            var water=go.GetComponent<MeshRenderer>();water.sharedMaterial=Art.Water;water.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            waves=go.AddComponent<OceanWaves>();return water;
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
            // The heading hangs on the back wall above the truck's cab, high enough that its lowest line clears the cab from the arrival point.
            Header(2,"02 / HEAVY THINGS","Processing, not erasing.","Pick up each feeling. Point at the truck and let go, or throw it in.",new Vector3(0,3.95f,15.75f),10,0,.7f,1.2f,1.25f);
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
            var packed=new Grabbable[5];
            for(int i=0;i<5;i++)
            {
                // Rest each box on its plinth (top at 0.72 m). Starting inside the plinth made physics shove the boxes askew.
                Vector3 p=new Vector3(-2.8f+i*1.4f,1.008f,-.3f+(i%2)*.4f);
                Art.Box("Packing plinth",new Vector3(p.x,.36f,p.z),new Vector3(.95f,.72f,.95f),Art.Wood);
                var item=Item(names[i],"",2,p,new Vector3(.6f,.57f,.6f),colors[i]);item.throwable=true;packed[i]=item;
                Art.Box("Packing tape",new Vector3(0,.503f,0),new Vector3(.14f,.02f,1.01f),Art.Cream,false,item.transform);
            }
            Blast(packed);
            var truck=Art.Group("Feelings processing truck",new Vector3(0,0,6.6f));
            Art.Box("Truck bed",new Vector3(0,.35f,0),new Vector3(3.2f,.3f,4),Art.Metal,true,truck);
            for(int s=-1;s<=1;s+=2){Art.Box("Truck side",new Vector3(s*1.6f,1.5f,.2f),new Vector3(.15f,2.2f,3.6f),Art.Teal,true,truck);for(int z=-1;z<=1;z+=2){var wheel=Art.Shape("Wheel",PrimitiveType.Cylinder,new Vector3(s*1.6f,.36f,z*1.2f),new Vector3(.73f,.17f,.73f),Art.Ink,true,truck);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}}
            Art.Box("Truck cabin",new Vector3(0,1.4f,2.25f),new Vector3(3.2f,2.2f,1),Art.Teal,true,truck);
            Art.Box("Loading ramp",new Vector3(0,.17f,-2.55f),new Vector3(3.2f,.25f,1.5f),Art.Gold,true,truck);
            var bay=Zone("Processing bay",new Vector3(0,1.25f,6.1f),new Vector3(2.8f,1.7f,2.8f),2,"",5);
            Target(bay.gameObject,2,new Vector3(0,.2f,0),1.3f,"Point at the truck and let go.",Outline("Truck bay highlight",new Vector3(0,1.42f,4.62f),new Vector2(2.95f,1.85f),true));
            // One sign at the truck's open end carries both lines. Anything higher would stand between the player and the heading on the back wall.
            Fitted(Art.Box("Bay sign plate",new Vector3(0,1.27f,4.58f),new Vector3(2f,.8f,.04f),Art.Navy,false),.1f,1.2f);
            Art.Text("Truck message","ROOM TO CARRY ON",new Vector3(0,1.47f,4.53f),.22f,ivory,3);
            Art.Text("Bay sign","SEND BOXES HERE",new Vector3(0,1.07f,4.53f),.16f,ivory,3);
            for(int i=0;i<5;i++)Art.Box("Loading floor line",new Vector3(0,.015f,1.3f+i*.6f),new Vector3(.45f,.02f,.15f),Art.Gold,false);
            Sound("Ambience - Warehouse",.55f);
        }
        static void Kitchen()
        {
            Shell(13,15,Art.Cream);var k=d.rooms[3].root.AddComponent<KitchenRoom>();d.kitchen=k;k.director=d;
            // Packed close and high, so the espresso machine and backsplash never cut into a line.
            Header(3,"03 / A SMALL SPILL","Everyone is watching.","At least, that is how it feels. Make a cup of coffee at your own pace.",new Vector3(0,3.95f,8.7f),8,0,.7f);
            var seat=d.rooms[3].spawn.localPosition;
            // The base cabinets are hollow, with six real doors. Pointing at a door opens it, and one cupboard hides an orange mug.
            // The hinges are on the outer edges, so the doors swing away from the middle and nothing opens across the view of the machine.
            Art.Box("Cabinet plinth",new Vector3(0,.04f,5),new Vector3(8,.08f,1.1f),Art.Navy);
            Art.Box("Cabinet back",new Vector3(0,.52f,5.5f),new Vector3(8,.88f,.1f),Art.White);
            Art.Box("Cabinet ceiling",new Vector3(0,.945f,4.97f),new Vector3(7.8f,.03f,1.04f),Art.Cream,false);
            foreach(float edge in new[]{-3.95f,3.95f})Art.Box("Cabinet end",new Vector3(edge,.52f,4.97f),new Vector3(.1f,.88f,1.04f),Art.Teal);
            for(int i=1;i<6;i++)Art.Box("Cabinet divider",new Vector3(-3.9f+i*1.3f,.52f,4.97f),new Vector3(.05f,.88f,1.04f),Art.Cream);
            var cabinetDoors=new CabinetDoor[6];
            for(int i=0;i<6;i++)
            {
                bool leftHinge=i<3;float side=leftHinge?1:-1;
                float hingeX=leftHinge?-3.9f+i*1.3f+.02f:-3.9f+(i+1)*1.3f-.02f;
                var pivot=Art.Group("Cupboard "+(i+1),new Vector3(hingeX,.52f,4.43f));
                var swing=pivot.gameObject.AddComponent<CabinetDoor>();swing.openAngle=leftHinge?100:-100;cabinetDoors[i]=swing;
                var kinematic=pivot.gameObject.AddComponent<Rigidbody>();kinematic.isKinematic=true;kinematic.useGravity=false;
                var leaf=Art.Box("Cupboard door",new Vector3(side*.63f,0,0),new Vector3(1.26f,.86f,.035f),Art.Teal,true,pivot);
                var opener=leaf.AddComponent<Interactable>();opener.kind=ActionKind.Cabinet;opener.director=d;opener.value=i;opener.prompt="Cupboard";
                Art.Box("Door panel",new Vector3(side*.63f,0,-.02f),new Vector3(1f,.6f,.012f),Art.Navy,false,pivot,.004f);
                Art.Box("Cupboard pull",new Vector3(side*1.14f,.3f,-.045f),new Vector3(.04f,.26f,.03f),Art.Gold,false,pivot,.006f);
            }
            k.doors=cabinetDoors;
            Art.Box("Detailed stone worktop",new Vector3(0,1,5),new Vector3(8.2f,.08f,1.22f),Art.Cream);
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
            Backed(3,"Help permission",Fitted(Art.Box("Help plate",new Vector3(2.12f,1.89f,4.475f),new Vector3(1.4f,.46f,.03f),Art.Navy,false),.08f,1.15f));
            Art.Text("Help permission","Available from the start.\nYou do not have to earn it.",new Vector3(2.12f,1.89f,4.44f),.12f,ivory,2.1f);
            // More of the machine: a group head joining the spout to the front, the portafilter handle, a pressure gauge, a steam valve and wand on the left, and a lever on the
            // right that pulls down whenever it brews. The lever answers to a point-and-press just like BREW, and so does the steam wand (the valve and the wand both).
            Interactable Pointer(string pointerName,Vector3 pointerAt,Vector3 pointerSize,ActionKind pointerKind,Transform pointerParent=null)
            {
                var invisible=Art.Box(pointerName,pointerAt,pointerSize,Art.Ink,true,pointerParent);
                Object.DestroyImmediate(invisible.GetComponent<MeshRenderer>());Object.DestroyImmediate(invisible.GetComponent<MeshFilter>());
                var pointer=invisible.AddComponent<Interactable>();pointer.kind=pointerKind;pointer.director=d;pointer.prompt=pointerName;return pointer;
            }
            Art.Box("Group head",new Vector3(0,1.5f,4.5f),new Vector3(.4f,.2f,.2f),Art.Gold,false);
            Art.Shape("Spout collar",PrimitiveType.Cylinder,new Vector3(0,1.69f,4.36f),new Vector3(.17f,.02f,.17f),Art.Gold,false);
            Art.Line("Portafilter handle",new Vector3(-.03f,1.52f,4.3f),new Vector3(-.3f,1.52f,4.02f),.05f,Art.Ink);
            Art.Shape("Handle cap",PrimitiveType.Sphere,new Vector3(-.3f,1.52f,4.02f),Vector3.one*.06f,Art.Ink,false);
            Art.Box("Machine trim",new Vector3(0,2.05f,4.575f),new Vector3(1.62f,.025f,.03f),Art.Gold,false);
            var gaugeRim=Art.Shape("Gauge rim",PrimitiveType.Cylinder,new Vector3(.56f,1.93f,4.57f),new Vector3(.17f,.012f,.17f),Art.Gold,false);gaugeRim.transform.localRotation=Quaternion.Euler(90,0,0);
            var gaugeFace=Art.Shape("Gauge face",PrimitiveType.Cylinder,new Vector3(.56f,1.93f,4.562f),new Vector3(.145f,.012f,.145f),Art.White,false);gaugeFace.transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Box("Gauge needle",new Vector3(.58f,1.95f,4.545f),new Vector3(.012f,.07f,.006f),Art.Coral,false).transform.localRotation=Quaternion.Euler(0,0,-40);
            var valve=Art.Shape("Steam valve",PrimitiveType.Cylinder,new Vector3(-.52f,1.4f,4.56f),new Vector3(.1f,.022f,.1f),Art.Gold,false);valve.transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Box("Valve handle",new Vector3(-.52f,1.4f,4.535f),new Vector3(.15f,.03f,.03f),Art.Ink,false).transform.localRotation=Quaternion.Euler(0,0,25);
            Art.Shape("Wand socket",PrimitiveType.Sphere,new Vector3(-.88f,1.52f,4.72f),Vector3.one*.075f,Art.Gold,false);
            Art.Line("Steam wand",new Vector3(-.88f,1.52f,4.72f),new Vector3(-1.02f,1.52f,4.72f),.04f,Art.Metal);
            Art.Shape("Wand elbow",PrimitiveType.Sphere,new Vector3(-1.02f,1.52f,4.72f),Vector3.one*.055f,Art.Metal,false);
            Art.Line("Steam wand",new Vector3(-1.02f,1.52f,4.72f),new Vector3(-1.02f,1.16f,4.72f),.04f,Art.Metal);
            Art.Shape("Wand elbow",PrimitiveType.Sphere,new Vector3(-1.02f,1.16f,4.72f),Vector3.one*.055f,Art.Metal,false);
            Art.Line("Steam wand tip",new Vector3(-1.02f,1.16f,4.72f),new Vector3(-.95f,1.1f,4.58f),.03f,Art.Metal);
            Art.Shape("Steam nozzle",PrimitiveType.Sphere,new Vector3(-.95f,1.1f,4.58f),Vector3.one*.04f,Art.Ink,false);
            Pointer("Steam wand pointer",new Vector3(-1f,1.34f,4.68f),new Vector3(.24f,.5f,.24f),ActionKind.Steam);
            Pointer("Steam valve pointer",new Vector3(-.52f,1.4f,4.5f),new Vector3(.2f,.2f,.14f),ActionKind.Steam);
            var leverPivot=Art.Group("Machine lever",new Vector3(.86f,1.62f,4.54f));leverPivot.localRotation=Quaternion.Euler(0,0,-55);k.lever=leverPivot;
            var leverHub=Art.Shape("Lever hub",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.1f,.03f,.1f),Art.Gold,false,leverPivot);leverHub.transform.localRotation=Quaternion.Euler(90,0,0);
            Art.Box("Lever arm",new Vector3(0,.18f,0),new Vector3(.04f,.36f,.04f),Art.Metal,false,leverPivot,.008f);
            Art.Shape("Lever grip",PrimitiveType.Sphere,new Vector3(0,.39f,0),Vector3.one*.1f,Art.Coral,false,leverPivot);
            Pointer("Lever pointer",new Vector3(0,.2f,0),new Vector3(.2f,.46f,.14f),ActionKind.Brew,leverPivot);
            // The steam wand's plume: soft white puffs that rise from the nozzle while it blows (KitchenRoom emits them).
            var steamAt=Art.Group("Steam",new Vector3(-.95f,1.08f,4.58f));steamAt.localRotation=Quaternion.Euler(-90,0,0);
            var steam=steamAt.gameObject.AddComponent<ParticleSystem>();steam.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var steamMain=steam.main;steamMain.playOnAwake=false;steamMain.loop=true;steamMain.duration=5;steamMain.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.3f);steamMain.startSpeed=new ParticleSystem.MinMaxCurve(.2f,.45f);
            steamMain.startSize=new ParticleSystem.MinMaxCurve(.05f,.1f);steamMain.gravityModifier=-.06f;steamMain.maxParticles=90;steamMain.simulationSpace=ParticleSystemSimulationSpace.World;
            var steamColours=new Gradient{mode=GradientMode.Fixed};steamColours.SetKeys(new[]{new GradientColorKey(new Color(.96f,.97f,.97f),.34f),new GradientColorKey(new Color(.87f,.91f,.92f),.67f),new GradientColorKey(new Color(.79f,.85f,.87f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
            var steamPick=new ParticleSystem.MinMaxGradient(steamColours);steamPick.mode=ParticleSystemGradientMode.RandomColor;steamMain.startColor=steamPick;
            var steamEmission=steam.emission;steamEmission.rateOverTime=0;
            var steamShape=steam.shape;steamShape.shapeType=ParticleSystemShapeType.Cone;steamShape.angle=16;steamShape.radius=.012f;
            var steamGrowth=steam.sizeOverLifetime;steamGrowth.enabled=true;steamGrowth.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(.4f,1),new Keyframe(1,0)));
            var steamNoise=steam.noise;steamNoise.enabled=true;steamNoise.strength=.12f;steamNoise.frequency=.8f;steamNoise.quality=ParticleSystemNoiseQuality.Low;
            var steamRenderer=steam.GetComponent<ParticleSystemRenderer>();steamRenderer.renderMode=ParticleSystemRenderMode.Mesh;steamRenderer.mesh=Geometry.Get(PrimitiveType.Sphere);steamRenderer.sharedMaterial=Art.ConfettiPaper;
            steamRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;steamRenderer.receiveShadows=false;
            k.steam=steam;
            // Mugs. "Your cup" starts the chapter, three more stand on the counter beside it, and an orange one is shut away in a cupboard. Any of them can be carried, sent under the spout
            // by pointing, or thrown, and a hard knock breaks it: it bursts, then comes back whole on its shelf (see Mug).
            Mug MakeMug(string mugLabel,Vector3 mugAt,Material glaze)
            {
                var vessel=Art.Shape(mugLabel,PrimitiveType.Cylinder,mugAt,new Vector3(.25f,.17f,.25f),glaze);
                var rigid=vessel.AddComponent<Rigidbody>();rigid.mass=.2f;rigid.interpolation=RigidbodyInterpolation.Interpolate;rigid.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rigid.maxDepenetrationVelocity=1.5f;
                var grab=vessel.AddComponent<Grabbable>();grab.label=mugLabel;grab.room=3;grab.upright=true;grab.tossable=true;grab.category="mug";
                // A flat-bottomed collider that matches the faceted cup. The primitive's capsule has a round base and tips over.
                Object.DestroyImmediate(vessel.GetComponent<Collider>());var hull=vessel.AddComponent<MeshCollider>();hull.sharedMesh=vessel.GetComponent<MeshFilter>().sharedMesh;hull.convex=true;
                Art.Shape("Cup interior",PrimitiveType.Cylinder,new Vector3(0,1.001f,0),new Vector3(.88f,.012f,.88f),Art.Ink,false,vessel.transform);
                Art.Box("Cup handle",new Vector3(.63f,.1f,0),new Vector3(.45f,.6f,.22f),glaze,false,vessel.transform);
                var liquid=Art.Shape("Coffee",PrimitiveType.Cylinder,new Vector3(0,.98f,0),new Vector3(.85f,.03f,.85f),Art.Wood,false,vessel.transform);liquid.SetActive(false);
                var piece=vessel.AddComponent<Mug>();piece.item=grab;piece.coffee=liquid.transform;piece.body=glaze.color;return piece;
            }
            var cupMug=MakeMug("Your cup",new Vector3(-1.7f,1.23f,4.6f),Art.White);
            var allMugs=new List<Mug>{cupMug,MakeMug("Teal mug",new Vector3(-2.3f,1.23f,4.6f),Art.Teal),MakeMug("Coral mug",new Vector3(-2.9f,1.23f,4.6f),Art.Coral),MakeMug("Navy mug",new Vector3(-2f,1.23f,5.05f),Art.Navy)};
            // The orange mug waits on the shelf in the fifth cupboard from the left, near the front, where it can be seen from the seat once the door is open.
            var hiddenMug=MakeMug("Orange mug",new Vector3(1.75f,.26f,4.78f),Art.Orange);hiddenMug.orange=true;allMugs.Add(hiddenMug);
            k.mugs=allMugs.ToArray();k.orangeMug=hiddenMug;k.cup=cupMug.item;k.coffee=cupMug.coffee;
            // What the other cupboards hold: plates, jars, bowls, tins, a kettle and folded cloths. For looking at only.
            void Shelf(string shelfName,PrimitiveType shelfType,float shelfX,float shelfZ,float diameter,float height,Material glaze)
            {Art.Shape(shelfName,shelfType,new Vector3(shelfX,.08f+height/2,shelfZ),new Vector3(diameter,shelfType==PrimitiveType.Sphere?height:height/2,diameter),glaze,false);}
            void Stack(float stackX,float stackZ,float across,int plates,Material glaze)
            {Shelf("Plates",PrimitiveType.Cylinder,stackX,stackZ,across*.9f,plates*.035f,glaze);Art.Shape("Plate rim",PrimitiveType.Cylinder,new Vector3(stackX,.08f+plates*.035f,stackZ),new Vector3(across,.008f,across),glaze,false);}
            Stack(-3.55f,4.85f,.4f,5,Art.White);Stack(-2.95f,4.85f,.34f,4,Art.Cream);Stack(-3.25f,5.25f,.4f,3,Art.Mint);
            Shelf("Jar",PrimitiveType.Cylinder,-2.25f,4.9f,.18f,.26f,Art.Cream);Shelf("Jar",PrimitiveType.Cylinder,-1.95f,5f,.2f,.34f,Art.Wood);Shelf("Jar",PrimitiveType.Cylinder,-1.65f,4.85f,.22f,.2f,Art.Gold);
            Shelf("Bowl",PrimitiveType.Sphere,-.9f,4.9f,.5f,.28f,Art.Teal);Shelf("Bowl",PrimitiveType.Sphere,-.4f,5.05f,.34f,.2f,Art.Coral);
            Shelf("Tin",PrimitiveType.Cylinder,.3f,4.85f,.3f,.16f,Art.Metal);Shelf("Tin",PrimitiveType.Cylinder,.7f,5f,.22f,.22f,Art.Coral);Shelf("Tin",PrimitiveType.Cylinder,1f,4.8f,.26f,.14f,Art.Gold);
            Art.Box("Folded cloth",new Vector3(2.25f,.105f,4.85f),new Vector3(.4f,.05f,.3f),Art.Mint,false);
            Shelf("Kettle",PrimitiveType.Sphere,3.1f,5f,.3f,.26f,Art.Metal);Art.Line("Kettle spout",new Vector3(3.2f,.24f,5f),new Vector3(3.36f,.32f,4.95f),.035f,Art.Metal);Art.Box("Folded cloth",new Vector3(3.5f,.105f,4.85f),new Vector3(.4f,.05f,.3f),Art.Cream,false);
            // When a mug breaks: shards in its own colour and white, in one system that falls to the counter and one that falls to the floor (each lands on a flat plane, which is cheaper than
            // collision with the room), a puff of ceramic dust, and a few splashes of coffee that stay where a full mug broke.
            var breaking=Art.Group("Mug breaking",Vector3.zero);
            var counterPlane=Art.Group("Counter plane",new Vector3(0,1.043f,0),breaking);var floorPlane=Art.Group("Floor plane",new Vector3(0,.012f,0),breaking);
            var fadeOut=new AnimationCurve(new Keyframe(0,1),new Keyframe(.75f,1),new Keyframe(1,0));var shardMesh=Geometry.Get(PrimitiveType.Cube);
            k.shardsCounter=BlastSystem("Mug shards over the counter",breaking,shardMesh,new[]{Color.white},34,new Vector2(1.1f,1.7f),new Vector2(1.2f,3.2f),.05f,1.2f,.6f,new Vector2(.035f,.08f),fadeOut,counterPlane,chip:true);
            k.shardsFloor=BlastSystem("Mug shards over the floor",breaking,shardMesh,new[]{Color.white},34,new Vector2(1.1f,1.7f),new Vector2(1.2f,3.2f),.05f,1.2f,.6f,new Vector2(.035f,.08f),fadeOut,floorPlane,chip:true);
            k.dust=BlastSystem("Mug dust",breaking,Geometry.Get(PrimitiveType.Sphere),new[]{new Color(.95f,.93f,.88f),new Color(.86f,.83f,.77f)},6,new Vector2(.7f,1.1f),new Vector2(.4f,1f),.05f,-.03f,1.4f,new Vector2(.1f,.2f),new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.3f,1),new Keyframe(1,0)));
            var splashList=new List<Transform>();
            for(int i=0;i<4;i++){var splash=Art.Shape("Coffee splash "+i,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.52f-i*.05f,.004f,.52f-i*.05f),Art.Wood,false);splash.SetActive(false);splashList.Add(splash.transform);}
            k.splashes=splashList.ToArray();
            k.shatterSound=SoundBank.Get("SFX - Shatter");k.cabinetSound=SoundBank.Get("SFX - Cabinet");k.steamSound=SoundBank.Get("SFX - Steam");
            // Send a mug under the spout from your seat: point at the drip tray while holding it and let go. Any mug will do.
            var glow=Art.Shape("Spout highlight",PrimitiveType.Cylinder,new Vector3(0,1.112f,4.28f),new Vector3(.52f,.004f,.52f),d.player.rayMaterial,false);glow.SetActive(false);
            var spoutTarget=Target(mark,3,new Vector3(0,.18f,0),.45f,"Point under the spout and let go.",glow);spoutTarget.category="mug";
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
            // The archive has no ceiling, so beams and light strips cross the sky behind the heading. A deep-blue plate gives its letters a clean ground.
            Backed(4,"Room title",Fitted(Art.Box("Heading plate",new Vector3(0,3.7f,7.52f),new Vector3(7.6f,2.1f,.06f),Art.Navy,false),.24f,1.1f));
            Header(4,"04 / THE INFINITE ARCHIVE","Not every thought is a fact.","Pick up a note, point at its tray, and let go. You do not need to sort the whole room.",new Vector3(0,3.85f,7.4f),9);
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
                // Each tray's name hangs above it on a plate behind the tray (cards stand in front of it), clear of the cabinets and under the heading.
                Fitted(Art.Box(name+" sign plate",p+new Vector3(0,1.9f,.6f),new Vector3(1.9f,.7f,.05f),Art.Navy,false),.08f,1.1f,"trays");
                Art.Text(name+" sign",name,p+new Vector3(0,2.06f,.55f),.3f,ivory,2.1f);
                Art.Text(name+" description",new[]{"What happened","What my mind predicts","What I still carry"}[i],p+new Vector3(0,1.7f,.55f),.15f,muted,1.9f);
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
            // The skyline is dark and the sky is pale, so the closing words stand on a deep-blue board in ivory, readable from anywhere on the roof.
            var board=Art.Group("Rooftop message board",new Vector3(0,0,9.15f));
            Art.Box("Board face",new Vector3(0,2.75f,0),new Vector3(7.6f,3.4f,.12f),Art.Navy,false,board);
            Art.Box("Board trim",new Vector3(0,4.5f,-.02f),new Vector3(7.8f,.07f,.16f),Art.Gold,false,board);Art.Box("Board trim",new Vector3(0,1f,-.02f),new Vector3(7.8f,.07f,.16f),Art.Gold,false,board);
            for(int side=-1;side<=1;side+=2)Art.Box("Board leg",new Vector3(side*3.3f,.5f,.1f),new Vector3(.14f,1f,.14f),Art.Metal,false,board);
            d.ending=Art.Text("Ending","You can rest\nbefore you are ready.",new Vector3(0,3.6f,9),.78f,ivory,7);
            Art.Text("Epilogue","The office is only one room in your life.",new Vector3(0,2.35f,9),.36f,muted,7);
            Art.Text("Next step","For now: a glass of water. A message to someone. A little daylight.\nYou get to choose the next small thing.",new Vector3(0,1.55f,9),.25f,ivory,7);
            for(int i=0;i<26;i++){float x=-30+i*2.4f;float height=3+(i*7%9);Art.Box("Low poly city",new Vector3(x,-2+height*.5f,25+(i%3)*5),new Vector3(1.8f,height,2),i%2==0?Art.Teal:Art.Navy,false);}
            Art.Shape("Tomorrow's sun",PrimitiveType.Sphere,new Vector3(13,16,45),Vector3.one*9,Art.Gold,false);
            for(int side=-1;side<=1;side+=2){Art.Plant(new Vector3(side*5.5f,0,5));Art.Plant(new Vector3(side*5.8f,0,7));Art.Box("Bench seat",new Vector3(side*4,.5f,2.5f),new Vector3(2.3f,.15f,.65f),Art.Wood);for(int leg=-1;leg<=1;leg+=2)Art.Box("Bench leg",new Vector3(side*4+leg*.8f,.24f,2.5f),new Vector3(.1f,.48f,.55f),Art.Metal);}
        }
    }
}
