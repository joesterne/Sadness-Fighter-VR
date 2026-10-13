using UnityEngine;
using UnityEngine.Rendering;
namespace AfterHours
{
    // The sea moves. Four swells cross the water, rolling in from behind the boat toward the shore: a main swell at a steady
    // eight-second beat, a longer one underneath, a cross swell and a little chop. The faceted water rises and falls with them,
    // and every facet is lit afresh as it tilts, so the light slides across the sea.
    //
    // The main swell keeps the beat of the "Ambience - Waves" sound: a crest passes the boat 3.3 seconds into every wave, just
    // as the sound crests and hisses, and SeaSounds keeps the two in step. The other swells have their own beats, which is what
    // makes the sea look alive instead of ticking over.
    //
    // The player never moves with the water (a boat that heaves is a fast way to feel unwell in a headset), and no wave climbs
    // above the deck. The mesh is rebuilt thirty times a second on the CPU. That is about 1,500 height samples and 2,800
    // triangles, a small cost beside what the GPU already draws, and it leaves the scene's lit water material, its day and
    // night looks and its flat facets exactly as they were. It runs only while the ocean room is on.
    public class OceanWaves : MonoBehaviour
    {
        // The water is a grid of 32 by 44 squares, each two metres across, split into two flat triangles.
        public const int CellsX=32,CellsZ=44;
        public const float Cell=2f,OriginX=-32f,OriginZ=-16f;
        // The beat of the main swell, and when into each beat its crest passes the boat. These match Tools/make_audio.py.
        public const float Period=8f,Crest=3.3f;
        // The time shown in the editor's pictures and saved in the scene: a swell on its way up, never a flat sea.
        public const float PreviewTime=2.2f;
        // The water meets the boat at about this height, and never rises above it. The deck's top is at 0.245.
        public const float Ceiling=.2f;

        public SeaSounds sea;
        // The ocean room's root. Waves are laid out in the room's own space, so they keep their place as the shore moves toward the boat.
        public Transform room;
        public float updatesPerSecond=30;
        // For tests: how many times the water has been rebuilt, the mesh being drawn, and the highest and lowest point of the sea just now.
        public int updates{get;private set;}
        public Mesh live{get;private set;}
        public float highest{get;private set;}
        public float lowest{get;private set;}

        struct Swell{public float kx,kz,omega,phase,amp;}
        static readonly Swell[] swells=
        {
            // length (m), direction (x, z), beat (s), starting phase, height (m)
            Make(24f,.12f,1f,Period,Mathf.PI/2+2*Mathf.PI*Crest/Period,.11f),
            Make(36f,-.30f,.95f,11f,1.1f,.05f),
            Make(15f,.55f,.835f,6.1f,2.6f,.045f),
            Make(8f,-.6f,.8f,4.7f,.4f,.025f),
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
        static void Fill(float t,Vector3 offset,float[] grid,Vector3[] verts,Vector3[] normals,out float high,out float low)
        {
            int cols=CellsX+1,rows=CellsZ+1;var quiet=Base();high=float.MinValue;low=float.MaxValue;
            for(int iz=0;iz<rows;iz++)for(int ix=0;ix<cols;ix++)
            {
                float h=quiet[iz*cols+ix]+Height(OriginX+ix*Cell+offset.x,OriginZ+iz*Cell+offset.z,t);
                grid[iz*cols+ix]=h;if(h>high)high=h;if(h<low)low=h;
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

        // The mesh as the builder saves it, with the sea caught at one moment.
        public static Mesh CreateMesh(float t)
        {
            var verts=new Vector3[VertexCount];var normals=new Vector3[VertexCount];var grid=new float[(CellsX+1)*(CellsZ+1)];
            Fill(t,Vector3.zero,grid,verts,normals,out _,out _);
            var uv=new Vector2[VertexCount];var tris=new int[VertexCount];
            for(int i=0;i<VertexCount;i++){uv[i]=new Vector2(verts[i].x*.04f,verts[i].z*.04f);tris[i]=i;}
            var mesh=new Mesh{name="Moving faceted ocean"};mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);
            // The waves stay inside this box, so the sea is never culled while any of it shows.
            mesh.bounds=new Bounds(new Vector3(0,-.2f,28),new Vector3(66,2,90));
            return mesh;
        }

        MeshFilter filter;float[] grid;Vector3[] verts,normals;float next;
        void Awake()
        {
            if(!Application.isPlaying)return;
            filter=GetComponent<MeshFilter>();
            // The game moves a mesh of its own, built here, so the one saved in the scene stays as it was built and never has to be readable.
            live=CreateMesh(PreviewTime);live.name="Moving faceted ocean (live)";live.MarkDynamic();filter.sharedMesh=live;
            grid=new float[(CellsX+1)*(CellsZ+1)];verts=new Vector3[VertexCount];normals=new Vector3[VertexCount];
        }
        void OnEnable(){next=0;}
        void OnDestroy(){if(live)Destroy(live);}
        void Update()
        {
            if(live==null||Time.time<next)return;
            next=Time.time+1f/Mathf.Max(1,updatesPerSecond);
            Apply(sea?sea.Clock:Time.time);
        }
        // Moves the water to how the sea looks at a time on the sound's clock.
        public void Apply(float t)
        {
            if(live==null)return;
            Vector3 offset=room?room.InverseTransformPoint(transform.position):transform.position;
            Fill(t,offset,grid,verts,normals,out var high,out var low);highest=high;lowest=low;
            const MeshUpdateFlags keep=MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontNotifyMeshUsers;
            live.SetVertices(verts,0,VertexCount,keep);live.SetNormals(normals,0,VertexCount,keep);
            updates++;
        }
    }
}
