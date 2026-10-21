using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
namespace AfterHours
{
    // "The infinite archive": a reading floor with the sorting desk, the three trays and rows of filing cabinets, edged by a brass rail and a colonnade
    // instead of walls. Past the rail, stacks of shelving rise forty and fifty metres out of sight and run on in every direction, with aisles, cross
    // aisles, catwalks and bridges between them and small warm lamps on their ends, until a deep blue haze takes them (the haze, the sky and the light
    // come from ArchiveRoom). Loose pages drift down through the aisles.
    // The far stacks are flat colours stored per vertex on one cheap unlit material, shaded by which way each face looks and darker the higher it is,
    // and built as a handful of large meshes (one per side of the room) that stay out of the room's batches.
    public static partial class WorldBuilder
    {
        // Boxes of flat colour gathered into one mesh. Colours are stored per vertex, with the face's shade and the height already worked in.
        sealed class Stacks
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<Color> c=new List<Color>();readonly List<int> t=new List<int>();
            // Mesh colours are not converted to the project's linear colour space, so they are converted here.
            static Color Linear(Color x)=>QualitySettings.activeColorSpace==ColorSpace.Linear?x.linear:x;
            // The upper reaches go dark before the haze takes them, so the stacks seem to rise out of sight rather than stop.
            static float Height(float y)=>Mathf.Lerp(1,.38f,Mathf.Clamp01(y/36f));
            void Face(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color col,float shade,bool glow)
            {
                int n=v.Count;
                foreach(var p in new[]{a,b,d,e}){v.Add(p);var k=glow?1:shade*Height(p.y);c.Add(Linear(new Color(col.r*k,col.g*k,col.b*k,1)));}
                t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
            }
            // A box between two corners. The bottom is left out unless asked for (most boxes stand on something). A glowing box is not shaded.
            public void Box(Vector3 p,Vector3 q,Color col,bool bottom=false,bool glow=false)
            {
                var m=Vector3.Min(p,q);var M=Vector3.Max(p,q);
                Face(new Vector3(m.x,M.y,m.z),new Vector3(m.x,M.y,M.z),new Vector3(M.x,M.y,M.z),new Vector3(M.x,M.y,m.z),col,1f,glow);
                Face(new Vector3(m.x,m.y,m.z),new Vector3(m.x,M.y,m.z),new Vector3(M.x,M.y,m.z),new Vector3(M.x,m.y,m.z),col,.9f,glow);
                Face(new Vector3(M.x,m.y,M.z),new Vector3(M.x,M.y,M.z),new Vector3(m.x,M.y,M.z),new Vector3(m.x,m.y,M.z),col,.72f,glow);
                Face(new Vector3(m.x,m.y,M.z),new Vector3(m.x,M.y,M.z),new Vector3(m.x,M.y,m.z),new Vector3(m.x,m.y,m.z),col,.82f,glow);
                Face(new Vector3(M.x,m.y,m.z),new Vector3(M.x,M.y,m.z),new Vector3(M.x,M.y,M.z),new Vector3(M.x,m.y,M.z),col,.82f,glow);
                if(bottom)Face(new Vector3(m.x,m.y,m.z),new Vector3(M.x,m.y,m.z),new Vector3(M.x,m.y,M.z),new Vector3(m.x,m.y,M.z),col,.45f,glow);
            }
            public int Triangles=>t.Count/3;
            public void Build(Transform parent,string name)
            {
                var mesh=new Mesh{name="Archive "+name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject("Archive "+name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=Art.ArchiveStacks;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }
        // The far stacks' colours: dark blue shelving, pale boards, brass ends, books in the game's own colours, and warm lamps.
        static readonly Color StackBody=new Color32(0x22,0x37,0x4A,255),StackBoard=new Color32(0x7D,0x8E,0x96,255),StackBrass=new Color32(0x8C,0x71,0x48,255),
            StackBand=new Color32(0x6E,0x65,0x50,255),StackIron=new Color32(0x5E,0x6E,0x78,255),StackLamp=new Color32(0xFF,0xD5,0x8A,255);
        static readonly Color[] Spines={new Color32(0xCF,0xBC,0x96,255),new Color32(0x37,0x7C,0x80,255),new Color32(0xC8,0x78,0x63,255),new Color32(0xDC,0xD4,0xC0,255),new Color32(0x8E,0x6F,0x4E,255),new Color32(0x4F,0x6F,0x86,255)};
        // The reading floor: 18 metres wide, from just behind the arrival point to 48 metres ahead.
        const float ReadingHalfWidth=9,ReadingNear=-5,ReadingFar=48;

        static void Archive()
        {
            var room=d.rooms[ArchiveRoom.Room].root.AddComponent<ArchiveRoom>();room.director=d;d.archive=room;d.rooms[ArchiveRoom.Room].total=6;
            ReadingFloor();
            // The archive has no ceiling, so beams and light strips cross the dark behind the heading. A deep-blue plate gives its letters a clean ground.
            Backed(4,"Room title",Fitted(Art.Box("Heading plate",new Vector3(0,3.7f,7.52f),new Vector3(7.6f,2.1f,.06f),Art.Navy,false),.24f,1.1f));
            Header(4,"04 / THE INFINITE ARCHIVE","Not every thought is a fact.","Pick up a note, point at its tray, and let go. You do not need to sort the whole room.",new Vector3(0,3.85f,7.4f),9);
            for(int x=-3;x<=3;x++)for(int z=0;z<13;z++)
            {
                if(x==0||Mathf.Abs(x)==1&&z<3)continue;
                var cab=Art.Group("Repeating cabinet",new Vector3(x*2.55f,0,3+z*3.3f));
                Art.Box("Cabinet body",new Vector3(0,1.25f,0),new Vector3(1.5f,2.5f,.95f),z%3==0?Art.Teal:Art.Metal,true,cab);
                for(int drawer=0;drawer<4;drawer++){float y=.33f+drawer*.61f;Art.Box("Drawer",new Vector3(0,y,-.492f),new Vector3(1.39f,.55f,.025f),Art.Navy,false,cab);Art.Box("Label slot",new Vector3(0,y+.11f,-.52f),new Vector3(.39f,.1f,.03f),Art.Paper,false,cab);Art.Box("Handle",new Vector3(0,y-.09f,-.57f),new Vector3(.36f,.04f,.065f),Art.Gold,false,cab);}
            }
            // Each beam rests on a pair of columns just outside the rail, so the reading floor is lined by a colonnade that opens onto the stacks.
            for(int z=0;z<13;z++)
            {
                float at=z*3.3f+3;
                Art.Box("Archive ceiling beam",new Vector3(0,4.5f,at),new Vector3(19.7f,.15f,.18f),Art.Metal,false);Art.Box("Light in the distance",new Vector3(0,4.32f,at),new Vector3(1.6f,.03f,.12f),Art.White,false);
                for(int s=-1;s<=1;s+=2)Art.Box("Archive column",new Vector3(s*9.7f,2.25f,at),new Vector3(.26f,4.5f,.26f),Art.Metal,false);
            }
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
            FarStacks(room);DriftingPages(room);
            Sound("Ambience - Archive",.6f);
        }

        // ----- The reading floor: the floor you can walk on, the dark floor of the stacks beyond it, and a brass rail all the way round -----
        static void ReadingFloor()
        {
            Floor(new Vector3(0,-.15f,(ReadingNear+ReadingFar)/2),new Vector3(ReadingHalfWidth*2,.3f,ReadingFar-ReadingNear),Art.Navy);
            // The stacks stand on a dark floor that runs on under the haze. It sits a little below the reading floor, so only the reading floor takes a teleport.
            Art.Box("Floor of the stacks",new Vector3(0,-.17f,22),new Vector3(150,.3f,170),Art.Ink,true,null,0f).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var corners=new[]{new Vector3(-ReadingHalfWidth,0,ReadingNear),new Vector3(-ReadingHalfWidth,0,ReadingFar),new Vector3(ReadingHalfWidth,0,ReadingFar),new Vector3(ReadingHalfWidth,0,ReadingNear)};
            for(int e=0;e<4;e++)
            {
                Vector3 a=corners[e],b=corners[(e+1)%4],dir=b-a,mid=(a+b)/2;float length=dir.magnitude;bool alongZ=Mathf.Abs(dir.z)>Mathf.Abs(dir.x);
                Vector3 Along(float size,float thick)=>alongZ?new Vector3(thick,size,length):new Vector3(length,size,thick);
                int posts=Mathf.Max(1,Mathf.RoundToInt(length/2.4f));
                for(int i=0;i<posts;i++)Art.Box("Rail post",a+dir*(i/(float)posts)+Vector3.up*.5f,new Vector3(.07f,1f,.07f),Art.Gold,false);
                var top=Along(.06f,.09f);top.x+=alongZ?0:.09f;top.z+=alongZ?.09f:0;
                Art.Box("Rail",mid+Vector3.up*1.02f,top,Art.Gold,false);
                Art.Box("Lower rail",mid+Vector3.up*.5f,Along(.035f,.04f),Art.Gold,false);
                // An unseen wall above the rail keeps the player, and anything thrown, on the reading floor.
                var edge=new GameObject("Edge of the reading floor").transform;edge.SetParent(Art.Root,false);edge.localPosition=mid+Vector3.up*3;
                edge.gameObject.AddComponent<BoxCollider>().size=Along(6,.2f);
            }
        }

        // ----- The stacks: rows of shelving on both sides, ahead and behind, catwalks and bridges between them, and lamps -----
        static void FarStacks(ArchiveRoom room)
        {
            var group=Art.Group("Far stacks",Vector3.zero);room.scenery=group;
            var rnd=new System.Random(404);float Rand(float a,float b)=>a+(float)rnd.NextDouble()*(b-a);
            var regions=new List<(string name,Stacks s)>();
            // Both sides: rows that run away from the reading floor, so you look down the aisles between them. Cross aisles break each row in three.
            for(int side=-1;side<=1;side+=2)
            {
                var s=new Stacks();
                for(int k=0;k<18;k++)
                {
                    float z=-12+5.2f*k,height=Rand(34,54);bool near=z<50;
                    ShelfRun(s,rnd,true,side*11.5f,side*27f,z,height,near,true);
                    ShelfRun(s,rnd,true,side*30.5f,side*44f,z,height+Rand(-6,6),false,true);
                    ShelfRun(s,rnd,true,side*47.5f,side*62f,z,height+Rand(-6,6),false,false);
                    // A library ladder against the first rows, for a sense of scale.
                    if(near&&k%2==1)Ladder(s,side*Rand(14,24),z-.95f,Rand(6,9));
                }
                // Catwalks down the cross aisles, high up, with lamps along them.
                Catwalk(s,side*28.75f,8.5f,-13,80);Catwalk(s,side*28.75f,17f,-13,80);Catwalk(s,side*45.75f,12.5f,-13,80);
                regions.Add((side<0?"stacks on the left":"stacks on the right",s));
            }
            // Ahead, past the end of the reading floor, and behind the arrival point: rows that run straight on, so you look down them into the haze.
            foreach(var (name,from,dir) in new[]{("stacks ahead",51f,1f),("stacks behind",-8f,-1f)})
            {
                var s=new Stacks();
                for(int i=-2;i<=2;i++)
                {
                    float x=i*4.6f,height=Rand(34,54);
                    ShelfRun(s,rnd,false,from,from+dir*14,x,height,true,true);
                    ShelfRun(s,rnd,false,from+dir*16.5f,from+dir*30,x,height+Rand(-6,6),false,true);
                    ShelfRun(s,rnd,false,from+dir*32.5f,from+dir*46,x,height+Rand(-6,6),false,false);
                }
                // A bridge across each cross aisle.
                Bridge(s,from+dir*15.25f,10.5f,13);
                regions.Add((name,s));
            }
            // Over the reading floor, high above the beams: bridges from one side's stacks to the other, with lamps hanging from them.
            var above=new Stacks();float[] heights={12,19.5f,14.5f,23};
            for(int i=0;i<4;i++)Bridge(above,10+i*10.2f,heights[i],11.5f,true);
            regions.Add(("bridges overhead",above));
            int total=0;foreach(var (name,s) in regions){s.Build(group,name);total+=s.Triangles;}
            Debug.Log("After Hours: the infinite archive's far stacks are "+total+" triangles in "+regions.Count+" meshes.");

            // One run of shelving: from 'from' to 'to' along its length (x when alongX, otherwise z), centred on 'across' on the other axis.
            // Near runs have books on their lower shelves; far ones only bands of shadowed books, as that is all the haze leaves of them.
            void ShelfRun(Stacks s,System.Random r,bool alongX,float from,float to,float across,float height,bool books,bool lamps)
            {
                Vector3 P(float l,float y,float a)=>alongX?new Vector3(l,y,a):new Vector3(a,y,l);
                float lo=Mathf.Min(from,to),hi=Mathf.Max(from,to);const float half=.75f;
                s.Box(P(lo,0,across-half),P(hi,height,across+half),StackBody);
                // Brass ends: the edge you see first, row after row, all the way into the haze.
                foreach(float end in new[]{lo,hi})s.Box(P(end-.07f,0,across-half-.06f),P(end+.07f,height,across+half+.06f),StackBrass);
                float step=books?1.7f:3.4f,boards=Mathf.Min(height,books?30:34);
                for(int face=-1;face<=1;face+=2)
                {
                    float f=across+face*half;
                    for(float y=.25f;y<boards;y+=step)
                    {
                        if(books)s.Box(P(lo,y,f),P(hi,y+.07f,f+face*.14f),StackBoard);
                        if(books&&y<9)
                        {
                            // Books in short runs of one colour, some leaning taller than others, with the odd gap where a box was taken out.
                            for(float l=lo+.1f;l<hi-.1f;)
                            {
                                float len=Mathf.Min((float)(.8+r.NextDouble()*1.8),hi-.1f-l);
                                if(r.NextDouble()>.12)
                                {
                                    var col=Spines[r.Next(Spines.Length)]*(float)(.8+r.NextDouble()*.25);
                                    s.Box(P(l,y+.07f,f),P(l+len,y+.07f+(float)(.95+r.NextDouble()*.45),f+face*.09f),col);
                                }
                                l+=len+.03f;
                            }
                        }
                        else s.Box(P(lo+.1f,y+.07f,f),P(hi-.1f,y+1.3f,f+face*.05f),StackBand);
                    }
                    // Uprights every three metres give the near rows a rhythm.
                    if(books)for(float l=lo+3;l<hi-.5f;l+=3)s.Box(P(l-.05f,0,f),P(l+.05f,boards,f+face*.16f),StackBoard);
                }
                if(!lamps)return;
                // A small warm lamp on the end facing the reading floor, and now and then another far up: specks of light that recede in every direction.
                float near=Mathf.Abs(from)<Mathf.Abs(to)?from:to,outward=Mathf.Sign(near-(from+to)/2);
                s.Box(P(near+outward*.07f,2.2f,across-.12f),P(near+outward*.3f,2.5f,across+.12f),StackLamp,true,true);
                if(r.NextDouble()<.55){float y=(float)(6+r.NextDouble()*22);s.Box(P(near+outward*.07f,y,across-.12f),P(near+outward*.3f,y+.3f,across+.12f),StackLamp,true,true);}
            }
            // A library ladder flat against a row's face: two rails and rungs.
            void Ladder(Stacks s,float x,float z,float height)
            {
                for(int i=-1;i<=1;i+=2)s.Box(new Vector3(x+i*.25f-.03f,0,z-.03f),new Vector3(x+i*.25f+.03f,height,z+.03f),StackBrass);
                for(float y=.35f;y<height;y+=.35f)s.Box(new Vector3(x-.25f,y-.02f,z-.02f),new Vector3(x+.25f,y+.02f,z+.02f),StackBrass);
            }
            // A catwalk along z at height y: a deck, two rails, and a lamp hanging under it now and then.
            void Catwalk(Stacks s,float x,float y,float from,float to)
            {
                s.Box(new Vector3(x-.8f,y-.18f,from),new Vector3(x+.8f,y,to),StackIron,true);
                for(int i=-1;i<=1;i+=2)s.Box(new Vector3(x+i*.8f-.03f,y+.95f,from),new Vector3(x+i*.8f+.03f,y+1.01f,to),StackIron,true);
                for(float z=from+4;z<to;z+=9)s.Box(new Vector3(x-.15f,y-.55f,z-.15f),new Vector3(x+.15f,y-.25f,z+.15f),StackLamp,true,true);
            }
            // A bridge along x at z and height y, 'reach' metres out each way, with lamps on long cords when it crosses the reading floor.
            void Bridge(Stacks s,float z,float y,float reach,bool lanterns=false)
            {
                s.Box(new Vector3(-reach,y-.2f,z-.7f),new Vector3(reach,y,z+.7f),StackIron,true);
                for(int i=-1;i<=1;i+=2)
                {
                    s.Box(new Vector3(-reach,y+.95f,z+i*.7f-.03f),new Vector3(reach,y+1.01f,z+i*.7f+.03f),StackIron,true);
                    s.Box(new Vector3(-reach,y,z+i*.7f-.02f),new Vector3(reach,y+.22f,z+i*.7f+.02f),StackIron,true);
                }
                if(!lanterns)return;
                for(int i=-1;i<=1;i++)
                {
                    float x=i*4.2f,drop=Rand(3,6);
                    s.Box(new Vector3(x-.015f,y-.2f-drop,z-.015f),new Vector3(x+.015f,y-.2f,z+.015f),StackIron);
                    s.Box(new Vector3(x-.22f,y-.2f-drop-.32f,z-.22f),new Vector3(x+.22f,y-.2f-drop,z+.22f),StackLamp,true,true);
                }
            }
        }

        // ----- Loose pages that drift down through the aisles and just past the rail -----
        static void DriftingPages(ArchiveRoom room)
        {
            var group=Art.Group("Drifting pages",Vector3.zero);room.moving=group;
            var rnd=new System.Random(44);float Rand(float a,float b)=>a+(float)rnd.NextDouble()*(b-a);
            var pages=new List<Transform>();var speeds=new List<float>();
            for(int i=0;i<18;i++)
            {
                int side=i%2==0?-1:1;Vector3 p;
                // Most fall in the aisles between the side rows; a few fall just past the rail, close enough to see them turn.
                if(i<14)p=new Vector3(side*Rand(12.5f,26),Rand(1,ArchiveRoom.PageTop),-12+5.2f*rnd.Next(1,10)+2.6f);
                else p=new Vector3(side*Rand(10.2f,10.9f),Rand(1,ArchiveRoom.PageTop),Rand(8,38));
                var page=Art.Box("Drifting page",p,new Vector3(.21f,.004f,.297f),Art.Paper,false,group,.002f);
                page.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
                pages.Add(page.transform);speeds.Add(Rand(.35f,.6f));
            }
            room.pages=pages.ToArray();room.speeds=speeds.ToArray();
        }
    }
}
