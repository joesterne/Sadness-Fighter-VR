using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace AfterHours
{
    // The last room, "Room for tomorrow": a rooftop at sunrise, in a bright, chunky, toy-like style. A deck of warm boards with a rug and benches,
    // deck chairs facing the sun, big round plants, bunting overhead, a stairwell hut and a water tower, a skyline of pastel blocks, and clouds and a
    // balloon in a gradient sky (the sky, the light and the haze come from RooftopRoom). The closing words stay on their navy board, exactly as they were.
    // The skyline, windows and clouds use materials of their own, so far scenery never shares a mesh with the near props.
    public static partial class WorldBuilder
    {
        // Pieces of scenery gathered by material and merged into one mesh per material.
        sealed class Scenery
        {
            readonly Dictionary<Material,List<CombineInstance>> by=new Dictionary<Material,List<CombineInstance>>();
            public void Add(Mesh mesh,Vector3 at,Vector3 size,Vector3 euler,Material material)
            {
                if(!by.TryGetValue(material,out var list)){list=new List<CombineInstance>();by[material]=list;}
                list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(at,Quaternion.Euler(euler),size)});
            }
            public void Cube(Vector3 at,Vector3 size,Material material,Vector3 euler=default,float bevel=.03f)=>Add(Geometry.Get(PrimitiveType.Cube,size,bevel),at,size,euler,material);
            public void Ball(Vector3 at,Vector3 size,Material material,Vector3 euler=default)=>Add(Geometry.Get(PrimitiveType.Sphere),at,size,euler,material);
            // A cylinder by its width and height (a Unity cylinder is two units tall at scale one).
            public void Tube(Vector3 at,float diameter,float height,Material material,Vector3 euler=default,float bevel=.02f)
            {
                var size=new Vector3(diameter,height*.5f,diameter);Add(Geometry.Get(PrimitiveType.Cylinder,size,bevel),at,size,euler,material);
            }
            public void Build(Transform parent,string name,ShadowCastingMode shadows)
            {
                foreach(var pair in by)
                {
                    string label="Rooftop "+name+" - "+pair.Key.name;
                    var mesh=new Mesh{name=label,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                    var go=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=shadows;renderer.receiveShadows=shadows!=ShadowCastingMode.Off;
                }
            }
        }
        // A flag for the bunting: a triangle hanging from its top edge, drawn on both sides. It faces along z; turn it for a string that runs along z.
        static Mesh pennantMesh,windowMesh;
        static Mesh Pennant()
        {
            if(pennantMesh)return pennantMesh;
            var mesh=new Mesh{name="Rooftop pennant"};
            mesh.SetVertices(new[]{new Vector3(-.5f,0,0),new Vector3(.5f,0,0),new Vector3(0,-1,0),new Vector3(-.5f,0,0),new Vector3(0,-1,0),new Vector3(.5f,0,0)});
            mesh.SetUVs(0,new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,0),new Vector2(0,1),new Vector2(.5f,0),new Vector2(1,1)});
            mesh.SetTriangles(new[]{0,1,2,3,4,5},0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return pennantMesh=mesh;
        }
        // A cone for a roof: a 12-sided point one unit tall on a base one unit wide.
        static Mesh coneMesh;
        static Mesh Cone()
        {
            if(coneMesh)return coneMesh;
            const int sides=12;var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;int n=vs.Count;
                vs.Add(new Vector3(0,1,0));vs.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));vs.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                uv.Add(new Vector2(.5f,1));uv.Add(new Vector2(1,0));uv.Add(new Vector2(0,0));ts.Add(n);ts.Add(n+1);ts.Add(n+2);
            }
            var mesh=new Mesh{name="Rooftop cone"};mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(ts,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return coneMesh=mesh;
        }
        // A window pane: one square facing -z.
        static Mesh WindowQuad()
        {
            if(windowMesh)return windowMesh;
            var mesh=new Mesh{name="Rooftop window"};
            mesh.SetVertices(new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)});
            mesh.SetUVs(0,new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(1,1)});
            mesh.SetTriangles(new[]{0,2,1,2,3,1},0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return windowMesh=mesh;
        }

        static void Rooftop()
        {
            var room=d.rooms[RooftopRoom.Room].root.AddComponent<RooftopRoom>();room.director=d;d.rooftop=room;
            Sound("Ambience - Rooftop",.55f);
            RooftopDeck();RooftopBoard();RooftopBunting();RooftopFurniture();RooftopHutAndTower();RooftopSkyline();RooftopSky(room);
        }

        // ----- The deck: warm boards, chunky cream parapets with sky-blue caps on all four sides, and a bright rug -----
        static void RooftopDeck()
        {
            Floor(new Vector3(0,-.15f,3),new Vector3(17,.3f,19),Art.Deck);
            for(int s=-1;s<=1;s+=2)
            {
                Art.Box("Rooftop parapet",new Vector3(s*8.5f,.6f,3),new Vector3(.3f,1.2f,19),Art.ToyCream,true,null,.07f);
                Art.Box("Rooftop parapet cap",new Vector3(s*8.5f,1.28f,3),new Vector3(.54f,.16f,19.4f),Art.ToySky,false,null,.06f);
            }
            Art.Box("Rooftop edge",new Vector3(0,.6f,12.5f),new Vector3(17,1.2f,.3f),Art.ToyCream,true,null,.07f);
            Art.Box("Rooftop edge cap",new Vector3(0,1.28f,12.5f),new Vector3(17.4f,.16f,.54f),Art.ToySky,false,null,.06f);
            Art.Box("Rooftop back wall",new Vector3(0,.6f,-6.5f),new Vector3(17,1.2f,.3f),Art.ToyCream,true,null,.07f);
            Art.Box("Rooftop back wall cap",new Vector3(0,1.28f,-6.5f),new Vector3(17.4f,.16f,.54f),Art.ToySky,false,null,.06f);
            // The rug sits flat on the boards (under 5 cm, so it never counts as something standing in the way).
            Art.Box("Rug",new Vector3(0,.015f,1.2f),new Vector3(6.6f,.03f,3.8f),Art.ToyTomato,false,null,.012f);
            Art.Box("Rug band",new Vector3(0,.018f,1.2f),new Vector3(6f,.03f,3.2f),Art.ToyCream,false,null,.012f);
            Art.Box("Rug centre",new Vector3(0,.021f,1.2f),new Vector3(5.2f,.03f,2.4f),Art.ToySun,false,null,.012f);
            for(int s=-1;s<=1;s+=2)Art.Box("Rug stripe",new Vector3(0,.024f,1.2f+s*.85f),new Vector3(5.2f,.03f,.16f),Art.ToySky,false,null,.01f);
        }

        // ----- The board with the closing words -----
        static void RooftopBoard()
        {
            // The words stand on a deep-blue board in ivory, readable from anywhere on the roof against any sky. Only the frame around it is new.
            var board=Art.Group("Rooftop message board",new Vector3(0,0,9.15f));
            Art.Box("Board face",new Vector3(0,2.75f,0),new Vector3(7.6f,3.4f,.12f),Art.Navy,false,board);
            Art.Box("Board trim",new Vector3(0,4.5f,-.02f),new Vector3(7.9f,.1f,.18f),Art.ToySun,false,board,.03f);Art.Box("Board trim",new Vector3(0,1f,-.02f),new Vector3(7.9f,.1f,.18f),Art.ToySun,false,board,.03f);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Box("Board side trim",new Vector3(side*3.9f,2.75f,-.02f),new Vector3(.1f,3.5f,.18f),Art.ToySun,false,board,.03f);
                Art.Box("Board leg",new Vector3(side*3.3f,.5f,.1f),new Vector3(.22f,1f,.22f),Art.ToyWoodDark,false,board,.04f);
            }
            d.ending=Art.Text("Ending","You can rest\nbefore you are ready.",new Vector3(0,3.6f,9),.78f,ivory,7);
            Art.Text("Epilogue","The office is only one room in your life.",new Vector3(0,2.35f,9),.36f,muted,7);
            Art.Text("Next step","For now: a glass of water. A message to someone. A little daylight.\nYou get to choose the next small thing.",new Vector3(0,1.55f,9),.25f,ivory,7);
            // A row of warm bulbs along the top, like a little marquee (above the words, so they never cross them).
            var bulbs=new Scenery();
            for(int k=0;k<=16;k++)bulbs.Ball(new Vector3(-4f+k*.5f,4.68f,9.06f),Vector3.one*.17f,Art.ToyBulb);
            bulbs.Build(Art.Root,"board bulbs",ShadowCastingMode.Off);
        }

        // ----- Bunting overhead: poles on the parapets, ropes between them and a flag every half metre or so -----
        static void RooftopBunting()
        {
            var bunting=new Scenery();
            Material[] colours={Art.ToyTomato,Art.ToySun,Art.ToySky,Art.ToyLeaf,Art.ToyPink,Art.ToyLilac};
            int next=0;
            float[] poleZ={-6.3f,-1f,5f,12.3f};
            const float top=5.3f;
            for(int side=-1;side<=1;side+=2)foreach(float z in poleZ)
            {
                Art.Shape("Bunting pole",PrimitiveType.Cylinder,new Vector3(side*8.5f,3.41f,z),new Vector3(.12f,2.05f,.12f),Art.ToyWoodDark,false,null,.01f);
                Art.Shape("Bunting pole knob",PrimitiveType.Sphere,new Vector3(side*8.5f,5.5f,z),Vector3.one*.28f,Art.ToySun,false);
            }
            void Rope(Vector3 a,Vector3 b,bool alongX)
            {
                float length=Vector3.Distance(a,b);int pieces=Mathf.Max(4,Mathf.RoundToInt(length/.8f));float sag=Mathf.Max(.35f,length*.045f);
                Vector3 At(float t)=>Vector3.Lerp(a,b,t)+Vector3.down*sag*4*t*(1-t);
                for(int i=0;i<pieces;i++)
                {
                    var p=At(i/(float)pieces);var q=At((i+1)/(float)pieces);
                    bunting.Cube((p+q)/2,new Vector3(.035f,.035f,Vector3.Distance(p,q)+.02f),Art.ToyWoodDark,Quaternion.LookRotation(q-p).eulerAngles,0f);
                }
                int flags=Mathf.Max(3,Mathf.RoundToInt(length/.62f));
                for(int i=0;i<flags;i++)
                {
                    var p=At((i+.5f)/flags);
                    bunting.Add(Pennant(),p+Vector3.down*.02f,new Vector3(.36f,.46f,1),new Vector3(0,alongX?0:90,0),colours[next++%colours.Length]);
                }
            }
            // Across the roof: behind the arrival point, then two more strings. All hang well above the closing words, which are read from below them.
            foreach(float z in new[]{-6.3f,-1f,5f})Rope(new Vector3(-8.5f,top,z),new Vector3(8.5f,top,z),true);
            for(int side=-1;side<=1;side+=2)for(int k=0;k+1<poleZ.Length;k++)Rope(new Vector3(side*8.5f,top,poleZ[k]),new Vector3(side*8.5f,top,poleZ[k+1]),false);
            bunting.Build(Art.Root,"bunting",ShadowCastingMode.Off);
        }

        // ----- Things to sit by: benches with backs, deck chairs facing the sun, a little table, big round plants and flower boxes -----
        static void RooftopFurniture()
        {
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*4;
                for(int k=-1;k<=1;k++)Art.Box("Bench plank",new Vector3(x,.5f,2.5f+k*.22f),new Vector3(2.3f,.12f,.2f),Art.ToyWood,true,null,.03f);
                for(int leg=-1;leg<=1;leg+=2)
                {
                    Art.Box("Bench leg",new Vector3(x+leg*.85f,.24f,2.5f),new Vector3(.12f,.48f,.62f),Art.ToySky,true,null,.03f);
                    Art.Box("Bench back post",new Vector3(x+leg*.95f,.74f,2.2f),new Vector3(.1f,.5f,.1f),Art.ToySky,true,null,.03f);
                }
                Art.Box("Bench back",new Vector3(x,.88f,2.2f),new Vector3(2.3f,.46f,.09f),Art.ToyWood,true,null,.03f);
                ToyPlant(new Vector3(side*5.5f,0,5),side<0?Art.ToyTomato:Art.ToySun,1f);
                ToyPlant(new Vector3(side*5.8f,0,7),side<0?Art.ToyPink:Art.ToyLilac,.9f);
                ToyPlant(new Vector3(side*7.5f,0,11.2f),side<0?Art.ToySky:Art.ToyTomato,1.1f);
            }
            // Two deck chairs turned toward the sun, with a table between them and a glass of water on it.
            DeckChair(new Vector3(-1.7f,0,5.2f),RooftopRoom.SunAzimuth-8,Art.ToySky);
            DeckChair(new Vector3(1.7f,0,5.2f),RooftopRoom.SunAzimuth+4,Art.ToyTomato);
            var table=Art.Group("Side table",new Vector3(0,0,5.35f));
            Art.Shape("Table top",PrimitiveType.Cylinder,new Vector3(0,.52f,0),new Vector3(.7f,.025f,.7f),Art.ToyCream,false,table,.02f);
            Art.Shape("Table stem",PrimitiveType.Cylinder,new Vector3(0,.26f,0),new Vector3(.1f,.26f,.1f),Art.ToyWoodDark,false,table,.01f);
            Art.Shape("Table foot",PrimitiveType.Cylinder,new Vector3(0,.03f,0),new Vector3(.4f,.03f,.4f),Art.ToyWoodDark,false,table,.015f);
            Art.Shape("Glass of water",PrimitiveType.Cylinder,new Vector3(.1f,.6f,0),new Vector3(.1f,.075f,.1f),Art.Glass,false,table,.01f);
            Art.Shape("Water",PrimitiveType.Cylinder,new Vector3(.1f,.59f,0),new Vector3(.085f,.05f,.085f),Art.Water,false,table,.005f);
            // Beanbags to sink into, low and soft (nothing to bump into).
            var beanbags=new[]{(new Vector3(-4.1f,0,-.4f),Art.ToyTomato,20f),(new Vector3(4f,0,-.8f),Art.ToySky,-30f),(new Vector3(-2.6f,0,-2.6f),Art.ToySun,70f),(new Vector3(2.9f,0,-2.3f),Art.ToyPink,-10f)};
            foreach(var (where,material,turn) in beanbags)
            {
                var bag=Art.Group("Beanbag",where);bag.localEulerAngles=new Vector3(0,turn,0);
                Art.Shape("Beanbag",PrimitiveType.Sphere,new Vector3(0,.27f,0),new Vector3(1f,.62f,.9f),material,false,bag);
                Art.Shape("Beanbag top",PrimitiveType.Sphere,new Vector3(.12f,.5f,0),new Vector3(.5f,.3f,.5f),material,false,bag);
            }
            // Flower boxes along the parapets.
            foreach(float z in new[]{-3.5f,1.5f,6.5f,10f})for(int side=-1;side<=1;side+=2)
            {
                var box=Art.Group("Flower box",new Vector3(side*8.5f,1.36f,z));
                Art.Box("Flower box tub",new Vector3(0,.15f,0),new Vector3(.46f,.3f,1.5f),(z>0)==(side>0)?Art.ToyTomato:Art.ToyLilac,false,box,.04f);
                for(int k=-1;k<=1;k+=2)Art.Shape("Leaf puff",PrimitiveType.Sphere,new Vector3(0,.4f,k*.42f),new Vector3(.5f,.4f,.55f),Art.ToyLeaf,false,box);
                Art.Shape("Flower",PrimitiveType.Sphere,new Vector3(.1f,.6f,.3f),Vector3.one*.17f,Art.ToyPink,false,box);
                Art.Shape("Flower",PrimitiveType.Sphere,new Vector3(-.1f,.58f,-.35f),Vector3.one*.17f,Art.ToySun,false,box);
            }
        }
        // A big round plant: a chunky pot, a short stem and a crown of leaf puffs with a few flowers.
        static void ToyPlant(Vector3 at,Material pot,float scale)
        {
            var t=Art.Group("Plant",at);float s=scale;
            Art.Shape("Planter",PrimitiveType.Cylinder,new Vector3(0,.34f*s,0),new Vector3(.66f*s,.34f*s,.66f*s),pot,true,t,.05f);
            Art.Shape("Planter rim",PrimitiveType.Cylinder,new Vector3(0,.7f*s,0),new Vector3(.8f*s,.045f*s,.8f*s),pot,false,t,.03f);
            Art.Shape("Stem",PrimitiveType.Cylinder,new Vector3(0,.95f*s,0),new Vector3(.12f*s,.3f*s,.12f*s),Art.ToyWoodDark,false,t,.01f);
            Art.Shape("Leaf puff",PrimitiveType.Sphere,new Vector3(0,1.35f*s,0),new Vector3(1f,.9f,1f)*s,Art.ToyLeaf,false,t);
            Art.Shape("Leaf puff",PrimitiveType.Sphere,new Vector3(.34f*s,1.05f*s,.1f*s),Vector3.one*.62f*s,Art.ToyLeafDark,false,t);
            Art.Shape("Leaf puff",PrimitiveType.Sphere,new Vector3(-.3f*s,1.12f*s,-.12f*s),Vector3.one*.58f*s,Art.ToyLeaf,false,t);
            Art.Shape("Flower",PrimitiveType.Sphere,new Vector3(.18f*s,1.72f*s,.2f*s),Vector3.one*.17f*s,Art.ToyPink,false,t);
            Art.Shape("Flower",PrimitiveType.Sphere,new Vector3(-.28f*s,1.55f*s,.3f*s),Vector3.one*.17f*s,Art.ToySun,false,t);
            Art.Shape("Flower",PrimitiveType.Sphere,new Vector3(.36f*s,1.5f*s,-.26f*s),Vector3.one*.17f*s,Art.ToyPink,false,t);
        }
        // A folding deck chair with a striped sling, facing +z before it is turned by yaw degrees.
        static void DeckChair(Vector3 at,float yaw,Material stripe)
        {
            var c=Art.Group("Deck chair",at);c.localEulerAngles=new Vector3(0,yaw,0);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.34f;
                Art.Box("Chair runner",new Vector3(x,.07f,.05f),new Vector3(.06f,.06f,1.1f),Art.ToyWood,false,c,.015f);
                Art.Box("Chair front leg",new Vector3(x,.3f,.5f),new Vector3(.06f,.52f,.06f),Art.ToyWood,false,c,.015f);
                var back=Art.Box("Chair back rail",new Vector3(x,.46f,-.38f),new Vector3(.06f,.96f,.06f),Art.ToyWood,false,c,.015f);back.transform.localEulerAngles=new Vector3(-38,0,0);
                var rest=Art.Box("Chair seat rail",new Vector3(x,.34f,.12f),new Vector3(.06f,.05f,.8f),Art.ToyWood,false,c,.015f);rest.transform.localEulerAngles=new Vector3(-10,0,0);
            }
            for(int k=-1;k<=1;k++)
            {
                var m=k==0?Art.ToyCream:stripe;
                var seat=Art.Box("Chair seat",new Vector3(k*.21f,.37f,.14f),new Vector3(.2f,.035f,.64f),m,false,c,.01f);seat.transform.localEulerAngles=new Vector3(-10,0,0);
                var back=Art.Box("Chair back",new Vector3(k*.21f,.48f,-.4f),new Vector3(.2f,.9f,.035f),m,false,c,.01f);back.transform.localEulerAngles=new Vector3(-38,0,0);
            }
        }

        // ----- A stairwell hut in the back-left corner and a wooden water tower in the back-right -----
        static void RooftopHutAndTower()
        {
            var hut=Art.Group("Stairwell hut",new Vector3(-6.8f,0,-5f));
            Art.Box("Hut walls",new Vector3(0,1.15f,0),new Vector3(2.6f,2.3f,2.4f),Art.ToyCream,true,hut,.07f);
            Art.Box("Hut roof",new Vector3(0,2.4f,0),new Vector3(3.1f,.2f,2.9f),Art.ToyTomato,false,hut,.06f);
            Art.Box("Hut door frame",new Vector3(1.3f,1f,0),new Vector3(.06f,2.05f,1.25f),Art.ToySun,false,hut,.02f);
            Art.Box("Hut door",new Vector3(1.33f,.95f,0),new Vector3(.06f,1.9f,1f),Art.ToySky,false,hut,.02f);
            Art.Shape("Hut door knob",PrimitiveType.Sphere,new Vector3(1.38f,.95f,.34f),Vector3.one*.13f,Art.ToyCream,false,hut);
            var porthole=Art.Shape("Hut porthole",PrimitiveType.Cylinder,new Vector3(0,1.5f,1.21f),new Vector3(.8f,.04f,.8f),Art.ToySun,false,hut,.02f);porthole.transform.localEulerAngles=new Vector3(90,0,0);
            var glass=Art.Shape("Hut porthole glass",PrimitiveType.Cylinder,new Vector3(0,1.5f,1.235f),new Vector3(.58f,.03f,.58f),Art.ToySky,false,hut,.01f);glass.transform.localEulerAngles=new Vector3(90,0,0);
            Art.Shape("Hut vent pipe",PrimitiveType.Cylinder,new Vector3(-.7f,2.85f,.4f),new Vector3(.3f,.35f,.3f),Art.ToyWoodDark,false,hut,.03f);
            Art.Shape("Hut vent cap",PrimitiveType.Sphere,new Vector3(-.7f,3.2f,.4f),new Vector3(.46f,.24f,.46f),Art.ToyTomato,false,hut);

            var tank=Art.Group("Water tower",new Vector3(6.9f,0,-4.6f));
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)Art.Box("Tower leg",new Vector3(sx*.8f,1.4f,sz*.8f),new Vector3(.22f,2.8f,.22f),Art.ToyWoodDark,true,tank,.04f);
            for(int k=-1;k<=1;k+=2)
            {
                Art.Box("Tower brace",new Vector3(0,1.1f,k*.8f),new Vector3(1.7f,.1f,.1f),Art.ToyWoodDark,false,tank,.02f);
                Art.Box("Tower brace",new Vector3(k*.8f,1.1f,0),new Vector3(.1f,.1f,1.7f),Art.ToyWoodDark,false,tank,.02f);
            }
            Art.Box("Tower platform",new Vector3(0,2.88f,0),new Vector3(2.4f,.16f,2.4f),Art.ToyWood,false,tank,.04f);
            Art.Shape("Tower tank",PrimitiveType.Cylinder,new Vector3(0,3.95f,0),new Vector3(2.1f,.98f,2.1f),Art.ToyWood,false,tank,.05f);
            foreach(float y in new[]{3.45f,4.4f})Art.Shape("Tower hoop",PrimitiveType.Cylinder,new Vector3(0,y,0),new Vector3(2.18f,.06f,2.18f),Art.ToyTomato,false,tank,.02f);
            Art.Facet("Tower roof",new Vector3(0,4.93f,0),new Vector3(2.5f,1.5f,2.5f),Art.ToyTomato,10,tank);
            Art.Shape("Tower roof knob",PrimitiveType.Sphere,new Vector3(0,5.85f,0),Vector3.one*.22f,Art.ToySun,false,tank);
        }

        // ----- The skyline: rings of chunky pastel blocks, lower and fainter toward the horizon, with a clear gap where the sun rises -----
        static void RooftopSkyline()
        {
            var far=new Scenery();var rnd=new System.Random(5);
            float Rand(float a,float b)=>a+(float)rnd.NextDouble()*(b-a);
            float[] rings={30,44,60,76};float[] tallest={24,30,36,22};
            int built=0;bool hero=false;
            for(int ring=0;ring<rings.Length;ring++)
            {
                float radius=rings[ring];float theta=Rand(0,.25f);
                while(theta<Mathf.PI*2-.2f)
                {
                    float width=Rand(7,12)*(1+.1f*ring),depth=Rand(7,11),azDeg=theta*Mathf.Rad2Deg;
                    float fromSun=Mathf.Abs(Mathf.DeltaAngle(azDeg,RooftopRoom.SunAzimuth));
                    // Near the sun the blocks stay under about five degrees above the horizon, so the sun and its glow are never covered.
                    float lid=1.6f+radius*.085f;float top;
                    if(fromSun<17)top=Rand(Mathf.Max(.5f,lid-4),lid);
                    else if(fromSun<34)top=Rand(2f,Mathf.Lerp(lid,tallest[ring],(fromSun-17)/17f));
                    else top=Rand(5,tallest[ring]);
                    float x=Mathf.Sin(theta)*radius,z=3+Mathf.Cos(theta)*radius;
                    var euler=new Vector3(0,azDeg,0);var yaw=Quaternion.Euler(euler);
                    far.Cube(new Vector3(x,(top-45)/2,z),new Vector3(width,top+45,depth),Art.Skyline[(built*5+ring*2)%Art.Skyline.Length],euler,.3f+ring*.12f);
                    far.Cube(new Vector3(x,top+.3f,z),new Vector3(width+.5f,.6f,depth+.5f),Art.SkylineCap,euler,.12f);
                    var offset=yaw*new Vector3(Rand(-width*.25f,width*.25f),0,Rand(-depth*.2f,depth*.2f));float stairs=Rand(1.8f,3.2f);
                    far.Cube(new Vector3(x,top+.6f+stairs/2,z)+offset,new Vector3(Rand(2.4f,3.8f),stairs,Rand(2.4f,3.4f)),Art.SkylineCap,euler,.15f);
                    // A wooden water tower on one block to the left of the sunrise, and on a few others.
                    if(ring==0&&!hero&&Mathf.Abs(Mathf.DeltaAngle(azDeg,-30))<7){SkylineTower(far,new Vector3(x,top+.6f,z)-offset*.6f,1.35f);hero=true;}
                    else if(ring<3&&fromSun>40&&built%9==4)SkylineTower(far,new Vector3(x,top+.6f,z)-offset*.6f,1f);
                    else if(built%5==1&&fromSun>40)
                    {
                        far.Cube(new Vector3(x,top+.6f+3f,z)-offset,new Vector3(.14f,6f,.14f),Art.SkylineCap,euler,0f);
                        far.Ball(new Vector3(x,top+.6f+6.1f,z)-offset,Vector3.one*.5f,Art.Skyline[0]);
                    }
                    // Windows face the roof: pale blue panes, and a few lit ones.
                    if(ring<2)
                    {
                        int cols=Mathf.Max(2,Mathf.FloorToInt(width/3f));float spacing=width/cols;
                        int rows=Mathf.Clamp(Mathf.FloorToInt((top-2.5f+9)/3.2f),0,ring==0?7:5);
                        for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
                        {
                            var local=new Vector3((col+.5f-cols/2f)*spacing,top-2.2f-row*3.2f,-depth/2-.03f);
                            far.Add(WindowQuad(),new Vector3(x,0,z)+yaw*local,new Vector3(1.3f,1.7f,1),euler,rnd.NextDouble()<.3?Art.SkylineWindowLit:Art.SkylineWindowGlass);
                        }
                    }
                    theta+=(width*1.06f+Rand(0,2.5f))/radius;built++;
                }
            }
            far.Build(Art.Root,"skyline",ShadowCastingMode.Off);
        }
        // A water tower on stilts: legs, a platform, a round wooden tank with two hoops and a pointed lid.
        static void SkylineTower(Scenery s,Vector3 b,float k)
        {
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)s.Cube(b+new Vector3(sx*.75f,1.5f,sz*.75f)*k,new Vector3(.24f,3f,.24f)*k,Art.SkylineCap,default,.04f);
            s.Cube(b+new Vector3(0,3.05f,0)*k,new Vector3(2.4f,.2f,2.4f)*k,Art.SkylineCap,default,.05f);
            s.Tube(b+new Vector3(0,4.2f,0)*k,2.2f*k,2.2f*k,Art.SkylineWood,default,.05f);
            foreach(float y in new[]{3.6f,4.8f})s.Tube(b+new Vector3(0,y,0)*k,2.28f*k,.14f*k,Art.SkylineWindowGlass,default,.02f);
            s.Add(Cone(),b+new Vector3(0,5.3f,0)*k,new Vector3(2.7f,1.7f,2.7f)*k,Vector3.zero,Art.Skyline[0]);
        }

        // ----- The sky: clouds that drift and a hot-air balloon that bobs, both kept off the sun -----
        static void RooftopSky(RooftopRoom room)
        {
            var sky=Art.Group("Drifting sky",Vector3.zero);room.moving=sky;
            var rnd=new System.Random(11);float Rand(float a,float b)=>a+(float)rnd.NextDouble()*(b-a);
            Vector3 Polar(float azDeg,float elDeg,float distance)
            {
                float az=azDeg*Mathf.Deg2Rad,el=elDeg*Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(az)*Mathf.Cos(el)*distance,1.6f+Mathf.Sin(el)*distance,3+Mathf.Cos(az)*Mathf.Cos(el)*distance);
            }
            // azimuth, elevation, distance, size: spread around the sky, with the sun's patch left clear.
            float[][] specs={new[]{-46f,22f,62f,1.2f},new[]{-14f,31f,70f,1f},new[]{56f,27f,64f,1.25f},new[]{82f,12f,70f,1f},new[]{-78f,15f,66f,1.1f},new[]{150f,24f,60f,1.2f},new[]{-128f,28f,68f,1f},new[]{38f,9f,72f,1f},new[]{12f,44f,74f,.9f},new[]{-28f,10f,74f,.9f}};
            var drifting=new List<Transform>();var speeds=new List<float>();
            for(int i=0;i<specs.Length;i++)
            {
                var spec=specs[i];float size=spec[3];var mat=i%3==1?Art.CloudPink:Art.CloudWhite;
                var cloud=Art.Group("Cloud "+(i+1),Polar(spec[0],spec[1],spec[2]),sky);cloud.localEulerAngles=new Vector3(0,spec[0],0);
                var parts=new Scenery();int puffs=4+i%2;float width=16f*size;
                for(int k=0;k<puffs;k++)
                {
                    float t=k/(float)(puffs-1)-.5f;float lift=Mathf.Clamp01(1-Mathf.Abs(t)*1.6f);
                    float h=Rand(4.5f,6.5f)*size*(.6f+.6f*lift),w=Rand(9,12)*size*(.8f+.3f*lift);
                    parts.Ball(new Vector3(t*width*.9f+Rand(-1,1),h*.3f,Rand(-1.5f,1.5f)),new Vector3(w,h,w*.8f),mat);
                }
                parts.Ball(new Vector3(0,-.9f*size,0),new Vector3(width*1.05f,3f*size,6.5f*size),mat);
                parts.Build(cloud,"cloud "+(i+1),ShadowCastingMode.Off);
                drifting.Add(cloud);speeds.Add(Rand(.08f,.25f));
            }
            room.drifting=drifting.ToArray();room.speeds=speeds.ToArray();
            var balloon=Art.Group("Hot air balloon",Polar(42,25,62),sky);room.balloon=balloon;
            Art.Shape("Balloon envelope",PrimitiveType.Sphere,Vector3.zero,new Vector3(8.4f,9.4f,8.4f),Art.ToyTomato,false,balloon);
            Art.Shape("Balloon band",PrimitiveType.Cylinder,new Vector3(0,.2f,0),new Vector3(8.5f,.85f,8.5f),Art.ToySun,false,balloon,.05f);
            Art.Shape("Balloon band",PrimitiveType.Cylinder,new Vector3(0,-2.1f,0),new Vector3(7.8f,.4f,7.8f),Art.ToySky,false,balloon,.05f);
            Art.Shape("Balloon cap",PrimitiveType.Sphere,new Vector3(0,4.3f,0),new Vector3(2.4f,1.6f,2.4f),Art.ToyCream,false,balloon);
            Art.Shape("Balloon skirt",PrimitiveType.Cylinder,new Vector3(0,-4.9f,0),new Vector3(2.8f,.5f,2.8f),Art.ToyWoodDark,false,balloon,.05f);
            for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)Art.Line("Balloon rope",new Vector3(sx*1f,-5.3f,sz*1f),new Vector3(sx*.7f,-7.2f,sz*.7f),.08f,Art.ToyWoodDark,balloon);
            Art.Box("Balloon basket",new Vector3(0,-7.9f,0),new Vector3(1.8f,1.3f,1.8f),Art.ToyWood,false,balloon,.08f);
            Art.Box("Balloon basket rim",new Vector3(0,-7.2f,0),new Vector3(2f,.14f,2f),Art.ToyWoodDark,false,balloon,.05f);
        }
    }
}
