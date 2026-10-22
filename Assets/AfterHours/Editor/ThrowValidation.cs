using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Regression test for throwing the boxes into the truck, for the hands-or-controllers wording that follows what the
    // headset reports, for the Quest 2 profile, and for foveated rendering. The headset itself cannot be driven from here, so the throws are made
    // with the same player code the headset uses (switched on for the desktop), moving the player quickly and letting go.
    public static class ThrowValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;
        static UniversalRenderPipelineAsset pipeline;static float savedScale,savedShadow;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(pipeline){savedScale=pipeline.renderScale;savedShadow=pipeline.shadowDistance;}
            Wording.Reset();
            mouse=InputSystem.AddDevice<Mouse>("ThrowValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("ThrowValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static string Name(Grabbable box)=>box.label.Replace("\n"," ");

        // ---- Throw helpers
        static readonly List<string> watchTrace=new List<string>();
        class Flight{public bool thrown,sending;public float peak;}
        static Flight flight=new Flight();
        // Watches a box for a while, noting whether it was thrown, how fast it flew, and whether it glided into place.
        static IEnumerable<float> Watch(Grabbable box,float seconds,Func<bool> until=null)
        {
            flight=new Flight();watchTrace.Clear();float end=Time.time+seconds;
            while(Time.time<end)
            {
                if(box.thrown){flight.thrown=true;flight.peak=Mathf.Max(flight.peak,box.Body.linearVelocity.magnitude);}
                if(box.sending)flight.sending=true;
                if(box.processed&&watchTrace.Count<24)watchTrace.Add("t"+Time.time.ToString("0.00")+" sending "+(box.sending?1:0)+" at "+box.transform.position.ToString("0.00"));
                if(until!=null&&until())break;
                yield return 0;
            }
        }
        // Stands the player two metres behind a box, points at it and takes hold with the pointer.
        static IEnumerable<float> Take(Grabbable box)
        {
            HandHome();var at=box.transform.position;
            P.Place(at+new Vector3(0,.04f-at.y,-1.8f),0);yield return .15f;
            P.Head.rotation=Quaternion.LookRotation(box.transform.position-P.Head.position);yield return .05f;
            Press(true);yield return .25f;
            Check(box.held,"The pointer takes "+Name(box));
            P.Head.rotation=Quaternion.identity;yield return .05f;
        }
        // Moves the player, and the box in its hand, at a steady speed for a moment. Frames are timed by the game clock, not the wall clock.
        // The desktop carry point is where a held box sits, a little ahead of the head. A hand throwing is simulated by moving it, which is
        // what a hand does in the headset: the body stays still and the hand sweeps forward. (Shifting the whole player does not work here:
        // the character controller puts the player back where it last stood on the next frame.)
        static Transform Grip=>P.Head.Find("Desktop carry point");
        static Vector3 gripHome=new Vector3(0,-.25f,1.35f);
        static string flingTrace="";
        static IEnumerable<float> Fling(Vector3 direction,float speed,float duration=.3f)
        {
            var grip=Grip;direction.Normalize();var local=grip.parent.InverseTransformDirection(direction);float t=0;int frames=0;var trace=new System.Text.StringBuilder();
            while(t<duration)
            {
                float dt=Mathf.Max(Time.deltaTime,.001f);var was=grip.position;grip.localPosition+=local*speed*dt;
                if(frames<2||t+dt>=duration)trace.Append(" [frame "+frames+": dt "+dt.ToString("0.000")+", hand z "+was.z.ToString("0.000")+" -> "+grip.position.z.ToString("0.000")+"]");
                t+=dt;frames++;yield return 0;
            }
            flingTrace="frames "+frames+trace;
        }
        static void HandHome(){var g=Grip;if(g)g.localPosition=gripHome;}
        static Vector3 Flat(Vector3 v){v.y=0;return v;}
        static void Release(){Press(false);}
        // The simulated flight of a launch: where it crosses a height on the way down.
        static Vector3 LandsAt(Vector3 from,Vector3 velocity,float height)
        {
            for(float t=.01f;t<6;t+=.002f)
            {
                var a=Throwing.Position(from,velocity,t);var b=Throwing.Position(from,velocity,t+.002f);
                if(velocity.y+Physics.gravity.y*t<0&&a.y>=height&&b.y<height)return a;
            }
            return new Vector3(float.NaN,float.NaN,float.NaN);
        }

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            var bayZone=D.rooms[2].root.GetComponentInChildren<DropZone>(true);var bayTarget=D.rooms[2].root.GetComponentInChildren<SendTarget>(true);
            var boxes=D.rooms[2].root.GetComponentsInChildren<Grabbable>(true).OrderBy(b=>b.transform.position.x).ToArray();
            Check(boxes.Length==5&&bayZone&&bayTarget,"The heavy room has five boxes, a truck bay and a send target");

            // ---- Which things can be thrown
            var everything=D.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Grabbable>(true)).ToArray();
            Check(boxes.All(b=>b.throwable)&&D.shredder.pages.All(p=>p.throwable)&&everything.Count(g=>g.throwable)==10,"Only the five boxes in the heavy room and the five résumé pages can be thrown");
            Check(!P.CanThrow&&!VRPlayer.ThrowOnDesktop,"On the desktop nothing is thrown unless a test switches throwing on");

            // ---- The throw: speed, direction and the pull toward the truck
            Check(!Throwing.IsThrow(new Vector3(.5f,0,.5f))&&!Throwing.IsThrow(new Vector3(0,3,.4f))&&Throwing.IsThrow(new Vector3(0,0,2))&&Throwing.IsThrow(new Vector3(1.4f,1.4f,0)),"A slow hand, or one moving only up or down, is not a throw; a brisk sideways flick is");
            Check(Mathf.Abs(Throwing.Angle(new Vector3(0,0,3),Vector3.zero,new Vector3(0,0,7)))<.01f&&Mathf.Abs(Throwing.Angle(new Vector3(3,1,3),Vector3.zero,new Vector3(0,0,7))-45)<.01f&&Throwing.Angle(Vector3.zero,Vector3.zero,new Vector3(0,0,7))==180,"The sideways angle of a throw to the truck is measured on the floor plan");
            Check(Mathf.Abs(Throwing.Strength(0)-Throwing.AssistStrength)<.001f&&Mathf.Abs(Throwing.Strength(Throwing.FullAssistAngle)-Throwing.AssistStrength)<.001f&&Throwing.Strength(Throwing.NoAssistAngle)<.001f&&Throwing.Strength(40)<Throwing.Strength(30)&&Throwing.Strength(30)>0,"The pull is strongest close to straight at the truck and fades to nothing by "+Throwing.NoAssistAngle+" degrees");
            var landing=bayTarget.LandingPoint;var bay=new Bounds(bayZone.transform.position,bayZone.transform.lossyScale);
            foreach(var from in new[]{landing+new Vector3(0,-.05f,-7.4f),landing+new Vector3(-2.8f,-.05f,-6.8f),landing+new Vector3(2.8f,-.05f,-6.8f),landing+new Vector3(0,-.4f,-8.6f)})
            {
                var v=Throwing.Ballistic(from,landing);var arrive=Throwing.Position(from,v,Throwing.FlightTime(new Vector3(landing.x-from.x,0,landing.z-from.z).magnitude));
                Check(Vector3.Distance(arrive,landing)<.01f&&v.magnitude<Throwing.MaxSpeed,"A lofted flight from "+(landing.z-from.z).ToString("0.0")+" m away arrives at the truck at "+v.magnitude.ToString("0.0")+" m/s");
            }
            int tried=0,landed=0;var misses=new List<string>();
            foreach(var from in new[]{landing+new Vector3(0,-.05f,-6.3f),landing+new Vector3(-2.8f,-.05f,-6.3f),landing+new Vector3(2.8f,-.05f,-6.3f),landing+new Vector3(1.2f,-.3f,-7.6f)})
            {
                var towards=new Vector3(landing.x-from.x,0,landing.z-from.z).normalized;
                foreach(float off in new[]{-20f,-10f,0f,10f,20f})foreach(float speed in new[]{2f,3.5f,5f,8f})foreach(float up in new[]{-1f,0f,1.5f,3f})
                {
                    var hand=Quaternion.Euler(0,off,0)*towards*speed+Vector3.up*up;
                    if(!Throwing.IsThrow(hand))continue;
                    var launch=Throwing.Launch(hand,from,landing,out bool helped);var at=LandsAt(from,launch,landing.y);tried++;
                    bool inside=helped&&at.x>bay.min.x+.1f&&at.x<bay.max.x-.1f&&at.z>bay.min.z+.1f&&at.z<bay.max.z-.1f;
                    if(inside)landed++;else misses.Add(speed+" m/s, "+off+" degrees, up "+up+" from "+from.x.ToString("0.0")+" lands "+at.x.ToString("0.0")+","+at.z.ToString("0.0"));
                }
            }
            Check(tried>=150&&landed==tried,"Every throw within 20 degrees of the truck, from "+tried+" tries (2 to 8 m/s, high and low, from either end of the row), lands inside the bay"+(misses.Count>0?" / first miss: "+misses[0]+" (bay x "+bay.min.x.ToString("0.0")+" to "+bay.max.x.ToString("0.0")+", z "+bay.min.z.ToString("0.0")+" to "+bay.max.z.ToString("0.0")+")":""));
            var side=new Vector3(5,0,0);var far=Throwing.Launch(side,landing+new Vector3(0,0,-7),landing,out bool farHelped);
            Check(!farHelped&&Vector3.Distance(far,side*Throwing.Boost)<.001f,"A throw 90 degrees away from the truck is left alone");
            var hard=Throwing.Launch(new Vector3(0,0,50),landing+new Vector3(0,0,-7),null,out bool none);
            Check(!none&&hard.magnitude<=Throwing.MaxSpeed+.001f,"With nothing to aim at, a throw keeps the hand's direction and is never faster than "+Throwing.MaxSpeed+" m/s");

            // ---- Hands and controllers: what the headset says decides the words
            Check(Wording.Kind==InputKind.Hands&&!Wording.Controllers,"The game starts in hands wording");
            Check(VRPlayer.SeenInput(OVRInput.Controller.Hands,false,true)==InputKind.Hands&&VRPlayer.SeenInput(OVRInput.Controller.LHand,false,false)==InputKind.Hands,"The headset reporting hands means hands, whatever else is tracked");
            Check(VRPlayer.SeenInput(OVRInput.Controller.Touch,true,false)==InputKind.Controllers&&VRPlayer.SeenInput(OVRInput.Controller.RTouch,false,false)==InputKind.Controllers,"The headset reporting controllers means controllers");
            Check(VRPlayer.SeenInput(OVRInput.Controller.None,true,false)==InputKind.Hands&&VRPlayer.SeenInput(OVRInput.Controller.None,false,true)==InputKind.Controllers&&VRPlayer.SeenInput(OVRInput.Controller.None,true,true)==null&&VRPlayer.SeenInput(OVRInput.Controller.None,false,false)==null,"With no answer from the headset, only one kind of tracking decides, and tracking both or neither decides nothing");
            Wording.Reset();
            Wording.Observe(null,0);Check(Wording.Kind==InputKind.Hands,"Nothing seen leaves hands wording");
            Wording.Observe(InputKind.Controllers,1);Check(Wording.Controllers,"The first thing the headset reports is believed at once");
            Wording.Observe(InputKind.Hands,2);Wording.Observe(InputKind.Hands,2.3f);Check(Wording.Controllers,"A change that has lasted less than "+Wording.SwitchDelay+" seconds is ignored");
            Wording.Observe(InputKind.Controllers,2.4f);Wording.Observe(InputKind.Hands,2.5f);Wording.Observe(InputKind.Hands,3.0f);Check(Wording.Controllers,"A change that flickers back resets the wait");
            Wording.Observe(InputKind.Hands,3.2f);Wording.Observe(InputKind.Hands,3.2f+Wording.SwitchDelay+.01f);Check(Wording.Kind==InputKind.Hands,"A change that holds for "+Wording.SwitchDelay+" seconds switches the wording back to hands");
            Check(VRPlayer.ControllerGrab(.8f,0)&&VRPlayer.ControllerGrab(0,.8f)&&!VRPlayer.ControllerGrab(.5f,.5f),"With controllers, the grip or the trigger holds an object");

            // Every sign with two wordings keeps the controllers' words free of hands and the hands' words free of controllers.
            var signs=UnityEngine.Object.FindObjectsByType<InputText>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var signNames=signs.Select(x=>x.name).ToArray();var missingSigns=new[]{"Move instructions","Comfort note","Menu tip"}.Where(n=>!signNames.Contains(n)).ToArray();
            Check(missingSigns.Length==0,"The lobby signs and the menu tip have a wording for each kind of input ("+signs.Length+" signs"+(missingSigns.Length>0?" / missing: "+string.Join(", ",missingSigns)+" / found: "+string.Join(", ",signNames):"")+")");
            string[] handsOnly={"pinch","Pinch","PINCH","palm","Palm"},controllersOnly={"trigger","Trigger","grip","Grip","squeeze","Squeeze","SQUEEZE","B / Y","Menu button","menu button","stick"};
            foreach(var sign in signs)
            {
                Check(!handsOnly.Any(w=>sign.controllers.Contains(w)),"Controller wording on '"+sign.name+"' says nothing of pinching or palms");
                Check(!controllersOnly.Any(w=>sign.hands.Contains(w)),"Hands wording on '"+sign.name+"' says nothing of triggers, sticks or B / Y");
            }
            var moveSign=signs.First(s=>s.name=="Move instructions").GetComponent<TMP_Text>();
            Wording.Set(InputKind.Hands);yield return .3f;
            Check(moveSign.text.Contains("PINCH")&&!moveSign.text.Contains("SQUEEZE"),"The lobby board says pinch to a player using hands");
            Wording.Set(InputKind.Controllers);yield return .3f;
            Check(moveSign.text.Contains("SQUEEZE")&&!moveSign.text.Contains("PINCH"),"The lobby board follows the player to controllers without being rebuilt");
            // Nothing else in the world talks about pinching unless it is one of those two-wording signs.
            Wording.Set(InputKind.Hands);
            var stray=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(t=>t.text.IndexOf("pinch",StringComparison.OrdinalIgnoreCase)>=0&&!t.GetComponent<InputText>()&&!t.transform.IsChildOf(D.intro.transform)&&t!=P.hint).Select(t=>t.name).ToArray();
            Check(stray.Length==0,"No other sign mentions pinching"+(stray.Length>0?" / found: "+string.Join(", ",stray):""));
            // The first-visit guide.
            Wording.Set(InputKind.Hands);
            Check(IntroGuide.Body(1,false).Contains("thumb")&&IntroGuide.Body(2,false).Contains("palm")&&IntroGuide.Title(1,false)=="Point and pinch","The guide teaches hands to point and pinch, and to open the menu with a palm pinch");
            Wording.Set(InputKind.Controllers);
            Check(IntroGuide.Body(1,false).Contains("trigger")&&!IntroGuide.Body(1,false).Contains("thumb")&&IntroGuide.Body(2,false).Contains("menu button")&&!IntroGuide.Body(2,false).Contains("palm")&&IntroGuide.Title(1,false)=="Point and squeeze","The guide teaches controllers to point and squeeze the trigger, and to press the menu button");
            Check(IntroGuide.Body(1,true).Contains("mouse")&&IntroGuide.Title(1,true)=="Point and pinch","The desktop guide still teaches the mouse");
            Wording.Set(InputKind.Hands);

            // ---- Messages say the right thing for the input, and mention throwing
            D.Travel(2);yield return .95f;
            Check(D.currentRoom==2&&P.hint.text.Contains("Pinch a box")&&P.hint.text.Contains("throw it"),"The heavy room says to pinch a box, and that it can be thrown");
            D.Travel(0);yield return .95f;Wording.Set(InputKind.Controllers);D.Travel(2);yield return .95f;
            Check(P.hint.text.Contains("Squeeze the trigger on a box")&&!P.hint.text.Contains("Pinch"),"With controllers it says to squeeze the trigger instead");
            Wording.Set(InputKind.Hands);

            // ---- The held box remembers how the hand moved
            var test=boxes[2];var holder=new GameObject("Test hand").transform;holder.position=test.transform.position+new Vector3(0,0,-1.2f);
            Check(test.Take(holder),"A test hand takes the middle box");yield return .2f;
            Check(test.HandVelocity().magnitude<.3f,"A hand held still gives a throw of nothing ("+test.HandVelocity().magnitude.ToString("0.00")+" m/s)");
            float moved=0;while(moved<.4f){float dt=Mathf.Max(Time.deltaTime,.001f);holder.position+=new Vector3(.3f,.4f,1f).normalized*4f*dt;moved+=dt;yield return 0;}
            var measured=test.HandVelocity();
            Check(measured.magnitude>3.2f&&measured.magnitude<4.8f&&Vector3.Angle(measured,new Vector3(.3f,.4f,1f))<8f,"A hand moving at 4 m/s is measured at "+measured.magnitude.ToString("0.0")+" m/s in the right direction");
            test.Release();UnityEngine.Object.Destroy(holder.gameObject);yield return .2f;test.ResetObject();yield return .3f;

            // ---- The real thing: a hand moves, lets go, and the box flies
            VRPlayer.ThrowOnDesktop=true;
            var slots=bayZone.slots;int accepted=D.rooms[2].accepted;
            // 1. A brisk throw straight at the truck.
            var b=boxes[2];
            foreach(var t in Take(b))yield return t;
            Check(P.hint.text.Contains("Or throw it."),"Picking up a box says it can be thrown");
            var before=b.transform.position;var playerBefore=P.transform.position;
            foreach(var t in Fling(Vector3.forward,4.5f))yield return t;
            lines.Add("INFO: the move: "+flingTrace);
            lines.Add("INFO: before letting go the box had moved "+Vector3.Distance(before,b.transform.position).ToString("0.00")+" m with the player moving "+Vector3.Distance(playerBefore,P.transform.position).ToString("0.00")+" m (it should be about 1.3 m and 0), held "+b.held+", hand speed "+b.HandVelocity().magnitude.ToString("0.0")+" m/s, throwing allowed "+P.CanThrow+", game frame time "+Time.deltaTime.ToString("0.000"));
            Release();
            foreach(var t in Watch(b,5,()=>b.processed&&!b.sending))yield return t;
            lines.Add("INFO: after letting go: thrown seen "+flight.thrown+", sending seen "+flight.sending+", peak speed "+flight.peak.ToString("0.0")+", processed "+b.processed+", held "+b.held);
            Check(flight.thrown&&flight.peak>5f,"The box was thrown and flew at "+flight.peak.ToString("0.0")+" m/s");
            Check(b.processed&&D.rooms[2].accepted==accepted+1,"A throw straight at the truck is caught and counted once");
            lines.Add("INFO: frames after the catch: "+string.Join(" | ",watchTrace));
            lines.Add("INFO: the caught box rests at "+b.transform.position.ToString("0.00")+"; places in the truck: "+string.Join(" ",slots.Select(s=>s.position.ToString("0.00")))+"; distances "+string.Join(" ",slots.Select(s=>Vector3.Distance(b.transform.position,s.position).ToString("0.00"))));
            bool glided=flight.sending;var restedAt=b.transform.position;
            foreach(var t in Watch(b,.3f))yield return t;
            lines.Add("INFO: and a moment later: "+string.Join(" | ",watchTrace.Take(6))+"; it was "+Vector3.Distance(restedAt,slots[0].position).ToString("0.000")+" m from its place when the glide ended");
            Check(glided&&Vector3.Distance(b.transform.position,slots[0].position)<.02f,"The caught box glides into its place in the truck");
            Check(!b.thrown&&b.Body.isKinematic,"It stays where it was put");
            yield return .5f;

            // 2. Throws from the ends of the row, angled a little off.
            foreach(var (index,offset,place) in new[]{(0,-14f,1),(4,15f,2)})
            {
                b=boxes[index];
                // Aimed a little to one side of the line from the box to the truck.
                var line=Vector3.ProjectOnPlane(landing-b.transform.position,Vector3.up).normalized;
                foreach(var t in Take(b))yield return t;
                foreach(var t in Fling(Quaternion.Euler(0,offset,0)*line,4.5f))yield return t;
                Release();
                foreach(var t in Watch(b,5,()=>b.processed&&!b.sending))yield return t;
                lines.Add("INFO: "+Name(b)+" rests at "+b.transform.position.ToString("0.00")+", thrown seen "+flight.thrown+", processed "+b.processed+", peak "+flight.peak.ToString("0.0"));
                Check(flight.thrown&&b.processed,Name(b)+" thrown "+Math.Abs(offset)+" degrees off line is still caught by the truck");
                Check(Vector3.Distance(b.transform.position,slots[place].position)<.02f,Name(b)+" settles into the next place in the truck");
                yield return .4f;
            }
            Check(D.rooms[2].accepted==accepted+3&&!D.rooms[2].complete,"Three thrown boxes are three boxes given a place");

            // 3. A throw the wrong way is left alone, and the box comes back with no penalty.
            b=boxes[1];var home=b.transform.position;
            foreach(var t in Take(b))yield return t;
            foreach(var t in Fling(Vector3.right,4.5f))yield return t;
            Release();
            foreach(var t in Watch(b,1.5f))yield return t;
            Check(flight.thrown&&flight.peak>4f&&!b.processed,"A box thrown sideways flies off to the side ("+flight.peak.ToString("0.0")+" m/s) and is not counted");
            foreach(var t in Watch(b,12,()=>b.sending))yield return t;
            yield return .15f;
            Check(b.sending&&!b.processed&&P.hint.gameObject.activeSelf&&P.hint.text.Contains("Not quite"),"Once it has stopped short of the truck it is sent home, with a gentle message");
            foreach(var t in Watch(b,4,()=>!b.sending&&!b.thrown&&Vector3.Distance(b.transform.position,home)<.1f))yield return t;
            yield return .3f;
            Check(!b.processed&&!b.thrown&&Vector3.Distance(b.transform.position,home)<.1f&&!b.Body.isKinematic,"The missed box is back on its stand, ready to pick up again");
            Check(D.rooms[2].accepted==accepted+3,"A miss costs nothing and counts nothing");

            // 4. Lifting a box and letting go is not a throw; pointing at the truck still sends it there.
            foreach(var t in Take(b))yield return t;
            P.Head.rotation=Quaternion.LookRotation(bayTarget.LandingPoint-P.Head.position);yield return .4f;
            foreach(var t in Fling(Vector3.up,4f,.2f))yield return t;
            Release();
            foreach(var t in Watch(b,5,()=>b.processed&&!b.sending))yield return t;
            Check(!flight.thrown&&b.processed&&D.rooms[2].accepted==accepted+4,"A box lifted quickly and let go while pointing at the truck is sent, not thrown");
            yield return .4f;

            // 5. With throwing off (the desktop), the same movement only sends.
            VRPlayer.ThrowOnDesktop=false;b=boxes[3];
            foreach(var t in Take(b))yield return t;
            Check(!P.hint.text.Contains("Or throw it."),"With throwing off, picking up a box does not offer to throw it");
            P.Head.rotation=Quaternion.LookRotation(bayTarget.LandingPoint-P.Head.position);yield return .4f;
            foreach(var t in Fling(Vector3.forward,4.5f))yield return t;
            Release();
            foreach(var t in Watch(b,5,()=>b.processed&&!b.sending))yield return t;
            Check(!flight.thrown&&b.processed&&D.rooms[2].accepted==accepted+5,"With throwing off, the same movement points and sends exactly as before");
            Check(D.rooms[2].complete&&D.rooms[2].accepted==5&&boxes.All(x=>x.processed),"Thrown and sent boxes together finish the chapter, each counted once");
            // A box placed by the truck during a physics step is drawn there over a frame or two; look once it has settled.
            yield return .3f;
            lines.Add("INFO: the boxes: "+string.Join("; ",boxes.Select(x=>Name(x)+" at "+x.transform.position.ToString("0.00")+" processed "+x.processed+" sending "+x.sending+" thrown "+x.thrown+" moving "+(x.Body.isKinematic?"no":x.Body.linearVelocity.magnitude.ToString("0.00")))));
            lines.Add("INFO: boxes in each place: "+string.Join(" ",slots.Select(s=>boxes.Count(x=>Vector3.Distance(x.transform.position,s.position)<.02f))));
            Check(slots.Select(s=>boxes.Count(x=>Vector3.Distance(x.transform.position,s.position)<.02f)).All(n=>n==1),"Every place in the truck holds exactly one box");

            // ---- Résumé pages: thrown into the shredder, and an easter egg: a page thrown onto the floor bursts into confetti
            VRPlayer.ThrowOnDesktop=true;
            D.Travel(6);yield return .95f;
            var egg=D.shredder.egg;var sheet=D.shredder.pages[1];var sheetHome=sheet.transform.position;int popped=egg.bursts;
            Check(D.shredder.pages.All(p=>p.throwable&&!p.tossable)&&D.ambush.papers.Length==6&&D.ambush.papers.All(p=>p.tossable&&!p.throwable)&&everything.Count(g=>g.tossable&&!g.GetComponent<Mug>())==6&&everything.Count(g=>g.tossable&&g.GetComponent<Mug>())==5,"Only the six ambush papers and the five kitchen mugs are tossed; the résumé pages are thrown, helped toward the shredder");
            foreach(var t in Take(sheet))yield return t;
            Check(P.hint.text.Contains("Or throw it."),"Picking up a page says it can be thrown");
            // Stand clear of the desk with the page in hand, and flick it down at the floor.
            // (Place would let go of the page, so the player is shifted with it in hand.)
            var clear=D.rooms[6].root.transform.TransformPoint(new Vector3(3.6f,.04f,-2.2f));P.Shift(new Vector3(clear.x,.04f,clear.z)-P.transform.position);P.Head.rotation=Quaternion.identity;yield return .2f;
            Check(sheet.held,"The page stays in hand when the player steps clear of the desk");
            foreach(var t in Fling(new Vector3(.3f,-.5f,1f),3.5f))yield return t;
            Release();
            foreach(var t in Watch(sheet,2.5f,()=>egg.bursts>popped))yield return t;
            lines.Add("INFO: the page flick: thrown seen "+flight.thrown+", peak "+flight.peak.ToString("0.0")+" m/s, bursts "+(egg.bursts-popped)+", active "+sheet.gameObject.activeSelf+", held "+sheet.held+", at "+sheet.transform.position.ToString("0.00")+" (last burst "+egg.lastAt.ToString("0.00")+"), room "+D.currentRoom);
            Check(flight.thrown&&egg.bursts==popped+1&&!sheet.gameObject.activeSelf,"A page flicked at the floor flies off the hand and bursts into confetti");
            foreach(var t in Watch(sheet,3,()=>sheet.gameObject.activeSelf))yield return t;
            yield return .4f;
            lines.Add("INFO: the page after the toss: active "+sheet.gameObject.activeSelf+", returns "+egg.returns+", "+Vector3.Distance(sheet.transform.position,sheetHome).ToString("0.00")+" m from home, processed "+sheet.processed+", accepted "+D.rooms[6].accepted);
            Check(sheet.gameObject.activeSelf&&!sheet.processed&&Vector3.Distance(sheet.transform.position,sheetHome)<.3f&&D.rooms[6].accepted==0,"It is back on its stand, and nothing is counted");
            // Pointing at the shredder and letting go still sends the page in, even with the hand moving.
            foreach(var t in Take(sheet))yield return t;
            var slot=D.Targets.First(x=>x.GetComponent<Shredder>());
            P.Head.rotation=Quaternion.LookRotation(slot.LandingPoint-P.Head.position);yield return .4f;
            foreach(var t in Fling(Vector3.up,4f,.2f))yield return t;
            Release();
            foreach(var t in Watch(sheet,5,()=>sheet.processed&&!sheet.sending))yield return t;
            Check(!flight.thrown&&sheet.processed&&egg.bursts==popped+1,"A page let go while pointing at the shredder (the hand only lifting) is sent in, not thrown");
            // Thrown at the shredder, a page is helped in and shredded like one sent there.
            D.RestartCurrentRoom();yield return .3f;
            Check(!sheet.processed&&sheet.gameObject.activeSelf&&D.rooms[6].accepted==0&&D.shredder.shredded==0,"TRY AGAIN puts the page back on its stand");
            var shredderSlot=D.Targets.First(x=>x.GetComponent<Shredder>());
            Check(Vector3.Distance(Flat(D.rooms[6].spawn.position),Flat(shredderSlot.LandingPoint))<2.5f&&Flat(shredderSlot.LandingPoint-D.rooms[6].spawn.position).z<.5f,"The shredder stands on the player's side of the desk, within a couple of steps of the arrival point");
            foreach(var t in Take(sheet))yield return t;
            var toward=Flat(shredderSlot.LandingPoint-sheet.transform.position).normalized+Vector3.up*.3f;
            foreach(var t in Fling(toward,4f))yield return t;
            Release();
            foreach(var t in Watch(sheet,4,()=>sheet.processed))yield return t;
            lines.Add("INFO: the page thrown at the shredder: thrown seen "+flight.thrown+", peak "+flight.peak.ToString("0.0")+" m/s, processed "+sheet.processed+", at "+sheet.transform.position.ToString("0.00")+", accepted "+D.rooms[6].accepted);
            Check(flight.thrown&&sheet.processed&&egg.bursts==popped+1,"A page thrown at the shredder flies in and is taken");
            yield return 1.6f;
            Check(D.rooms[6].accepted==1&&D.shredder.shredded==1&&!sheet.gameObject.activeSelf,"It is shredded and counted once");
            D.RestartCurrentRoom();yield return .2f;

            // ---- Mugs in the kitchen: thrown or dropped from carrying height they break and come back; set down gently, or sent under the spout, they never do
            D.Travel(3);yield return .95f;
            var kit=D.kitchen;var kitchenRoot=D.rooms[3].root.transform;
            var tealMug=kit.mugs.First(m=>m.item.label=="Teal mug");var coralMug=kit.mugs.First(m=>m.item.label=="Coral mug");
            var tealItem=tealMug.item;var coralItem=coralMug.item;var tealHome=tealItem.transform.position;var coralHome=coralItem.transform.position;
            var spoutPoint=D.Targets.First(x=>x.category=="mug"&&x.room==3);
            var clearSpot=kitchenRoot.TransformPoint(new Vector3(-.3f,.04f,.8f));
            Check(kit.mugs.Length==5&&kit.mugs.All(m=>m.item.tossable&&!m.item.throwable&&m.item.category=="mug")&&kit.mugs.Count(m=>m.orange)==1,"The kitchen has five mugs that can be carried, sent and tossed, and exactly one is the orange one");
            Check(kit.mugs.All(m=>m.item.Body&&m.item.Body.maxDepenetrationVelocity<=1.5f),"Mugs pressed into each other part gently ("+string.Join(", ",kit.mugs.Select(m=>m.item.Body.maxDepenetrationVelocity.ToString("0.0")))+")");
            int shattersBefore=kit.shatters;
            // 1. Picking a mug up warns about it, and a flick at the floor breaks it.
            foreach(var t in Take(tealItem))yield return t;
            Check(P.hint.text.Contains("a mug will break"),"Picking up a mug says that throwing it will break it");
            P.Shift(new Vector3(clearSpot.x,.04f,clearSpot.z)-P.transform.position);P.Head.rotation=Quaternion.Euler(20,-90,0);yield return .3f;
            Check(tealItem.held,"The mug stays in hand when the player steps clear of the counter");
            foreach(var t in Fling(new Vector3(0,-.6f,1f),4f,.2f))yield return t;
            Release();
            foreach(var t in Watch(tealItem,3,()=>kit.shatters>shattersBefore))yield return t;
            lines.Add("INFO: the mug flick: thrown seen "+flight.thrown+", peak "+flight.peak.ToString("0.0")+" m/s, shatters "+(kit.shatters-shattersBefore)+", active "+tealItem.gameObject.activeSelf+", room "+D.currentRoom);
            Check(flight.thrown&&kit.shatters==shattersBefore+1&&tealMug.breaks==1,"A mug flicked at the floor flies off the hand and breaks, once");
            Check(!tealItem.gameObject.activeSelf&&kit.IsAway(tealMug)&&!tealItem.held,"The broken mug is out of the room for a moment and out of the hand");
            Check((kit.shardsFloor.isPlaying||kit.shardsCounter.isPlaying)&&kit.dust.isPlaying,"It bursts into shards and a puff of dust");
            yield return KitchenRoom.ReturnAfter+.6f;
            Check(tealItem.gameObject.activeSelf&&!kit.IsAway(tealMug)&&tealMug.level==0&&!tealItem.held&&Vector3.Distance(tealItem.transform.position,tealHome)<.15f,"A moment later the mug is back, whole, on its shelf");
            int broken=kit.shatters;
            // 2. A mug put down from a hand's height does not break.
            var dropAt=coralHome+Vector3.up*.1f;
            coralItem.Body.position=dropAt;coralItem.transform.position=dropAt;coralItem.Body.linearVelocity=Vector3.zero;coralItem.Body.angularVelocity=Vector3.zero;
            yield return 1.5f;
            Check(kit.shatters==broken&&coralItem.gameObject.activeSelf&&Vector3.Distance(coralItem.transform.position,coralHome)<.15f,"A mug set down from a hand's height does not break");
            // 3. Lifting a mug and letting go while pointing at the spout sends it there, even with the hand moving; it does not break.
            foreach(var t in Take(tealItem))yield return t;
            P.Head.rotation=Quaternion.LookRotation(spoutPoint.LandingPoint-P.Head.position);yield return .4f;
            foreach(var t in Fling(Vector3.up,4f,.2f))yield return t;
            Release();
            foreach(var t in Watch(tealItem,6,()=>flight.sending&&!tealItem.sending))yield return t;
            yield return 1.2f;
            lines.Add("INFO: the mug sent to the spout rests at "+tealItem.transform.position.ToString("0.00")+" ("+Vector3.Distance(tealItem.transform.position,spoutPoint.LandingPoint).ToString("0.00")+" m from the landing point), thrown seen "+flight.thrown+", sending seen "+flight.sending);
            Check(!flight.thrown&&flight.sending&&kit.shatters==broken&&tealItem.gameObject.activeSelf,"A mug let go while pointing at the spout is sent, not thrown, and does not break");
            Check(Vector3.Distance(tealItem.transform.position,spoutPoint.LandingPoint)<.25f&&kit.UnderSpout()==tealMug,"The sent mug rests under the spout");
            // 4. A second mug sent to the spout sends the first one home, so two never end up in each other.
            foreach(var t in Take(coralItem))yield return t;
            P.Head.rotation=Quaternion.LookRotation(spoutPoint.LandingPoint-P.Head.position);yield return .4f;
            foreach(var t in Fling(Vector3.up,4f,.2f))yield return t;
            Release();
            foreach(var t in Watch(coralItem,6,()=>flight.sending&&!coralItem.sending))yield return t;
            yield return 2f;
            Check(kit.shatters==broken&&kit.UnderSpout()==coralMug&&Vector3.Distance(coralItem.transform.position,spoutPoint.LandingPoint)<.25f,"The second mug takes the place under the spout");
            Check(!tealItem.sending&&tealItem.gameObject.activeSelf&&Vector3.Distance(tealItem.transform.position,tealHome)<.15f,"The first mug went back to where it started, unbroken");
            // 5. The pour goes into the mug that was sent, and a full mug let go of at carrying height breaks, splashes, and comes back empty. Nothing is counted against anyone.
            kit.AskForHelp();yield return .2f;
            kit.Brew();yield return .3f;
            Check(coralMug.level==1&&kit.fills==1&&kit.mistakes==0,"The pour goes into the mug that was sent under the spout");
            foreach(var t in Take(coralItem))yield return t;
            P.Shift(new Vector3(clearSpot.x,.04f,clearSpot.z)-P.transform.position);P.Head.rotation=Quaternion.Euler(20,-90,0);yield return .3f;
            Check(coralItem.held,"The full mug stays in hand when the player steps clear of the counter");
            Release();
            foreach(var t in Watch(coralItem,3,()=>kit.shatters>broken))yield return t;
            Check(kit.shatters==broken+1&&coralMug.breaks==1&&!coralItem.gameObject.activeSelf,"A full mug let go of at carrying height breaks");
            Check(kit.splashes.Any(sp=>sp&&sp.gameObject.activeSelf),"A full mug leaves a splash of coffee where it broke");
            yield return KitchenRoom.ReturnAfter+.6f;
            Check(coralItem.gameObject.activeSelf&&coralMug.level==0&&Vector3.Distance(coralItem.transform.position,coralHome)<.15f,"The broken mug comes back empty on its shelf");
            Check(kit.fills==1&&kit.mistakes==0&&D.rooms[3].accepted==0,"Breaking a mug takes nothing away and counts as no mistake");
            VRPlayer.ThrowOnDesktop=false;yield return .5f;

            // ---- Quest 2
            Check(DeviceProfile.Classify(OVRPlugin.SystemHeadset.Oculus_Quest_2)==Headset.Quest2&&DeviceProfile.Classify(OVRPlugin.SystemHeadset.Oculus_Link_Quest_2)==Headset.Quest2,"A Quest 2, or a Quest 2 on a link cable, is recognised");
            Check(DeviceProfile.Classify(OVRPlugin.SystemHeadset.Meta_Quest_3)==Headset.Quest3&&DeviceProfile.Classify(OVRPlugin.SystemHeadset.Meta_Quest_3S)==Headset.Quest3S&&DeviceProfile.Classify(OVRPlugin.SystemHeadset.Meta_Quest_Pro)==Headset.QuestPro&&DeviceProfile.Classify(OVRPlugin.SystemHeadset.None)==Headset.Unknown,"A Quest 3, 3S and Pro are recognised, and no answer stays unknown");
            if(pipeline)
            {
                pipeline.renderScale=1;pipeline.shadowDistance=28;
                DeviceProfile.Apply(Headset.Quest3);
                Check(Mathf.Approximately(pipeline.renderScale,1)&&Mathf.Approximately(pipeline.shadowDistance,28)&&DeviceProfile.Current==Headset.Quest3,"A Quest 3 keeps the full picture");
                DeviceProfile.Undo();
                DeviceProfile.Apply(Headset.Quest2);
                Check(DeviceProfile.Current==Headset.Quest2&&pipeline.renderScale<=DeviceProfile.Quest2RenderScale+.001f&&pipeline.shadowDistance<=DeviceProfile.Quest2ShadowDistance+.001f,"A Quest 2 gets a lighter picture: render scale "+pipeline.renderScale.ToString("0.00")+", shadows to "+pipeline.shadowDistance.ToString("0")+" m");
                DeviceProfile.Undo();
                Check(Mathf.Approximately(pipeline.renderScale,1)&&Mathf.Approximately(pipeline.shadowDistance,28),"The lighter picture can be put back exactly");
                pipeline.renderScale=savedScale;pipeline.shadowDistance=savedShadow;
            }
            else lines.Add("SKIP: No Universal Render Pipeline asset is active, so the Quest 2 picture settings were not exercised.");

            // ---- Foveated rendering: stand-ins take the place of the headset runtime, so the requests it would receive can be read back.
            {
                var fvAsked=new List<string>();bool fvOffered=true,fvBreaks=false;
                Action fvStand=()=>
                {
                    DeviceProfile.FoveationSupported=()=>fvOffered;
                    DeviceProfile.FoveationSetter=(fvLevel,fvDynamic)=>{if(fvBreaks)throw new InvalidOperationException("stand-in runtime failure");fvAsked.Add(fvLevel+(fvDynamic?" dynamic":" fixed"));};
                };
                Check(DeviceProfile.FoveationFor(Headset.Quest2)==OVRManager.FoveatedRenderingLevel.High&&DeviceProfile.FoveationFor(Headset.Quest3)==OVRManager.FoveatedRenderingLevel.Medium&&DeviceProfile.FoveationFor(Headset.Quest3S)==OVRManager.FoveatedRenderingLevel.Medium&&DeviceProfile.FoveationFor(Headset.QuestPro)==OVRManager.FoveatedRenderingLevel.Medium&&DeviceProfile.FoveationFor(Headset.Unknown)==OVRManager.FoveatedRenderingLevel.Off,"The Quest 2 gets the highest foveation ceiling, the newer headsets a middle one, and an unknown headset none");
                DeviceProfile.Undo();fvStand();fvAsked.Clear();
                DeviceProfile.Apply(Headset.Quest2);
                Check(fvAsked.Count==1&&fvAsked[0]=="High dynamic"&&DeviceProfile.FoveationApplied&&DeviceProfile.FoveationDynamic&&DeviceProfile.Foveation==OVRManager.FoveatedRenderingLevel.High&&DeviceProfile.Settled,"A Quest 2 asks for foveated rendering up to High, adapted to the load by the headset: "+string.Join(", ",fvAsked));
                foreach(var newer in new[]{Headset.Quest3,Headset.Quest3S,Headset.QuestPro})
                {
                    DeviceProfile.Undo();fvStand();fvAsked.Clear();
                    DeviceProfile.Apply(newer);
                    Check(fvAsked.Count==1&&fvAsked[0]=="Medium dynamic"&&DeviceProfile.FoveationApplied&&DeviceProfile.Settled,"A "+newer+" asks for foveated rendering up to Medium, adapted to the load: "+string.Join(", ",fvAsked));
                }
                DeviceProfile.Undo();fvStand();fvAsked.Clear();
                DeviceProfile.Apply(Headset.Unknown);
                Check(fvAsked.Count==0&&!DeviceProfile.FoveationApplied&&DeviceProfile.Settled,"A headset that is not recognised is left to the system: nothing is asked of the runtime");
                // The runtime can take a moment to be ready: the request is repeated until it takes it.
                DeviceProfile.Undo();fvStand();fvAsked.Clear();fvOffered=false;
                DeviceProfile.Apply(Headset.Quest3);
                Check(fvAsked.Count==0&&!DeviceProfile.FoveationApplied&&!DeviceProfile.Settled,"While the runtime does not offer foveation yet, nothing is asked and the question stays open");
                fvOffered=true;DeviceProfile.TryApply();
                Check(fvAsked.Count==1&&fvAsked[0]=="Medium dynamic"&&DeviceProfile.FoveationApplied&&DeviceProfile.Settled,"Foveation is applied as soon as the runtime offers it");
                DeviceProfile.TryApply();
                Check(fvAsked.Count==1,"Once applied it is not asked for again");
                // A runtime that never offers it is given up on after a while, and then left alone.
                DeviceProfile.Undo();fvStand();fvAsked.Clear();fvOffered=false;
                DeviceProfile.Apply(Headset.Quest3);
                for(int tries=2;tries<DeviceProfile.FoveationAttempts;tries++)DeviceProfile.TryApply();
                Check(!DeviceProfile.Settled,"One check short of the limit, the question is still open");
                DeviceProfile.TryApply();
                fvOffered=true;DeviceProfile.TryApply();
                Check(DeviceProfile.Settled&&!DeviceProfile.FoveationApplied&&fvAsked.Count==0,"A runtime that never offers foveation is given up on after "+DeviceProfile.FoveationAttempts+" checks and then left alone");
                // A runtime that fails must never stop the game.
                DeviceProfile.Undo();fvStand();fvAsked.Clear();fvBreaks=true;
                DeviceProfile.Apply(Headset.Quest3);
                Check(DeviceProfile.Settled&&!DeviceProfile.FoveationApplied&&fvAsked.Count==0,"A runtime that fails when foveation is requested is logged and left alone, and the game goes on");
                // The editor and the tests never reach the real runtime.
                DeviceProfile.Undo();
                DeviceProfile.Apply(Headset.Quest3);
                Check(!DeviceProfile.FoveationApplied&&DeviceProfile.Foveation==OVRManager.FoveatedRenderingLevel.Off,"Outside a headset build the real runtime is never asked for foveation");
                DeviceProfile.Undo();
                Check(!DeviceProfile.Applied&&!DeviceProfile.FoveationApplied&&!DeviceProfile.FoveationSettled&&DeviceProfile.Foveation==OVRManager.FoveatedRenderingLevel.Off,"Putting the profile back also puts foveation back");
                // The project is set up to take the request: Meta's foveation feature is on for the headset, Unity's own is not (two would compete).
                bool metaFoveation=false,unityFoveation=false;
                var xrSettings=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if(xrSettings!=null)foreach(var xrFeature in xrSettings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())
                {
                    string xrFeatureName=xrFeature.GetType().Name;
                    if(xrFeatureName=="MetaXRFoveationFeature")metaFoveation=xrFeature.enabled;
                    else if(xrFeatureName=="FoveatedRenderingFeature"&&xrFeature.enabled)unityFoveation=true;
                }
                Check(xrSettings!=null&&metaFoveation&&!unityFoveation,"The headset build has Meta's foveation feature on and Unity's own foveation feature off");
            }
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying){Finish("Stopped");return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.017;EditorApplication.Step();
            // Wait in game time so every input edge spans rendered game frames, even when the editor stalls.
            if(Time.time<deadline)return;
            try{if(!run.MoveNext()){Finish("Passed");return;}deadline=Time.time+run.Current;}catch(Exception e){Finish("Failed: "+e.Message);}
        }
        static void Finish(string result)
        {
            if(run==null)return;run=null;Status=result;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.isPaused=false;
            VRPlayer.ThrowOnDesktop=false;Wording.Reset();DeviceProfile.Undo();
            if(pipeline){pipeline.renderScale=savedScale;pipeline.shadowDistance=savedShadow;}
            if(D){D.saveEnabled=false;try{if(D.player)HandHome();}catch(Exception){}}
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Throwing-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"Throwing the boxes into the truck, hands and controller wording that follows the headset, the Quest 2 picture profile, and foveated rendering."}.Concat(lines));Debug.Log("Throwing, controllers and Quest 2 validation: "+Status);
        }
    }
}
