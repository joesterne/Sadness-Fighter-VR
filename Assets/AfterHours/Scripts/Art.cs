using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    public static class Art
    {
        public static Material Ink, Navy, Teal, Mint, Coral, Gold, Cream, White, Wood, Glass, Metal, Paper, Water;
        // The avatar's skin tones and hair colours, in wardrobe order.
        public static Material[] Skins, Hairs;
        public static TMP_FontAsset Font;
        public static Transform Root;

        public static GameObject Box(string name, Vector3 p, Vector3 s, Material mat, bool solid = true, Transform parent = null, float bevel = Geometry.Bevel)
        { return Shape(name, PrimitiveType.Cube, p, s, mat, solid, parent, bevel); }
        public static GameObject Shape(string name, PrimitiveType type, Vector3 p, Vector3 s, Material mat, bool solid = true, Transform parent = null, float bevel = Geometry.Bevel)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent ? parent : Root, false);
            go.transform.localPosition = p; go.transform.localScale = s;
            go.GetComponent<MeshFilter>().sharedMesh=Geometry.Get(type,go.transform.lossyScale,bevel); go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        // A large surface drawn as bevelled tiles or panels. The returned object keeps one plain collider for walking,
        // teleporting and physics; tile counts round to the nearest whole number along each axis (0 = one piece).
        // Shadows come from one unbroken, invisible slab: shadow bias shrinks each tile slightly, so tiles casting their own
        // shadows would leak light through every seam. Floors pass castShadows false and cast none.
        public static GameObject Tiled(string name, Vector3 p, Vector3 size, Material mat, Vector3 tile, bool solid = true, Transform parent = null, float bevel = .035f, bool castShadows = true)
        {
            // The shadow slab stays square-edged so walls and ceiling meet without a light leak along the join.
            var body = Box(name, p, size, mat, solid, parent, 0f);
            if (castShadows) body.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            else { Object.DestroyImmediate(body.GetComponent<MeshRenderer>()); Object.DestroyImmediate(body.GetComponent<MeshFilter>()); }
            int nx = Count(size.x, tile.x), ny = Count(size.y, tile.y), nz = Count(size.z, tile.z);
            var piece = new Vector3(size.x / nx, size.y / ny, size.z / nz);
            for (int x = 0; x < nx; x++) for (int y = 0; y < ny; y++) for (int z = 0; z < nz; z++)
            {
                var panel = Box(name + " panel", p + new Vector3((x + .5f) * piece.x - size.x * .5f, (y + .5f) * piece.y - size.y * .5f, (z + .5f) * piece.z - size.z * .5f), piece, mat, false, parent, bevel);
                panel.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return body;
        }
        static int Count(float length, float target) => target <= 0 ? 1 : Mathf.Max(1, Mathf.RoundToInt(length / target));
        public static Transform Group(string name, Vector3 p, Transform parent = null)
        {
            var t = new GameObject(name).transform; t.SetParent(parent ? parent : Root, false); t.localPosition = p; return t;
        }
        public static TextMeshPro Text(string name, string value, Vector3 p, float size, Color color, float width = 6, Transform parent = null)
        {
            var t = new GameObject(name).AddComponent<TextMeshPro>(); t.transform.SetParent(parent ? parent : Root, false);
            t.transform.localPosition = p; t.font = Font; t.text = value; t.fontSize = size * 7; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(width, 3); t.richText = true;
            return t;
        }
        public static void Line(string name, Vector3 a, Vector3 b, float width, Material mat, Transform parent = null)
        {
            var go = Box(name, (a + b) / 2, new Vector3(width,width,Vector3.Distance(a,b)), mat, false, parent);
            go.transform.localRotation = Quaternion.LookRotation(b-a);
        }
        public static GameObject Facet(string name, Vector3 p, Vector3 s, Material mat, int segments = 7, Transform parent = null)
        {
            var vs = new List<Vector3>(); var ts = new List<int>();
            Vector3 top = new Vector3(0,.6f,0), bottom = new Vector3(0,-.4f,0);
            for(int i=0;i<segments;i++)
            {
                float a = i*Mathf.PI*2/segments, b=(i+1)*Mathf.PI*2/segments;
                Vector3 u=new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f), v=new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f);
                int n=vs.Count; vs.AddRange(new[]{top,v,u,bottom,u,v}); for(int j=0;j<6;j++)ts.Add(n+j);
            }
            var mesh=new Mesh { name=name+" "+segments+" mesh" }; mesh.SetVertices(vs); mesh.SetTriangles(ts,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<Renderer>().sharedMaterial=mat; go.transform.SetParent(parent?parent:Root,false);go.transform.localPosition=p;go.transform.localScale=s;return go;
        }
        public static void Plant(Vector3 p, bool polished = false)
        {
            var t=Group("Plant",p);
            Shape("Planter",PrimitiveType.Cylinder,new Vector3(0,.3f,0),new Vector3(.55f,.3f,.55f),polished?White:Coral,true,t);
            for(int i=0;i<5;i++) { float a=i*1.25f; var leaf=polished?Shape("Smooth leaf",PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*.24f,.95f+i*.11f,Mathf.Cos(a)*.24f),new Vector3(.35f,.75f,.18f),Teal,false,t):Facet("Paper leaf",new Vector3(Mathf.Sin(a)*.24f,.95f+i*.11f,Mathf.Cos(a)*.24f),new Vector3(.55f,.8f,.35f),Teal,5,t); leaf.transform.localEulerAngles=new Vector3(20,i*72,20); }
        }
        public static void Person(Vector3 p, Transform parent)
        {
            var t=Group("Watching silhouette",p,parent);
            Facet("Faceted coat",new Vector3(0,.9f,0),new Vector3(.7f,1.15f,.42f),Navy,5,t);
            Facet("Head",new Vector3(0,1.65f,0),Vector3.one*.43f,Cream,6,t);
            Box("Left leg",new Vector3(-.15f,.32f,0),new Vector3(.17f,.65f,.19f),Ink,false,t);
            Box("Right leg",new Vector3(.15f,.32f,0),new Vector3(.17f,.65f,.19f),Ink,false,t);
            for(int k=-1;k<=1;k+=2) Shape("Eye",PrimitiveType.Sphere,new Vector3(k*.075f,1.68f,-.195f),Vector3.one*.04f,Gold,false,t);
        }
        public static GameObject Button(string name, string label, Vector3 p, Vector3 size, Material color, ActionKind kind, GameDirector director, int value=0, Transform parent=null)
        {
            var go=Box(name,p,size,color,true,parent);var a=go.AddComponent<Interactable>();a.kind=kind;a.director=director;a.value=value;a.prompt=label;
            var labelText=Text(name+" label",label,p+new Vector3(0,0,-size.z*.5f-.015f),.20f,Color.white,size.x*.92f,parent); labelText.rectTransform.sizeDelta=new Vector2(size.x*.9f,size.y*.8f);labelText.enableAutoSizing=true;labelText.fontSizeMin=.5f;labelText.fontSizeMax=1.6f;
            return go;
        }
    }
}



