using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Looks for signs a player cannot read. For every sign in every room it sends rays from where a player's eyes can be (the
    // arrival point at three eye heights, and a grid over every floor that can be stood on) to a grid of points across the sign,
    // and counts the sign as seen from a spot when at least 80% of those points are in clear view. The rays are tested against
    // the triangles of everything solid and visible (not physics colliders, which are not what the player sees), against the
    // other signs, and the colour of what is directly behind the sign is compared with the sign's own colour. The ocean is
    // checked at every stroke, because its shore moves toward the boat as you row.
    public static class TextVisibilityValidation
    {
        // Hits this close to a sign are its own backing (a board, a button, a wall it is printed on).
        const float Tolerance=.06f,MinDistance=.3f,GridStep=1.5f,SeenFraction=.8f,MinFacing=.25f;
        // Letters this many degrees tall are about ten pixels on a Quest 3; farther than that a sign is not expected to be read.
        const float ReadableDegrees=.4f,MinContrast=1.8f,MinFogVisible=.6f,LowContrastShare=.25f;
        static readonly float[] ArrivalHeights={1.45f,1.65f,1.85f};
        static IEnumerator<float> sequence;static double next;static float waitUntil;
        static readonly List<string> results=new List<string>(),details=new List<string>();
        static string checkpoint,wardrobe;static bool hadCheckpoint,hadProgress,hadWardrobe;static int progress;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;

        sealed class Solid{public string name;public Vector3 min,max;public Vector3[] verts;public int[] tris;public int cull;public Color color;public bool exact;}
        sealed class Quad{public TextMeshPro t;public Vector3 pos,fwd;public Bounds local;}
        struct View{public Vector3 eye;public bool arrival;}
        static readonly List<Solid> solids=new List<Solid>();
        static readonly Dictionary<Mesh,(Vector3[] v,int[] t)> meshes=new Dictionary<Mesh,(Vector3[],int[])>();
        // Every sign that was checked, and what was wrong with it: "room|sign" -> issue -> states (-1 when the room does not change).
        static readonly Dictionary<string,Dictionary<string,List<int>>> issues=new Dictionary<string,Dictionary<string,List<int>>>();
        static readonly Dictionary<string,string> notes=new Dictionary<string,string>();
        static readonly List<string> order=new List<string>();
        static readonly Dictionary<string,string> labels=new Dictionary<string,string>();
        static int skinned;

        public static void Start()
        {
            if(sequence!=null)return;if(!EditorApplication.isPlaying||P.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            checkpoint=PlayerPrefs.GetString(CheckpointStore.Key);hadCheckpoint=PlayerPrefs.HasKey(CheckpointStore.Key);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);hadWardrobe=PlayerPrefs.HasKey(Wardrobe.Key);
            hadProgress=PlayerPrefs.HasKey("AfterHours.Progress.v1");progress=PlayerPrefs.GetInt("AfterHours.Progress.v1",0);
            D.saveEnabled=false;if(D.intro)D.intro.Hide();
            results.Clear();details.Clear();issues.Clear();notes.Clear();order.Clear();labels.Clear();skinned=0;
            Status="Running";waitUntil=0;sequence=Run();EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            if(Time.time<waitUntil)return;
            try{if(!sequence.MoveNext()){Report();return;}waitUntil=Time.time+sequence.Current;}catch(Exception e){Finish("Failed: "+e.Message+"\n"+e.StackTrace);}
        }
        static void Finish(string status)
        {
            if(sequence==null)return;sequence=null;Status=status.StartsWith("Failed")&&status.Length>200?status.Substring(0,200):status;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            if(D)D.saveEnabled=false;
            if(hadCheckpoint)PlayerPrefs.SetString(CheckpointStore.Key,checkpoint);else PlayerPrefs.DeleteKey(CheckpointStore.Key);
            if(hadProgress)PlayerPrefs.SetInt("AfterHours.Progress.v1",progress);else PlayerPrefs.DeleteKey("AfterHours.Progress.v1");
            if(hadWardrobe)PlayerPrefs.SetString(Wardrobe.Key,wardrobe);else PlayerPrefs.DeleteKey(Wardrobe.Key);PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");
            File.WriteAllLines("Documentation/Text-visibility-validation.txt",new[]{DateTime.UtcNow.ToString("u"),status,"Rays from where a player's eyes can be to a grid of points across each sign, tested against the visible triangles of everything solid and against other signs."}.Concat(results).Concat(details));
            Debug.Log("Text visibility validation: "+status);
        }

        static IEnumerator<float> Run()
        {
            PlayerPrefs.DeleteKey(CheckpointStore.Key);PlayerPrefs.DeleteKey("AfterHours.Progress.v1");
            SceneManager.LoadScene("AfterHours");yield return 1f;D.saveEnabled=false;if(D.intro)D.intro.Hide();yield return .5f;
            for(int room=0;room<D.rooms.Length;room++)
            {
                for(int wait=0;wait<40&&P.busy;wait++)yield return .1f;
                if(D.currentRoom!=room){D.Travel(room);yield return 1.4f;}
                if(D.currentRoom!=room)throw new Exception("Could not travel to room "+room);
                Physics.SyncTransforms();
                if(room==1)
                {
                    // The shore moves toward the boat a little with every stroke, so the room is checked at each one.
                    for(int strokes=0;strokes<=D.ocean.requiredStrokes;strokes++){D.ocean.RestoreProgress(strokes);yield return .15f;Physics.SyncTransforms();Scan(room,strokes);yield return .05f;}
                    D.ocean.RestoreProgress(0);
                }
                else Scan(room,-1);
                yield return .1f;
            }
        }

        // ----- Geometry -----
        static string Name(Transform t)=>(t.parent?t.parent.name+"/"+t.name:t.name).Replace("\n"," ");
        static bool Transparent(Material m){if(!m)return false;if(m.renderQueue>=3000)return true;return m.HasProperty("_Surface")&&m.GetFloat("_Surface")>=1;}
        static Color Look(Material m)
        {
            if(!m)return Color.gray;var c=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.GetColor("_Color"):Color.gray;
            if(m.HasProperty("_EmissionColor")&&m.IsKeywordEnabled("_EMISSION")){var e=m.GetColor("_EmissionColor");c=new Color(Mathf.Max(c.r,e.r),Mathf.Max(c.g,e.g),Mathf.Max(c.b,e.b),1);}
            return c;
        }
        static bool IsUi(Transform t)=>(P&&t.IsChildOf(P.transform))||(D.menu&&t.IsChildOf(D.menu.transform))||(D.intro&&t.IsChildOf(D.intro.transform));
        static void CollectSolids()
        {
            solids.Clear();
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if(!r.enabled||r.shadowCastingMode==ShadowCastingMode.ShadowsOnly||r.GetComponent<TMP_Text>()||IsUi(r.transform))continue;
                var mats=r.sharedMaterials;if(mats.Length==0||mats.All(Transparent))continue;
                var filter=r.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;if(!mesh)continue;
                var first=mats.First(m=>!Transparent(m));
                var b=r.bounds;var s=new Solid{name=Name(r.transform),color=Look(first),min=b.min,max=b.max};
                if(mesh.isReadable)
                {
                    if(!meshes.TryGetValue(mesh,out var data)){data=(mesh.vertices,mesh.triangles);meshes[mesh]=data;}
                    var m4=r.localToWorldMatrix;var v=new Vector3[data.v.Length];for(int i=0;i<v.Length;i++)v[i]=m4.MultiplyPoint3x4(data.v[i]);
                    s.verts=v;s.tris=data.t;s.exact=true;
                    int cull=first&&first.HasProperty("_Cull")?(int)first.GetFloat("_Cull"):2;
                    if(m4.determinant<0)cull=cull==2?1:cull==1?2:0;
                    s.cull=cull;
                }
                solids.Add(s);
            }
            skinned=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude).Count(x=>!IsUi(x.transform));
        }
        // The nearest solid surface along a ray between two distances, or null.
        static Solid Hit(Vector3 o,Vector3 dir,float tMin,float tMax,out float at)
        {
            Solid best=null;at=tMax;
            float ix=1/(Mathf.Abs(dir.x)<1e-9f?1e-9f:dir.x),iy=1/(Mathf.Abs(dir.y)<1e-9f?1e-9f:dir.y),iz=1/(Mathf.Abs(dir.z)<1e-9f?1e-9f:dir.z);
            for(int k=0;k<solids.Count;k++)
            {
                var s=solids[k];
                float t1=(s.min.x-o.x)*ix,t2=(s.max.x-o.x)*ix;float lo=Mathf.Min(t1,t2),hi=Mathf.Max(t1,t2);
                t1=(s.min.y-o.y)*iy;t2=(s.max.y-o.y)*iy;lo=Mathf.Max(lo,Mathf.Min(t1,t2));hi=Mathf.Min(hi,Mathf.Max(t1,t2));
                t1=(s.min.z-o.z)*iz;t2=(s.max.z-o.z)*iz;lo=Mathf.Max(lo,Mathf.Min(t1,t2));hi=Mathf.Min(hi,Mathf.Max(t1,t2));
                if(hi<lo||hi<tMin||lo>=at)continue;
                if(!s.exact){float t=Mathf.Max(lo,tMin);if(t<at){best=s;at=t;}continue;}
                var verts=s.verts;var tris=s.tris;
                for(int i=0;i<tris.Length;i+=3)
                {
                    var a=verts[tris[i]];var e1=verts[tris[i+1]]-a;var e2=verts[tris[i+2]]-a;var p=Vector3.Cross(dir,e2);float det=Vector3.Dot(e1,p);
                    if(det>-1e-9f&&det<1e-9f)continue;
                    // A triangle faces the ray's origin when det is positive (front) and away when it is negative.
                    if(s.cull==2&&det<0)continue;if(s.cull==1&&det>0)continue;
                    float id=1/det;var tv=o-a;float u=Vector3.Dot(tv,p)*id;if(u<0||u>1)continue;
                    var q=Vector3.Cross(tv,e1);float w=Vector3.Dot(dir,q)*id;if(w<0||u+w>1)continue;
                    float tt=Vector3.Dot(e2,q)*id;if(tt<tMin||tt>=at)continue;
                    best=s;at=tt;
                }
            }
            return best;
        }
        // The merged "Batched geometry" meshes say little, so say where in the room the surface is.
        static string Describe(Solid hit,Vector3 point,Vector3 roomOrigin)
        {
            if(!hit.name.Contains("Batched"))return hit.name;
            var l=point-roomOrigin;return hit.name+" near ("+(Mathf.Round(l.x*2)/2).ToString("0.0")+", "+(Mathf.Round(l.y*2)/2).ToString("0.0")+", "+(Mathf.Round(l.z*2)/2).ToString("0.0")+")";
        }
        // An outline helps a little but is thin (it can only be as wide as the font atlas padding), so it never excuses a poor background; it is only reported.
        static bool Outlined(TextMeshPro t)
        {
            var m=t.fontSharedMaterial;if(!m||!m.IsKeywordEnabled("OUTLINE_ON")||!m.HasProperty("_OutlineWidth")||m.GetFloat("_OutlineWidth")<.05f)return false;
            return Ratio(t.color,m.GetColor("_OutlineColor"))>=4;
        }
        static float Lum(Color c){c=c.linear;return .2126f*c.r+.7152f*c.g+.0722f*c.b;}
        static float Ratio(Color a,Color b){float x=Lum(a),y=Lum(b);return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
        // Another sign in the way: the ray crosses its rectangle before it reaches the sign being looked at.
        static string TextBetween(Vector3 eye,Vector3 dir,float dist,List<Quad> quads,Quad own)
        {
            foreach(var q in quads)
            {
                if(q==own)continue;
                float denom=Vector3.Dot(dir,q.fwd);if(Mathf.Abs(denom)<1e-4f)continue;
                float t=Vector3.Dot(q.pos-eye,q.fwd)/denom;if(t<.02f||t>dist-Tolerance)continue;
                var l=q.t.transform.InverseTransformPoint(eye+dir*t);
                if(Mathf.Abs(l.x-q.local.center.x)<=q.local.extents.x+.02f&&Mathf.Abs(l.y-q.local.center.y)<=q.local.extents.y+.02f)return "text '"+Short(q.t.text)+"'";
            }
            return null;
        }
        static string Short(string s){s=(s??"").Replace("\n"," ").Trim();s=System.Text.RegularExpressions.Regex.Replace(s,"<[^>]+>","");return s.Length>34?s.Substring(0,34)+"...":s;}

        // ----- Where a player's eyes can be -----
        static List<View> Views(MindRoom room)
        {
            var list=new List<View>();var sp=room.spawn.position;
            foreach(var h in ArrivalHeights)list.Add(new View{eye=new Vector3(sp.x,sp.y+h,sp.z),arrival=true});
            foreach(var surface in room.root.GetComponentsInChildren<TeleportSurface>())
            {
                var col=surface.GetComponent<Collider>();if(!col||!col.enabled)continue;var bb=col.bounds;
                for(float x=bb.min.x+GridStep*.5f;x<bb.max.x;x+=GridStep)for(float z=bb.min.z+GridStep*.5f;z<bb.max.z;z+=GridStep)
                {
                    if(!col.Raycast(new Ray(new Vector3(x,bb.max.y+1,z),Vector3.down),out var hit,bb.size.y+2)||hit.normal.y<.85f)continue;
                    // Somewhere something solid stands is not a place a player can be.
                    if(Hit(hit.point+Vector3.up*.05f,Vector3.up,0,1.6f,out _)!=null)continue;
                    list.Add(new View{eye=hit.point+Vector3.up*1.65f,arrival=false});
                }
            }
            return list;
        }

        // ----- The check -----
        static void Scan(int roomIndex,int state)
        {
            var room=D.rooms[roomIndex];CollectSolids();var views=Views(room);
            var texts=room.root.GetComponentsInChildren<TextMeshPro>(false).Where(t=>t.enabled&&t.GetComponent<MeshRenderer>()&&t.GetComponent<MeshRenderer>().enabled&&!IsUi(t.transform)&&Short(t.text).Length>0&&t.color.a>.05f).ToList();
            var quads=new List<Quad>();
            foreach(var t in texts){t.ForceMeshUpdate();var tb=t.textBounds;if(tb.size.x<.001f||tb.size.y<.001f)continue;quads.Add(new Quad{t=t,pos=t.transform.position,fwd=t.transform.forward,local=tb});}
            foreach(var q in quads)AuditSign(roomIndex,state,room,q,quads,views);
            // Two signs printed on top of each other.
            for(int i=0;i<quads.Count;i++)for(int j=i+1;j<quads.Count;j++)
            {
                var a=quads[i];var b=quads[j];
                if(Mathf.Abs(Vector3.Dot(a.fwd,b.fwd))<.98f||Mathf.Abs(Vector3.Dot(b.pos-a.pos,a.fwd))>Tolerance)continue;
                var c=b.local;float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
                for(int k=0;k<4;k++)
                {
                    var corner=b.t.transform.TransformPoint(new Vector3(k%2==0?c.min.x:c.max.x,k<2?c.min.y:c.max.y,c.center.z));var l=a.t.transform.InverseTransformPoint(corner);
                    minX=Mathf.Min(minX,l.x);maxX=Mathf.Max(maxX,l.x);minY=Mathf.Min(minY,l.y);maxY=Mathf.Max(maxY,l.y);
                }
                float ox=Mathf.Min(maxX,a.local.max.x)-Mathf.Max(minX,a.local.min.x),oy=Mathf.Min(maxY,a.local.max.y)-Mathf.Max(minY,a.local.min.y);
                if(ox<=0||oy<=0)continue;
                float overlap=ox*oy/Mathf.Min(a.local.size.x*a.local.size.y,(maxX-minX)*(maxY-minY));
                if(overlap>.15f){Issue(roomIndex,a,state,"printed over text '"+Short(b.t.text)+"'");Issue(roomIndex,b,state,"printed over text '"+Short(a.t.text)+"'");}
            }
        }
        // Identified by where it hangs in its parent, so a sign that moves with the shore or changes its words is still one sign.
        static string Key(int room,Quad q)
        {
            var t=q.t.transform;var p=t.localPosition;
            return "room "+room+" ["+D.rooms[room].title+"] "+(t.parent?t.parent.name.Replace("\n"," ")+"/":"")+t.name.Replace("\n"," ")+" at ("+p.x.ToString("0.0")+", "+p.y.ToString("0.0")+", "+p.z.ToString("0.0")+")";
        }
        static void Issue(int room,Quad q,int state,string text)
        {
            var key=Key(room,q);if(!issues.TryGetValue(key,out var map)){map=new Dictionary<string,List<int>>();issues[key]=map;order.Add(key);labels[key]=Short(q.t.text);}
            if(text==null)return;if(!map.TryGetValue(text,out var states)){states=new List<int>();map[text]=states;}if(!states.Contains(state))states.Add(state);
        }
        static void AuditSign(int roomIndex,int state,MindRoom room,Quad q,List<Quad> quads,List<View> views)
        {
            var t=q.t;Issue(roomIndex,q,state,null);var origin=room.root.transform.position;
            var bounds=q.local;var samples=new List<Vector3>();
            for(int i=0;i<5;i++)for(int j=0;j<3;j++)
                samples.Add(t.transform.TransformPoint(new Vector3(Mathf.Lerp(bounds.min.x+bounds.size.x*.05f,bounds.max.x-bounds.size.x*.05f,i/4f),Mathf.Lerp(bounds.min.y+bounds.size.y*.1f,bounds.max.y-bounds.size.y*.1f,j/2f),bounds.center.z)));
            var center=t.transform.TransformPoint(bounds.center);
            float lineHeight=t.textInfo!=null&&t.textInfo.lineCount>0?t.textInfo.lineInfo[0].lineHeight*Mathf.Abs(t.transform.lossyScale.y):bounds.size.y*Mathf.Abs(t.transform.lossyScale.y);
            float cap=Mathf.Max(lineHeight*.7f,.002f),farthest=cap/Mathf.Tan(ReadableDegrees*Mathf.Deg2Rad);
            int range=0,front=0,clear=0;var blockers=new Dictionary<string,int>();string arrivalBlocked=null;float arrivalContrast=99;string arrivalAgainst=null;int arrivalShare=0;float fog=1;bool outlined=Outlined(t);
            foreach(var v in views)
            {
                var toView=v.eye-center;float dist=toView.magnitude;if(dist<MinDistance||dist>farthest)continue;range++;
                if(Vector3.Dot(toView/dist,-q.fwd)<MinFacing)continue;front++;
                int seen=0;string who=null;var sampleTotal=samples.Count;int low=0;float worst=99;string against=null;
                foreach(var s in samples)
                {
                    var dir=s-v.eye;float d=dir.magnitude;dir/=d;float at;
                    var hit=Hit(v.eye,dir,.02f,d-Tolerance,out at);string by=hit!=null?Describe(hit,v.eye+dir*at,origin):TextBetween(v.eye,dir,d,quads,q);
                    if(by==null)
                    {
                        seen++;
                        if(v.arrival)
                        {
                            var back=Hit(v.eye,dir,d-Tolerance,d+400,out at);
                            if(back!=null){float ratio=Ratio(t.color,back.color);if(ratio<MinContrast){low++;if(ratio<worst){worst=ratio;against=Describe(back,v.eye+dir*at,origin);}}}
                        }
                    }
                    else if(who==null)who=by;
                }
                bool ok=seen>=sampleTotal*SeenFraction;if(ok)clear++;
                else{if(who!=null)blockers[who]=blockers.TryGetValue(who,out var n)?n+1:1;if(v.arrival&&arrivalBlocked==null)arrivalBlocked=who??"something";}
                if(v.arrival&&ok)
                {
                    // A thin strip or a tile seam behind a few letters is not a problem; a quarter of the sign over a pale or dark patch is.
                    if(seen>0&&low>=seen*LowContrastShare&&worst<arrivalContrast){arrivalContrast=worst;arrivalAgainst=against;arrivalShare=Mathf.RoundToInt(100f*low/seen);}
                    if(RenderSettings.fog)
                    {
                        float f=RenderSettings.fogMode==FogMode.Linear?Mathf.Clamp01((RenderSettings.fogEndDistance-dist)/Mathf.Max(.01f,RenderSettings.fogEndDistance-RenderSettings.fogStartDistance)):RenderSettings.fogMode==FogMode.Exponential?Mathf.Exp(-RenderSettings.fogDensity*dist):Mathf.Exp(-Mathf.Pow(RenderSettings.fogDensity*dist,2));
                        fog=Mathf.Min(fog,f);
                    }
                }
            }
            string top=blockers.Count>0?blockers.OrderByDescending(x=>x.Value).First().Key:null;
            if(range==0)Note(roomIndex,q,"out of reading range from everywhere sampled (letters about "+(cap*100).ToString("0.0")+" cm tall)");
            else if(front==0)Issue(roomIndex,q,state,"faces away from every place it could be read from");
            else
            {
                if(clear==0)Issue(roomIndex,q,state,"hidden from all "+front+" places it could be read from, blocked by "+top);
                else if(clear<front*.5f)Issue(roomIndex,q,state,"hidden from most places ("+(front-clear)+" of "+front+"), blocked by "+top);
                if(arrivalBlocked!=null)Issue(roomIndex,q,state,"hidden from the arrival point, blocked by "+arrivalBlocked);
                if(arrivalContrast<MinContrast)Issue(roomIndex,q,state,"hard to read against "+arrivalAgainst+" behind it (contrast "+arrivalContrast.ToString("0.0")+" to 1 over "+arrivalShare+"% of the sign)");
                if(fog<MinFogVisible)Issue(roomIndex,q,state,"faded by fog at the arrival point ("+Mathf.RoundToInt(fog*100)+"% visible)");
                if(clear<front&&clear>=front*.5f)Note(roomIndex,q,"partly blocked from "+(front-clear)+" of "+front+" places, by "+top);
            }
            if(!notes.ContainsKey(Key(roomIndex,q)+"|stats"))notes[Key(roomIndex,q)+"|stats"]="in range of "+range+" places, faced from "+front+", clear from "+clear+(outlined?", outlined":"")+(arrivalContrast<99?", lowest contrast "+arrivalContrast.ToString("0.0"):"");
        }
        static void Note(int room,Quad q,string text){var key=Key(room,q)+"|note";notes[key]=notes.TryGetValue(key,out var old)&&old!=text?old+"; "+text:text;}

        static string Range(List<int> states)
        {
            if(states.Count==1&&states[0]<0)return "";
            states.Sort();var parts=new List<string>();int start=states[0],prev=start;
            for(int i=1;i<=states.Count;i++)
            {
                if(i<states.Count&&states[i]==prev+1){prev=states[i];continue;}
                parts.Add(start==prev?start.ToString():start+"-"+prev);if(i<states.Count){start=states[i];prev=start;}
            }
            return " (at stroke "+string.Join(", ",parts)+")";
        }
        static void Report()
        {
            int failed=0;
            foreach(var key in order)
            {
                var map=issues[key];
                if(map.Count==0)results.Add("PASS: "+key+" '"+labels[key]+"'");
                else{failed++;foreach(var kv in map)results.Add("FAIL: "+key+" '"+labels[key]+"': "+kv.Key+Range(kv.Value));}
            }
            foreach(var kv in notes.Where(x=>x.Key.EndsWith("|note")))details.Add("NOTE: "+kv.Key.Replace("|note","")+": "+kv.Value);
            foreach(var kv in notes.Where(x=>x.Key.EndsWith("|stats")))details.Add("INFO: "+kv.Key.Replace("|stats","")+": "+kv.Value);
            details.Add("INFO: "+order.Count+" signs checked; "+skinned+" skinned meshes in the last scan were not counted as solid.");
            Finish(failed==0?"Passed":"Failed: "+failed+" of "+order.Count+" signs have a problem");
        }
    }
}
