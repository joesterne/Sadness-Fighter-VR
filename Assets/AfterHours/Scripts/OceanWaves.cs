using UnityEngine;
using UnityEngine.Rendering;
namespace AfterHours
{
    // The sea moves. Five swells cross the water, rolling in from behind the boat toward the shore: a main swell at a steady
    // eight-second beat, a longer one underneath, two cross swells and a little chop. Together they lift the sea nearly a metre
    // from trough to crest. The faceted water rises and falls with them, and every facet is lit afresh as it tilts, so the light
    // slides across the sea.
    //
    // The main swell keeps the beat of the "Ambience - Waves" sound: a crest passes the boat 3.3 seconds into every wave, just
    // as the sound crests and hisses, and SeaSounds keeps the two in step. A moment later it breaks on the bow in a burst of
    // spray. The other swells have their own beats, which is what makes the sea look alive instead of ticking over.
    //
    // White foam rides the water: flecks that open on the crests as they pass and close again in the troughs, and a ring of wash
    // that rises and falls against the hull. Both are one small mesh of flat triangles, moved with the water.
    //
    // The player never moves with the water (a boat that heaves is a fast way to feel unwell in a headset). The sea is pressed
    // down under the hull, as a real boat pushes the water aside, so however high the swells run they climb the boat's sides but
    // never come up through the deck. The meshes are rebuilt thirty times a second on the CPU: about 1,500 height samples, 2,800
    // water triangles and 400 foam triangles, a small cost beside what the GPU already draws. It leaves the scene's lit water
    // material, its day and night looks and its flat facets exactly as they were. It runs only while the ocean room is on.
    public class OceanWaves : MonoBehaviour
    {
        // The water is a grid of 32 by 44 squares, each two metres across, split into two flat triangles.
        public const int CellsX=32,CellsZ=44;
        public const float Cell=2f,OriginX=-32f,OriginZ=-16f;
        // The beat of the main swell, and when into each beat its crest passes the boat. These match Tools/make_audio.py.
        public const float Period=8f,Crest=3.3f;
        // The crest reaches the bow a little after it passes the middle of the boat, and throws up the spray then.
        public const float SprayDelay=.6f;
        // The time shown in the editor's pictures and saved in the scene: a swell on its way up, never a flat sea.
        public const float PreviewTime=2.2f;
        // The hull, in the room's own space: the outside of the gunwales, the stern and the prow. The deck's top is at 0.245.
        public const float HullX=1.33f,HullBack=-2.49f,HullFront=2.06f;
        // Under the hull the water is held down to this height, and it may rise this much more for every metre away from the
        // hull. That keeps every facet that reaches inside the boat below the deck, while the swells climb the outside of the hull.
        public const float HullLevel=-.18f,HullRise=.45f;

        public SeaSounds sea;
        // The ocean room's root. Waves are laid out in the room's own space, so they keep their place as the shore moves toward the boat.
        public Transform room;
        // The foam on the water (a child of the water, in the same space), and the spray thrown up at the bow.
        public MeshFilter foam;
        public ParticleSystem spray;
        public float updatesPerSecond=30;
        // For tests: how many times the water has been rebuilt, the meshes being drawn, the highest and lowest point of the sea just now,
        // the highest point of the water under the hull, how many flecks of foam show, and how many times spray has been thrown up.
        public int updates{get;private set;}
        public Mesh live{get;private set;}
        public Mesh liveFoam{get;private set;}
        public float highest{get;private set;}
        public float lowest{get;private set;}
        public float underHull{get;private set;}
        public int foamShowing{get;private set;}
        public int sprays{get;private set;}

