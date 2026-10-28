using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Regression test for the rooftop's tower of blocks: what it is made of, the physics settings it plays with, the tower standing perfectly
    // still, a loose block eased out and laid on top, a load-bearing block that will not come and slips from the hand, a fingertip nudging a
    // block through, a desktop grab with real input, the tower coming down, NEW TOWER, and leaving and coming back.
    public static class TowerValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,BlockTower.BestKey};
        static bool[] existed;static string checkpoint;static int[] values;
        static Transform hand;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        static BlockTower Tower=>D.rooftop.tower;
        static Transform Root=>D.rooms[RooftopRoom.Room].root.transform;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);values=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("TowerValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("TowerValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Info(string text){lines.Add("INFO: "+text);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static void Aim(Component c,Vector3 at)
        {
            P.Head.rotation=Quaternion.LookRotation(at-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||!hit.collider.transform.IsChildOf(c.transform))
                throw new Exception("Pointer target obstructed: "+c.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static IEnumerable<float> Click(Interactable b){Check(b,"The button to press exists");Aim(b,b.transform.position);Press(true);yield return .12f;Press(false);yield return .2f;}
        static string Mm(float metres)=>(metres*1000).ToString("0.0")+" mm";
        // The largest drift of any block the player has not touched.
        static float Untouched()=>Tower.blocks.Where(b=>!b.touched).Select(b=>b.Drift).DefaultIfEmpty(0).Max();
        // The block in a row that carries nothing: the shortest, when it is clearly shorter than both of the others.
        static TowerBlock Loose(int row)
        {
            var r=Tower.blocks.Where(b=>b.level==row).OrderBy(b=>b.height).ToArray();
            return r[1].height-r[0].height>.0002f?r[0]:null;
        }
        static TowerBlock Tallest(int row)=>Tower.blocks.Where(b=>b.level==row).OrderByDescending(b=>b.height).First();
        // Moves the test hand in a straight line, at a steady speed, a frame at a time.
        static IEnumerable<float> Move(Vector3 to,float speed)
        {
            var from=hand.position;float time=Vector3.Distance(from,to)/speed;
            for(float t=0;t<time;){t+=Time.deltaTime;hand.position=Vector3.Lerp(from,to,Mathf.Clamp01(t/time));yield return 0;}
        }
        static IEnumerable<float> MoveTip(int index,Vector3 from,Vector3 to,float speed)
        {
            float time=Vector3.Distance(from,to)/speed;
            for(float t=0;t<time;){t+=Time.deltaTime;Tower.fingertipOverride[index]=Vector3.Lerp(from,to,Mathf.Clamp01(t/time));yield return 0;}
        }

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            var t=Tower;
            // ---- What the scene holds
            Check(t&&t.blocks.Length==BlockTower.Levels*BlockTower.PerLevel&&t.blocks.Length==54,"The rooftop has a tower of 54 blocks: 18 rows of three");
            Check(t.blocks.All(b=>b&&b.GetComponent<Grabbable>().physical&&b.GetComponent<Rigidbody>()&&b.GetComponent<BoxCollider>()),"Every block is a box under physics, held by force rather than carried");
            Check(t.blocks.All(b=>Mathf.Abs(b.transform.lossyScale.x-BlockTower.Length)<.001f&&Mathf.Abs(b.transform.lossyScale.z-BlockTower.Width)<.001f),"Every block is 15 by 5 cm (a real set at twice the size)");
            var newTower=Root.GetComponentsInChildren<Interactable>(true).FirstOrDefault(x=>x.kind==ActionKind.NewTower);
            Check(newTower&&t.score&&t.knock&&t.tumble,"NEW TOWER, the score on the sign, and the knock and fall sounds are there");
            float rate=Time.fixedDeltaTime;

            // ---- Arriving: physics settings, and the tower standing still
            D.Travel(RooftopRoom.Room);yield return 1.2f;
            Check(D.currentRoom==RooftopRoom.Room&&BlockTower.Playing==t,"Walking onto the rooftop");
            Check(Mathf.Abs(Time.fixedDeltaTime-1f/BlockTower.PhysicsRate)<1e-5f,"Physics steps 90 times a second on the roof (it was "+(1/rate).ToString("0")+")");
            var b0=t.blocks[0];var wood=b0.box.sharedMaterial;
            Check(b0.body.solverIterations==BlockTower.SolverIterations&&b0.body.solverVelocityIterations==BlockTower.VelocityIterations&&Mathf.Approximately(b0.body.mass,BlockTower.Mass),"Each block weighs 140 g and solves its contacts "+BlockTower.SolverIterations+" times a step ("+BlockTower.VelocityIterations+" for velocity)");
            Check(wood&&Mathf.Approximately(wood.staticFriction,BlockTower.StaticFriction)&&Mathf.Approximately(wood.dynamicFriction,BlockTower.DynamicFriction)&&wood.bounciness==0&&b0.box.contactOffset<.002f,"Wood on wood: friction "+BlockTower.StaticFriction+" at rest and "+BlockTower.DynamicFriction+" sliding, no bounce, contacts from 1.5 mm");
            Check(t.blocks.All(b=>b.height<=BlockTower.Height+1e-6f&&b.height>=BlockTower.Height*(1-BlockTower.HeightSpread)-1e-6f),"Every block is full height or up to 2.5% short, like a real set");
            int looseRows=Enumerable.Range(0,BlockTower.Levels).Count(r=>Loose(r));
            bool twoBear=Enumerable.Range(0,BlockTower.Levels).All(r=>t.blocks.Count(b=>b.level==r&&Mathf.Approximately(b.height,BlockTower.Height))>=2);
            Check(looseRows>=8&&looseRows<=17&&twoBear,looseRows+" of 18 rows have a loose block, and every row rests on at least two full-height blocks");
            float top=t.foot.position.y+t.TopHeight-Root.position.y;
            Check(top>1.1f&&top<1.25f&&t.foot.position.y-Root.position.y>.6f,"The tower stands on a table, its top at "+top.ToString("0.00")+" m");
            var spawn=D.rooms[RooftopRoom.Room].spawn.position;float away=Vector2.Distance(new Vector2(spawn.x,spawn.z),new Vector2(t.foot.position.x,t.foot.position.z));
            Check(away>2.5f&&away<4.5f,"The table is "+away.ToString("0.0")+" m from the arrival point");
            yield return 3f;
            Check(!t.Fallen&&t.blocks.Max(b=>b.Drift)<.002f,"Left alone for three seconds the tower stands still (largest drift "+Mm(t.blocks.Max(b=>b.Drift))+")");
            Info(t.blocks.Count(b=>b.body.IsSleeping())+" of 54 blocks asleep");

            // ---- Easing a loose block out and laying it on top
            hand=new GameObject("Tower test hand").transform;
            var loose=Enumerable.Range(9,6).Select(Loose).FirstOrDefault(b=>b);
            Check(loose,"A loose block in the upper middle of the tower");
            var axis=loose.transform.right;if(Vector3.Dot(axis,loose.transform.position-t.foot.position)<0)axis=-axis;
            hand.SetPositionAndRotation(loose.transform.position,Quaternion.identity);
            Check(loose.grab.TakeBlock(hand,false)&&loose.grab.held,"Taking hold of the loose block in row "+(loose.level+1));
            var start=loose.transform.position;
            foreach(var x in Move(start+axis*.2f,.1f))yield return x;yield return .3f;
            float pulled=Vector3.Dot(loose.transform.position-start,axis);
            Check(!loose.grab.Slipped&&pulled>.15f,"Drawn slowly, it slides out ("+Mm(pulled)+")");
            Check(!t.Fallen&&Untouched()<.004f,"Nothing else moved more than 4 mm (largest "+Mm(Untouched())+")");
            float over=t.foot.position.y+t.TopHeight+BlockTower.Height*.5f+.004f;
            foreach(var x in Move(new Vector3(hand.position.x,over+.08f,hand.position.z),.15f))yield return x;
            foreach(var x in Move(new Vector3(t.foot.position.x,over+.08f,t.foot.position.z),.15f))yield return x;
            foreach(var x in Move(new Vector3(t.foot.position.x,over,t.foot.position.z),.08f))yield return x;
            yield return .3f;loose.grab.Release();yield return 1.5f;
            Check(t.OnTop==1&&t.Best==1&&t.score.text.Contains("ON TOP  1")&&t.score.text.Contains("BEST  1"),"Laid on top, it counts: "+t.score.text);
            Check(PlayerPrefs.GetInt(BlockTower.BestKey)==1,"The best tower is kept");
            Check(!t.Fallen&&Untouched()<.004f,"The tower takes it without moving (largest drift "+Mm(Untouched())+")");

            // ---- A block the tower rests on will not come
            var bearing=Tallest(2);
            axis=bearing.transform.right;if(Vector3.Dot(axis,bearing.transform.position-t.foot.position)<0)axis=-axis;
            hand.position=bearing.transform.position;start=bearing.transform.position;
            Check(bearing.grab.TakeBlock(hand,false),"Taking hold of the block the third row rests on");
            foreach(var x in Move(start+axis*.16f,.13f))yield return x;yield return .3f;
            float budged=Vector3.Distance(bearing.transform.position,start);
            Check(budged<.01f,"It will not come: the rows above press down on it (moved "+Mm(budged)+")");
            Check(bearing.grab.Slipped,"And it slips out of the hand that pulls on");
            bearing.grab.Release();yield return .5f;
            Check(!t.Fallen&&Untouched()<.004f,"The tower is still standing (largest drift "+Mm(Untouched())+")");

            // ---- A fingertip nudges a loose block through
            var nudged=Enumerable.Range(4,5).Select(Loose).FirstOrDefault(b=>b&&b!=loose);
            Check(nudged,"Another loose block, lower down");
            axis=nudged.transform.right;start=nudged.transform.position;
            int taps=P.HapticCount(VRPlayer.HapticPattern.Tap);
            var outside=start-axis*(BlockTower.Length*.5f+.03f);
            Tower.fingertipOverride[1]=outside;yield return .2f;
            Check(t.TipLive(1),"A fingertip beside the tower comes alive");
            foreach(var x in MoveTip(1,outside,outside+axis*.08f,.05f))yield return x;yield return .3f;
            float pushed=Vector3.Dot(nudged.transform.position-start,axis);
            Tower.fingertipOverride[1]=null;yield return .2f;
            Check(!t.TipLive(1),"Taken away, the fingertip stops pushing");
            Check(pushed>.03f&&nudged.touched,"Pushed with a fingertip, the loose block slides through ("+Mm(pushed)+")");
            Check(P.HapticCount(VRPlayer.HapticPattern.Tap)>taps,"The touch is felt in that hand");
            Check(!t.Fallen&&Untouched()<.004f,"Nothing else moved more than 4 mm (largest "+Mm(Untouched())+")");

            // ---- A desktop grab with real input
            var facing=t.blocks.Where(b=>!b.touched&&b.level>3).OrderBy(b=>Vector3.Distance(b.transform.position,P.Head.position)).FirstOrDefault(b=>
                Physics.Raycast(P.Head.position,(b.transform.position-P.Head.position).normalized,out var hit,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.transform==b.transform);
            Check(facing,"A block in plain view of the arrival point");
            Aim(facing,facing.transform.position);Press(true);yield return .3f;
            Check(P.IsHolding(facing.grab)&&facing.grab.held,"Clicking a block takes hold of it");
            Check(P.hint.text.Contains("Ease it out"),"The hint says how: ease it out, and let go of one that will not come");
            Press(false);yield return .5f;
            Check(!facing.grab.held&&!P.IsHolding(facing.grab),"Letting go puts it down");
            Check(!t.Fallen,"The tower is still standing");

            // ---- Down it comes
            int bestBefore=t.Best;
            foreach(var b in t.blocks.Where(b=>b.level>=12))b.body.AddForce(new Vector3(1.6f,0,.4f),ForceMode.VelocityChange);
            for(int i=0;i<30&&!t.Fallen;i++)yield return .1f;
            Check(t.Fallen,"Knocked hard, the tower comes down and the game knows");
            Check(t.score.text.Contains("IT FELL")&&t.Best==bestBefore,"The sign says so, and keeps the best ("+t.score.text+")");
            Check(P.hint.text.Contains("Down it comes")&&P.hint.text.Contains("NEW TOWER"),"It is said kindly, with what to do next");
            yield return 2f;

            // ---- NEW TOWER
            int towers=t.Towers;var heights=t.blocks.Select(b=>b.height).ToArray();
            foreach(var x in Click(newTower))yield return x;
            Check(t.Towers==towers+1&&!t.Fallen&&t.OnTop==0&&t.blocks.All(b=>!b.touched),"NEW TOWER stacks it again");
            Check(!t.blocks.Select(b=>b.height).SequenceEqual(heights),"In a new order, so the loose blocks are somewhere new");
            yield return 2f;
            Check(t.blocks.Max(b=>b.Drift)<.002f,"And it stands still (largest drift "+Mm(t.blocks.Max(b=>b.Drift))+")");

            // ---- Leaving and coming back
            D.Travel(0);yield return 1f;
            Check(Mathf.Abs(Time.fixedDeltaTime-rate)<1e-6f&&BlockTower.Playing==null,"Leaving the roof puts physics back to its usual rate");
            D.Travel(RooftopRoom.Room);yield return 1.5f;
            Check(BlockTower.Playing==t&&!t.Fallen&&t.blocks.Max(b=>b.Drift)<.002f,"Coming back, the tower is as it was left");
            D.Travel(0);yield return 1f;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            if(Time.time<deadline)return;
            try{if(!run.MoveNext()){Finish("Passed");return;}deadline=Time.time+run.Current;}catch(Exception e){Finish("Failed: "+e.Message);}
        }
        static void Finish(string result)
        {
            if(run==null)return;run=null;Status=result;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            if(D)D.saveEnabled=false;
            if(hand)UnityEngine.Object.Destroy(hand.gameObject);
            if(D&&D.rooftop&&D.rooftop.tower)D.rooftop.tower.fingertipOverride=new Vector3?[2];
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Tower-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The rooftop's tower of blocks: what it is made of and the physics it plays with, standing still, a loose block eased out and laid on top, a load-bearing block that will not come, a fingertip nudge, a desktop grab, the tower coming down, NEW TOWER, leaving and coming back."}.Concat(lines));Debug.Log("Tower validation: "+Status);
        }
    }
}
