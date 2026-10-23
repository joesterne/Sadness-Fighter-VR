using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AfterHours.Editor
{
    // Regression test for the eighth chapter, The NDA web: the ring of webs and the machete in hand, cutting, the webs growing back
    // (each time a little slower), clearing them all at once, the spider and the unlabelled button that burns it, the hat of flames,
    // coming back, TRY AGAIN, and the machete going back on its hook.
    public static class WebValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        static WebRoom W=>D.web;
        static Transform Root=>D.rooms[WebRoom.Room].root.transform;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("WebValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("WebValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Info(string text){lines.Add("INFO: "+text);}
        static void Aim(Interactable b)
        {
            P.Head.rotation=Quaternion.LookRotation(b.transform.position-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,30,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=b)
                throw new Exception("Pointer target obstructed: "+b.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static IEnumerable<float> Click(Interactable b){Check(b,"The button to press exists");Aim(b);Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Tab(){Queue(keyboard,new KeyboardState(Key.Tab));yield return .1f;Queue(keyboard,new KeyboardState());yield return .15f;}
        static IEnumerable<float> OpenMenu(){for(int i=0;i<2&&!D.menu.IsOpen;i++)foreach(var t in Tab())yield return t;}
        static Interactable Find(ActionKind kind)=>Root.GetComponentsInChildren<Interactable>(true).FirstOrDefault(x=>x.kind==kind);
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            var w=W;var lobby=D.rooms[0].root;
            // ---- What the scene holds
            Check(w&&D.rooms.Length==10&&D.rooms[WebRoom.Room].title=="The NDA web"&&w.director==D,"The game has ten rooms, and room 9 is The NDA web");
            Check(GameDirector.Chapters.Length==8&&GameDirector.IsChapter(WebRoom.Room)&&D.rooms[WebRoom.Room].total==18&&w.webs.Length==18,"The NDA web is the eighth chapter, with eighteen webs to cut");
            var door=lobby.GetComponentsInChildren<Interactable>().FirstOrDefault(x=>x.kind==ActionKind.Travel&&x.value==WebRoom.Room);
            Check(door&&D.lobbyProgress.text.Contains("0 / 8"),"The lobby has an eighth door, and counts eight rooms");
            Check(w.webs.All(x=>x&&x.volume&&x.volume.isTrigger&&x.silk&&x.room==w)&&w.webs.Select(x=>x.index).SequenceEqual(Enumerable.Range(0,18)),"Every web has a trigger to cut and silk to show, so pointers and teleports pass through it");
            Check(w.webs.All(x=>x.silk.GetComponent<Renderer>().sharedMaterial.renderQueue>=3000),"The silk is see-through");
            var fire=Find(ActionKind.KillItWithFire);
            Check(fire&&fire.transform.parent.GetComponentsInChildren<TMP_Text>(true).Length==0&&string.IsNullOrEmpty(fire.prompt),"The button on the wall has no label at all");
            var hat=Outfits.Get("flame-hat");
            Check(hat.id=="flame-hat"&&hat.slot==OutfitSlot.Head&&Outfits.Earned(hat)&&!hat.IsAddOn&&!string.IsNullOrEmpty(hat.hint)&&Outfits.Earnable==14,"The hat of flames is a hat to earn, never to buy: one of fourteen pieces");
            Check(D.wardrobe.avatar.looks.Any(l=>l.id=="flame-hat"&&l.group.GetComponentsInChildren<Flicker>(true).Length>=9),"The reflection can wear the hat of flames, its flames flickering");

            // ---- Arriving
            D.Travel(WebRoom.Room);yield return 1.2f;
            var seat=D.rooms[WebRoom.Room].spawn.position;
            Check(D.currentRoom==WebRoom.Room&&!P.busy,"Walking in");
            Check(w.machete.held&&P.IsHolding(w.machete),"The machete is already in your hand");
            Check(w.Standing==18&&w.Cleared==0&&w.webs.All(x=>x.silk.gameObject.activeSelf&&Mathf.Approximately(x.grown,1)),"All eighteen webs are whole");
            Check(P.hint.text.Contains("machete"),"The room says the machete is in your hand");
            float nearest=w.webs.Min(x=>Flat(x.transform.position,seat)),farthest=w.webs.Max(x=>Flat(x.transform.position,seat));
            float low=w.webs.Min(x=>x.volume.bounds.min.y-Root.position.y),high=w.webs.Max(x=>x.volume.bounds.max.y-Root.position.y);
            Check(nearest>.6f&&farthest<.85f&&low>.55f&&high<2.05f,"The webs circle the seat at arm's reach ("+nearest.ToString("0.00")+" to "+farthest.ToString("0.00")+" m, "+low.ToString("0.00")+" to "+high.ToString("0.00")+" m up)");
            var angles=w.webs.Select(x=>Vector3.SignedAngle(D.rooms[WebRoom.Room].spawn.forward,Vector3.ProjectOnPlane(x.transform.position-seat,Vector3.up),Vector3.up)).ToArray();
            Check(angles.Min()<-110&&angles.Max()>110,"They wrap round from well behind one shoulder to well behind the other ("+angles.Min().ToString("0")+" to "+angles.Max().ToString("0")+" degrees)");
            // The spider: as big as a person, in the far corner, eight red eyes.
            var spider=w.spider;var bounds=spider.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
            Check(spider.gameObject.activeInHierarchy&&bounds.size.y>1f&&bounds.size.y<1.9f&&Mathf.Max(bounds.size.x,bounds.size.z)>1.8f,"The spider is the size of a person ("+bounds.size.y.ToString("0.00")+" m tall, "+Mathf.Max(bounds.size.x,bounds.size.z).ToString("0.0")+" m across)");
            var local=Root.InverseTransformPoint(spider.position);
            Check(Flat(spider.position,seat)>4f&&Mathf.Abs(local.x)>3f&&local.z>3.5f,"It sits in the far corner, "+Flat(spider.position,seat).ToString("0.0")+" m from the seat");
            Check(w.eyes.Length==8&&w.eyes.All(e=>e.enabled&&e.sharedMaterial.name=="36 Spider eyes"),"Eight eyes, glowing red");
            Check(!w.ash.activeSelf&&w.legs.Length==8,"Eight legs, and no ash yet");

            // ---- Cutting
            // A slow swing cuts nothing: the view drifts down through a column and back up, a little each frame.
            foreach(var t in WebTesting.Raise())yield return t;
            var head=P.Head;var through=WebTesting.Through(WebTesting.Between(0));
            head.rotation=through*Quaternion.Euler(-85,0,0);yield return .3f;
            for(float a=-85;a<25;a+=WebTesting.SlowTurn*Time.deltaTime){head.rotation=through*Quaternion.Euler(a,0,0);yield return 0;}
            for(float a=25;a>-85;a-=WebTesting.SlowTurn*Time.deltaTime){head.rotation=through*Quaternion.Euler(a,0,0);yield return 0;}
            Check(w.cuts==0&&w.Standing==18,"Resting the machete against a web, or moving it very slowly, cuts nothing");
            foreach(var t in WebTesting.Chop(4))yield return t;
            var middle=WebTesting.Column(4);
            Check(middle.All(x=>x.cut&&!x.silk.gameObject.activeSelf)&&w.Cleared==2&&w.cuts==2,"One chop straight ahead cuts both webs of that column");
            Check(D.rooms[WebRoom.Room].accepted==2&&D.rooms[WebRoom.Room].progress.text.Contains("2 / 18")&&!D.rooms[WebRoom.Room].complete,"The heading counts 2 / 18 cut away, and the chapter waits");
            Check(w.silk.particleCount>0,"Silk flies from the cut");
            Check(w.webs.Except(middle).All(x=>!x.cut),"Nothing else was cut on the way in or out of the chop");

            // ---- Growing back
            yield return WebRoom.RegrowAfter;
            Check(middle.All(x=>!x.cut&&x.silk.gameObject.activeSelf&&x.grown<1)&&w.Standing==18&&w.regrowths==2,"After a while the cut webs start to grow back");
            Check(Mathf.Approximately(w.regrowDelay,WebRoom.RegrowAfter+2*WebRoom.RegrowStep),"Each one that grows back makes the next wait longer ("+w.regrowDelay.ToString("0")+" s now)");
            Check(D.rooms[WebRoom.Room].accepted==0&&!D.rooms[WebRoom.Room].complete,"The heading counts them back");
            yield return WebRoom.GrowSeconds+.3f;
            Check(middle.All(x=>Mathf.Approximately(x.grown,1)),"A web grows whole again in "+WebRoom.GrowSeconds+" s");
            Check(WebRoom.RegrowMost>WebRoom.RegrowAfter&&WebRoom.RegrowMost>=25,"However slow the swings, the wait grows to "+WebRoom.RegrowMost+" s, so the room always gives in");

            // ---- Clearing them all
            float started=Time.time;
            foreach(var t in WebTesting.ClearAll())yield return t;
            Check(w.Cleared==18&&w.beaten,"Every web cut before any grew back ("+(Time.time-started).ToString("0.0")+" s)");
            Check(D.rooms[WebRoom.Room].complete&&D.CompletedCount==1&&PlayerPrefs.GetInt("AfterHours.Progress.v1")==(1<<WebRoom.Room),"That finishes the chapter, saved as chapter 9's flag");
            Check(D.rooms[WebRoom.Room].progress.text.Contains("BREATHE")&&D.lobbyProgress.text.Contains("1 / 8"),"The heading and the lobby count it");
            yield return WebRoom.RegrowAfter+1;
            Check(w.Cleared==18,"Once cleared, the webs stay gone");

            // ---- Kill it with fire
            Check(!D.wardrobe.Has("flame-hat"),"The hat of flames waits for the button");
            foreach(var t in Click(fire))yield return t;
            Check(w.Burning&&w.flames.particleCount>0,"The unlabelled button sets the spider ablaze");
            Check(D.wardrobe.Has("flame-hat"),"And earns the hat of flames");
            yield return WebRoom.BurnSeconds+.3f;
            Check(w.burned&&!w.spider.gameObject.activeSelf&&w.ash.activeSelf,"The spider burns away to a heap of ash");
            foreach(var t in Click(fire))yield return t;
            Check(w.burned&&!w.Burning&&P.hint.text.Contains("nothing left"),"Pressing it again only clicks");
            for(int i=0;i<30&&!(P.HintVisible&&P.hint.text.Contains("Hat of flames"));i++)yield return .5f;
            Check(P.HintVisible&&P.hint.text.Contains("Hat of flames"),"The hat of flames is announced");

            // ---- Leaving and coming back
            D.Travel(0);yield return 1f;
            Check(!w.machete.held&&!P.IsHolding(w.machete),"Leaving puts the machete back on its hook");
            D.Travel(WebRoom.Room);yield return 1.2f;
            Check(w.spider.gameObject.activeSelf&&!w.ash.activeSelf&&w.eyes.All(e=>e.enabled)&&!w.burned,"Coming back, the spider is in its corner again");
            Check(w.Standing==18&&!w.beaten&&D.rooms[WebRoom.Room].complete&&w.machete.held,"The webs are whole again for another go, the machete is in hand, and the chapter stays finished");
            Check(D.wardrobe.Has("flame-hat"),"The hat is kept");

            // ---- TRY AGAIN
            foreach(var t in WebTesting.Chop(0))yield return t;
            Check(w.Cleared==2,"A column cut before TRY AGAIN");
            foreach(var t in OpenMenu())yield return t;
            foreach(var t in Click(D.menu.Button(ActionKind.ResetRoom)))yield return t;yield return .5f;
            Check(w.Standing==18&&D.rooms[WebRoom.Room].accepted==0&&D.rooms[WebRoom.Room].complete&&w.machete.held,"TRY AGAIN grows every web back, and the machete stays in hand");
            Check(D.rooms[WebRoom.Room].progress.text.Contains("BREATHE"),"The finished chapter stays finished");
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
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Web-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The ring of webs and the machete in hand, cutting, the webs growing back, clearing them all, the spider and the unlabelled button that burns it, the hat of flames, coming back, TRY AGAIN."}.Concat(lines));Debug.Log("Web validation: "+Status);
        }
    }
}