        struct Swell{public float kx,kz,omega,phase,amp;}
        static readonly Swell[] swells=
        {
            // length (m), direction (x, z), beat (s), starting phase, height (m)
            Make(24f,.12f,1f,Period,Mathf.PI/2+2*Mathf.PI*Crest/Period,.2f),
            Make(36f,-.30f,.95f,11f,1.1f,.12f),
            Make(15f,.55f,.835f,6.1f,2.6f,.08f),
            Make(11f,-.85f,.5f,5.2f,4.2f,.05f),
            Make(8f,-.6f,.8f,4.7f,.4f,.04f),
        };
        static Swell Make(float length,float dx,float dz,float beat,float phase,float amp)
        {
            var d=new Vector2(dx,dz).normalized;float k=2*Mathf.PI/length;
            return new Swell{kx=d.x*k,kz=d.y*k,omega=2*Mathf.PI/beat,phase=phase,amp=amp};
        }
        // The height the swells add at a point of the room, at a time on the sound's clock. The point is in the room's own space.
        public static float Height(float x,float z,float t)
        {
            float h=0;
            for(int i=0;i<swells.Length;i++){var s=swells[i];h+=s.amp*Mathf.Sin(s.kx*x+s.kz*z-s.omega*t+s.phase);}
            return h;
        }
        // Just the main swell: it is the one the sound follows.
        public static float MainSwell(float x,float z,float t){var s=swells[0];return s.amp*Mathf.Sin(s.kx*x+s.kz*z-s.omega*t+s.phase);}
        // The tallest the sea can ever be, with every swell cresting together.
        public static float MostSwell{get{float h=0;foreach(var s in swells)h+=s.amp;return h;}}
        // The highest the water may be at a point of the room: low under the hull, rising away from it. The point is in the room's own space.
        public static float HullCap(float x,float z)
        {
            float dx=Mathf.Max(0,Mathf.Abs(x)-HullX),dz=Mathf.Max(0,Mathf.Max(HullBack-z,z-HullFront));
            return HullLevel+HullRise*Mathf.Sqrt(dx*dx+dz*dz);
        }

        // The sea's quiet shape, which never changes: the original irregular facets, with the same mean level as before, a little lower at the top so the swells have room to rise.
        static float[] baseGrid;
        static float[] Base()
        {
            if(baseGrid!=null)return baseGrid;
            int cols=CellsX+1,rows=CellsZ+1;baseGrid=new float[cols*rows];
            for(int iz=0;iz<rows;iz++)for(int ix=0;ix<cols;ix++)baseGrid[iz*cols+ix]=-.33f+Mathf.PerlinNoise((ix-16)*.31f+20,(iz-8)*.31f+20)*.28f;
            return baseGrid;
        }
        // Positions and flat normals for every triangle. Each square is two triangles with their own corners, so each facet is lit evenly.
        // Corners run a, c, b and then b, c, e: a is the corner nearest the origin, b one square along x, c one along z, e both.
        // grid gets the water's height at every corner and lift the height the swells alone add there (for the foam).
        static void Fill(float t,Vector3 offset,float[] grid,float[] lift,Vector3[] verts,Vector3[] normals,out float high,out float low,out float hull)
        {
            int cols=CellsX+1,rows=CellsZ+1;var quiet=Base();high=hull=float.MinValue;low=float.MaxValue;
            for(int iz=0;iz<rows;iz++)for(int ix=0;ix<cols;ix++)
            {
                float x=OriginX+ix*Cell+offset.x,z=OriginZ+iz*Cell+offset.z,s=Height(x,z,t),cap=HullCap(x,z);
                float h=Mathf.Min(quiet[iz*cols+ix]+s,cap);
                grid[iz*cols+ix]=h;lift[iz*cols+ix]=s;if(h>high)high=h;if(h<low)low=h;if(cap<=HullLevel&&h>hull)hull=h;
            }
            int v=0;
            for(int iz=0;iz<CellsZ;iz++)for(int ix=0;ix<CellsX;ix++)
            {
                float x0=OriginX+ix*Cell,x1=x0+Cell,z0=OriginZ+iz*Cell,z1=z0+Cell;
                float h00=grid[iz*cols+ix],h10=grid[iz*cols+ix+1],h01=grid[(iz+1)*cols+ix],h11=grid[(iz+1)*cols+ix+1];
                verts[v]=new Vector3(x0,h00,z0);verts[v+1]=new Vector3(x0,h01,z1);verts[v+2]=new Vector3(x1,h10,z0);
                verts[v+3]=verts[v+2];verts[v+4]=verts[v+1];verts[v+5]=new Vector3(x1,h11,z1);
                Vector3 first=new Vector3(h00-h10,Cell,h00-h01).normalized,second=new Vector3(h01-h11,Cell,h10-h11).normalized;
                normals[v]=normals[v+1]=normals[v+2]=first;normals[v+3]=normals[v+4]=normals[v+5]=second;
                v+=6;
            }
        }
        const int VertexCount=CellsX*CellsZ*6;
        // A value of a grid at any point of the water's own space, on the same flat facets that are drawn.
        static float Sample(float[] values,float x,float z)
        {
            float fx=Mathf.Clamp((x-OriginX)/Cell,0,CellsX-.001f),fz=Mathf.Clamp((z-OriginZ)/Cell,0,CellsZ-.001f);
            int ix=(int)fx,iz=(int)fz,cols=CellsX+1;float u=fx-ix,w=fz-iz;
            float h00=values[iz*cols+ix],h10=values[iz*cols+ix+1],h01=values[(iz+1)*cols+ix],h11=values[(iz+1)*cols+ix+1];
            return u+w<=1?h00+u*(h10-h00)+w*(h01-h00):h11+(1-u)*(h01-h11)+(1-w)*(h10-h11);
        }

