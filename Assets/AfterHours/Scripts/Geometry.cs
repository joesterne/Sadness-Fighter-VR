using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace AfterHours
{
    // Refined low-poly primitives. Every facet is flat shaded. Boxes and cylinders get a small bevel that stays the same
    // size in metres however the object is scaled, so edges catch the light evenly on a keyboard key and on a wall.
    public static class Geometry
    {
        public const float Bevel=.025f;
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();

        public static Mesh Get(PrimitiveType type)=>Get(type,Vector3.one);
        public static Mesh Get(PrimitiveType type,Vector3 worldScale,float bevel=Bevel)
        {
            var s=new Vector3(Mathf.Max(Mathf.Abs(worldScale.x),1e-4f),Mathf.Max(Mathf.Abs(worldScale.y),1e-4f),Mathf.Max(Mathf.Abs(worldScale.z),1e-4f));
            switch(type)
            {
                case PrimitiveType.Cube:
                {
                    float b=Mathf.Min(bevel,.18f*Mathf.Min(s.x,Mathf.Min(s.y,s.z)));
                    return Cached("bevel box",new Vector3(b/s.x,b/s.y,b/s.z),BevelBox);
                }
                case PrimitiveType.Cylinder:
                {
                    // Unity's cylinder is 1 unit wide and 2 units tall.
                    float b=Mathf.Min(bevel,.18f*Mathf.Min(2*s.y,Mathf.Min(s.x,s.z)));
                    int sides=Mathf.Max(s.x,s.z)>1.5f?16:12;
                    return Cached("faceted cylinder",new Vector3(b/Mathf.Max(s.x,s.z),b/s.y,sides),Cylinder);
                }
                case PrimitiveType.Capsule:
                    return Cached("faceted capsule",Vector3.zero,_=>Capsule());
                default:
                {
                    int detail=Mathf.Max(s.x,Mathf.Max(s.y,s.z))>2?2:1;
                    return Cached("icosphere",new Vector3(detail,0,0),k=>Icosphere((int)k.x));
                }
            }
        }

        static Mesh Cached(string kind,Vector3 k,Func<Vector3,Mesh> build)
        {
            k=new Vector3(Q(k.x),Q(k.y),Q(k.z));
            string key=kind+" "+k.x.ToString("0.####",CultureInfo.InvariantCulture)+" "+k.y.ToString("0.####",CultureInfo.InvariantCulture)+" "+k.z.ToString("0.####",CultureInfo.InvariantCulture);
            if(cache.TryGetValue(key,out var found)&&found)return found;
            var mesh=build(k);mesh.name="After Hours "+key;cache[key]=mesh;return mesh;
        }
        static float Q(float v)=>Mathf.Round(v*10000)/10000;

        // Unit box from -0.5 to 0.5. c holds the bevel width on each axis in unit space.
        static Mesh BevelBox(Vector3 c)
        {
            var m=new Builder();var h=new Vector3(.5f,.5f,.5f);var inner=h-c;
            // The point on the face of axis a at the corner with signs s.
            Vector3 P(int a,Vector3 s){var p=Vector3.Scale(inner,s);p[a]=h[a]*s[a];return p;}
            for(int a=0;a<3;a++)for(int sa=-1;sa<=1;sa+=2)
            {
                int u=(a+1)%3,w=(a+2)%3;
                Vector3 S(int su,int sw){var s=Vector3.zero;s[a]=sa;s[u]=su;s[w]=sw;return s;}
                m.Poly(P(a,S(-1,-1)),P(a,S(1,-1)),P(a,S(1,1)),P(a,S(-1,1)));
                // The bevel strip shared with the neighbouring face on axis u. Each edge is added once.
                for(int su=-1;su<=1;su+=2)m.Poly(P(a,S(su,-1)),P(a,S(su,1)),P(u,S(su,1)),P(u,S(su,-1)));
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var s=new Vector3(x,y,z);m.Poly(P(0,s),P(1,s),P(2,s));}
            return m.Mesh();
        }

        // k.x: radial bevel, k.y: vertical bevel (unit space), k.z: number of sides.
        static Mesh Cylinder(Vector3 k)
        {
            var m=new Builder();int n=(int)k.z;float r=.5f,ri=.5f-k.x,yi=1-k.y;
            Vector3 R(float radius,float y,int i){float a=(i+.5f)*2*Mathf.PI/n;return new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);}
            var top=new Vector3[n];var bottom=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                m.Poly(R(r,-yi,i),R(r,-yi,i+1),R(r,yi,i+1),R(r,yi,i));
                m.Poly(R(r,yi,i),R(r,yi,i+1),R(ri,1,i+1),R(ri,1,i));
                m.Poly(R(r,-yi,i),R(r,-yi,i+1),R(ri,-1,i+1),R(ri,-1,i));
                top[i]=R(ri,1,i);bottom[i]=R(ri,-1,i);
            }
            m.Poly(top);m.Poly(bottom);
            return m.Mesh();
        }

        // Unity's capsule: radius 0.5, 2 units tall.
        static Mesh Capsule()
        {
            var m=new Builder();const int n=10,rings=3;var rows=new List<Vector2>();
            for(int i=0;i<=rings;i++){float a=i*Mathf.PI*.5f/rings;rows.Add(new Vector2(Mathf.Sin(a)*.5f,Mathf.Cos(a)*.5f+.5f));}
            for(int i=rings;i>=0;i--){float a=i*Mathf.PI*.5f/rings;rows.Add(new Vector2(Mathf.Sin(a)*.5f,-Mathf.Cos(a)*.5f-.5f));}
            Vector3 R(Vector2 row,int i){float a=(i+.5f)*2*Mathf.PI/n;return new Vector3(Mathf.Cos(a)*row.x,row.y,Mathf.Sin(a)*row.x);}
            for(int j=0;j+1<rows.Count;j++)for(int i=0;i<n;i++)
            {
                Vector3 a=R(rows[j],i),b=R(rows[j],i+1),c=R(rows[j+1],i+1),d=R(rows[j+1],i);
                if(rows[j].x<1e-5f)m.Poly(a,c,d);else if(rows[j+1].x<1e-5f)m.Poly(a,b,c);else m.Poly(a,b,c,d);
            }
            return m.Mesh();
        }

        // Diameter 1. Detail 1 has 80 facets, detail 2 has 320.
        static Mesh Icosphere(int detail)
        {
            float t=(1+Mathf.Sqrt(5))/2;
            var p=new List<Vector3>{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            for(int i=0;i<p.Count;i++)p[i]=p[i].normalized;
            var faces=new List<int[]>{new[]{0,11,5},new[]{0,5,1},new[]{0,1,7},new[]{0,7,10},new[]{0,10,11},new[]{1,5,9},new[]{5,11,4},new[]{11,10,2},new[]{10,7,6},new[]{7,1,8},new[]{3,9,4},new[]{3,4,2},new[]{3,2,6},new[]{3,6,8},new[]{3,8,9},new[]{4,9,5},new[]{2,4,11},new[]{6,2,10},new[]{8,6,7},new[]{9,8,1}};
            for(int level=0;level<detail;level++)
            {
                var middle=new Dictionary<long,int>();var next=new List<int[]>();
                int Mid(int a,int b){long key=Math.Min(a,b)*100000L+Math.Max(a,b);if(middle.TryGetValue(key,out int i))return i;p.Add(((p[a]+p[b])*.5f).normalized);middle[key]=p.Count-1;return p.Count-1;}
                foreach(var f in faces){int ab=Mid(f[0],f[1]),bc=Mid(f[1],f[2]),ca=Mid(f[2],f[0]);next.Add(new[]{f[0],ab,ca});next.Add(new[]{f[1],bc,ab});next.Add(new[]{f[2],ca,bc});next.Add(new[]{ab,bc,ca});}
                faces=next;
            }
            var m=new Builder();foreach(var f in faces)m.Poly(p[f[0]]*.5f,p[f[1]]*.5f,p[f[2]]*.5f);
            return m.Mesh();
        }

        class Builder
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
            // Adds a convex polygon as its own flat-shaded facet, wound to face away from the centre of the shape.
            public void Poly(params Vector3[] p)
            {
                var centre=Vector3.zero;foreach(var q in p)centre+=q;centre/=p.Length;
                var n=Vector3.zero;for(int i=1;i+1<p.Length;i++)n+=Vector3.Cross(p[i]-p[0],p[i+1]-p[0]);
                if(n.sqrMagnitude<1e-14f)return;
                bool flip=Vector3.Dot(n,centre)<0;var axis=new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z));int b=v.Count;
                foreach(var q in p){v.Add(q);uv.Add(axis.x>=axis.y&&axis.x>=axis.z?new Vector2(q.z,q.y):axis.y>=axis.z?new Vector2(q.x,q.z):new Vector2(q.x,q.y));}
                for(int i=1;i+1<p.Length;i++){if(flip){t.Add(b);t.Add(b+i+1);t.Add(b+i);}else{t.Add(b);t.Add(b+i);t.Add(b+i+1);}}
            }
            public Mesh Mesh()
            {
                var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