        // The foam: whitecaps scattered over the sea (in the water's own space, so they travel with it as the boat is rowed, most of them
        // near the boat where they can be seen), and wash in a ring just outside the hull (in the room's own space, so it stays by the boat).
        // Each fleck is one flat triangle, laid on the water, that opens as the swells lift it and closes to nothing as they fall away.
        public const int Whitecaps=340,Wash=56,FoamCount=Whitecaps+Wash;
        struct Fleck{public float x,z,size,angle;}
        static Fleck[] flecks;
        static Fleck[] Flecks()
        {
            if(flecks!=null)return flecks;
            var r=new System.Random(1729);flecks=new Fleck[FoamCount];
            float R()=>(float)r.NextDouble();
            for(int i=0;i<Whitecaps;i++)
            {
                float side=R()<.5f?-1:1;
                flecks[i]=new Fleck{x=side*28f*Mathf.Pow(R(),1.5f),z=-14f+84f*Mathf.Pow(R(),1.7f),size=.28f+R()*.32f,angle=R()*Mathf.PI*2};
            }
            float around=2*(2*HullX+HullFront-HullBack);
            for(int i=0;i<Wash;i++)
            {
                // Evenly round the hull, a little way out from it.
                float d=(i+R()*.6f)/Wash*around,out_=.15f+R()*.75f,x,z;
                if(d<2*HullX){x=-HullX+d;z=HullFront+out_;}
                else if((d-=2*HullX)<HullFront-HullBack){x=HullX+out_;z=HullFront-d;}
                else if((d-=HullFront-HullBack)<2*HullX){x=HullX-d;z=HullBack-out_;}
                else{d-=2*HullX;x=-HullX-out_;z=HullBack+d;}
                flecks[Whitecaps+i]=new Fleck{x=x,z=z,size=.16f+R()*.18f,angle=R()*Mathf.PI*2};
            }
            return flecks;
        }
        static void FillFoam(Vector3 offset,float[] grid,float[] lift,Vector3[] verts,out int showing)
        {
            var all=Flecks();showing=0;
            for(int i=0;i<FoamCount;i++)
            {
                var f=all[i];bool wash=i>=Whitecaps;
                // Whitecaps live in the water's space; the wash is placed in the room's and moved into the water's.
                float x=wash?f.x-offset.x:f.x,z=wash?f.z-offset.z:f.z,s=Sample(lift,x,z);
                // A whitecap opens only near the top of a crest. The wash is there most of the time and swells as the water climbs the hull.
                float open=wash?Mathf.Clamp01((s+.08f)/.3f):Mathf.Clamp01((s-.2f)/.14f);
                int v=i*3;
                if(open<.05f){verts[v]=verts[v+1]=verts[v+2]=Vector3.zero;continue;}
                showing++;float size=f.size*open;
                // Corners go clockwise seen from above, so the fleck faces up.
                for(int k=0;k<3;k++)
                {
                    float a=f.angle-k*2.0944f,cx=x+Mathf.Cos(a)*size,cz=z+Mathf.Sin(a)*size;
                    verts[v+k]=new Vector3(cx,Sample(grid,cx,cz)+.03f,cz);
                }
            }
        }
        static Mesh NewFoam()
        {
            var mesh=new Mesh{name="Sea foam"};var verts=new Vector3[FoamCount*3];var normals=new Vector3[FoamCount*3];var tris=new int[FoamCount*3];
            for(int i=0;i<verts.Length;i++){normals[i]=Vector3.up;tris[i]=i;}
            mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetTriangles(tris,0);
            mesh.bounds=new Bounds(new Vector3(0,-.2f,28),new Vector3(66,2,90));
            return mesh;
        }

        // The mesh as the builder saves it, with the sea caught at one moment.
        public static Mesh CreateMesh(float t)
        {
            var verts=new Vector3[VertexCount];var normals=new Vector3[VertexCount];var grid=new float[(CellsX+1)*(CellsZ+1)];var lift=new float[grid.Length];
            Fill(t,Vector3.zero,grid,lift,verts,normals,out _,out _,out _);
            var uv=new Vector2[VertexCount];var tris=new int[VertexCount];
            for(int i=0;i<VertexCount;i++){uv[i]=new Vector2(verts[i].x*.04f,verts[i].z*.04f);tris[i]=i;}
            var mesh=new Mesh{name="Moving faceted ocean"};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
            // The waves stay inside this box, so the sea is never culled while any of it shows.
            mesh.bounds=new Bounds(new Vector3(0,-.2f,28),new Vector3(66,2,90));
            return mesh;
        }
        // The foam as the builder saves it, at the same moment.
        public static Mesh CreateFoam(float t)
        {
            var grid=new float[(CellsX+1)*(CellsZ+1)];var lift=new float[grid.Length];
            Fill(t,Vector3.zero,grid,lift,new Vector3[VertexCount],new Vector3[VertexCount],out _,out _,out _);
            var mesh=NewFoam();var verts=new Vector3[FoamCount*3];FillFoam(Vector3.zero,grid,lift,verts,out _);mesh.SetVertices(verts);
            return mesh;
        }

        MeshFilter filter;float[] grid,lift;Vector3[] verts,normals,foamVerts;Vector3 shift;float next,lastBeat=-1;
        // For tests: the height of the water drawn just now at a point of the room's own space, measured in the room.
        public float WaterAt(float x,float z)=>grid==null?0:Sample(grid,x-shift.x,z-shift.z)+shift.y;
        void Awake()
        {
            if(!Application.isPlaying)return;
            filter=GetComponent<MeshFilter>();
            // The game moves meshes of its own, built here, so the ones saved in the scene stay as they were built and never have to be readable.
            live=CreateMesh(PreviewTime);live.name="Moving faceted ocean (live)";live.MarkDynamic();filter.sharedMesh=live;
            grid=new float[(CellsX+1)*(CellsZ+1)];lift=new float[grid.Length];verts=new Vector3[VertexCount];normals=new Vector3[VertexCount];
            if(foam){liveFoam=NewFoam();liveFoam.name="Sea foam (live)";liveFoam.MarkDynamic();foam.sharedMesh=liveFoam;foamVerts=new Vector3[FoamCount*3];}
        }
        void OnEnable(){next=0;lastBeat=-1;}
        void OnDestroy(){if(live)Destroy(live);if(liveFoam)Destroy(liveFoam);}
        void Update()
        {
            if(live==null)return;
            float clock=sea?sea.Clock:Time.time;
            // Each time the main swell's crest reaches the bow, it breaks into spray. (The clock only ever leans a little backward, so a big step back is the loop coming round.)
            float beat=Mathf.Repeat(clock-Crest-SprayDelay,Period);
            if(lastBeat>=0&&beat<lastBeat-Period*.5f&&spray){spray.Play(true);sprays++;}
            lastBeat=beat;
            if(Time.time<next)return;
            next=Time.time+1f/Mathf.Max(1,updatesPerSecond);
            Apply(clock);
        }
        // Moves the water and its foam to how the sea looks at a time on the sound's clock.
        public void Apply(float t)
        {
            if(live==null)return;
            Vector3 offset=room?room.InverseTransformPoint(transform.position):transform.position;shift=offset;
            Fill(t,offset,grid,lift,verts,normals,out var high,out var low,out var hull);highest=high;lowest=low;underHull=hull;
            const MeshUpdateFlags keep=MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontNotifyMeshUsers;
            live.SetVertices(verts,0,VertexCount,keep);live.SetNormals(normals,0,VertexCount,keep);
            if(liveFoam){FillFoam(offset,grid,lift,foamVerts,out var showing);foamShowing=showing;liveFoam.SetVertices(foamVerts,0,foamVerts.Length,keep);}
            updates++;
        }
    }
}
