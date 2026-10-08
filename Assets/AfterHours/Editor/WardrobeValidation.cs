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
    // The mirror, the avatar and the wardrobe: what earns each piece, trying pieces on with the pointer, the reflection
    // following the player, and everything surviving a reload and TRY AGAIN.
    public static class WardrobeValidation
    {
        static IEnumerator<float> run;static double next,deadline;static Mouse mouse;
        static readonly List<string> lines=new List<string>();
        static readonly string[] intKeys={"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette",IntroGuide.Key};
        static readonly string[] stringKeys={CheckpointStore.Key,Wardrobe.Key};
        static bool[] intExisted,stringExisted;static int[] intValues;static string[] stringValues;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        static Wardrobe W=>D.wardrobe;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;
            intExisted=intKeys.Select(PlayerPrefs.HasKey).ToArray();intValues=intKeys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            stringExisted=stringKeys.Select(PlayerPrefs.HasKey).ToArray();stringValues=stringKeys.Select(k=>PlayerPrefs.GetString(k)).ToArray();
            foreach(var key in intKeys.Concat(stringKeys))PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("WardrobeValidationMouse");
            lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static IEnumerable<float> Click(Interactable button)
        {
            P.Head.rotation=Quaternion.LookRotation(button.transform.position-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=button)
                throw new Exception("Pointer target obstructed: "+button.name+" by "+(hit.collider?hit.collider.name:"nothing"));
            Press(true);yield return .12f;Press(false);yield return .25f;
        }
        static Interactable Lobby(ActionKind kind,int value=-1)=>D.rooms[0].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==kind&&(value<0||x.value==value));
        static IEnumerable<float> Step(int row,int times=1){for(int i=0;i<times;i++)foreach(var t in Click(Lobby(ActionKind.WardrobeNext,row)))yield return t;}
        static IEnumerable<float> ToMirror()
        {
            if(D.currentRoom!=0){D.Travel(0);yield return .9f;}
            P.Place(D.rooms[0].spawn.position,0);yield return .2f;
            foreach(var t in Click(Lobby(ActionKind.GoToMirror)))yield return t;yield return .8f;
        }
        // The point a mirror on this plane shows for a point in the lobby.
        static Vector3 Mirrored(Vector3 world){var plane=W.avatar.transform;var local=plane.InverseTransformPoint(world);local.z=-local.z;return plane.TransformPoint(local);}
        static bool Announced(string name)=>P.hint.gameObject.activeSelf&&P.hint.text.Contains(name);
        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return 1f;
            Check(W.FoundCount==0&&W.Owned(OutfitSlot.Top).Count==2&&W.Owned(OutfitSlot.Head).Count==1,"A new player owns only the starter pieces: two tops and no hat");
            Check(D.mirrorLabel.text.Contains("YOUR WARDROBE")&&W.nameplate.text=="YOUR WARDROBE","The desk's MIRROR sign and the mirror's nameplate start plain");
            foreach(var t in ToMirror())yield return t;
            Check(Vector2.Distance(new Vector2(P.Head.position.x,P.Head.position.z),new Vector2(D.mirrorSpot.position.x,D.mirrorSpot.position.z))<.15f,"MIRROR on the desk brings you to the mirror");
            Check(Mathf.Abs(Mathf.DeltaAngle(P.Head.eulerAngles.y,D.mirrorSpot.eulerAngles.y))<1,"You arrive facing the glass");
            var avatar=W.avatar;
            Check(avatar.reflection.gameObject.activeInHierarchy,"Your reflection shows in the mirror");
            P.Head.rotation=Quaternion.LookRotation(D.mirrorSpot.forward);yield return .2f;
            var expected=Mirrored(P.Head.position+P.Head.rotation*new Vector3(0,.03f,-.08f));
            Check(Vector3.Distance(avatar.head.position,expected)<.03f,"The reflection's head is exactly where a mirror would show yours ("+Vector3.Distance(avatar.head.position,expected).ToString("0.000")+" m)");
            Check(avatar.transform.InverseTransformPoint(avatar.head.position).z<0,"The reflection stands behind the glass");
            // Turn the head: a reflection turns the other way.
            P.Head.rotation=Quaternion.LookRotation(Quaternion.Euler(0,30,0)*D.mirrorSpot.forward);yield return .2f;
            var plane=avatar.transform;var look=P.Head.forward;var mirroredLook=look-2*Vector3.Dot(look,plane.forward)*plane.forward;
            Check(Vector3.Angle(avatar.head.TransformVector(Vector3.forward).normalized,mirroredLook)<2,"Turning your head turns the reflection's head the mirrored way");
            P.Head.rotation=Quaternion.LookRotation(D.mirrorSpot.forward);yield return .1f;
            // Step closer and the reflection comes closer too.
            float before=Vector3.Distance(P.Head.position,avatar.head.position);P.Shift(D.mirrorSpot.forward*.5f);yield return .2f;
            Check(Mathf.Abs(before-Vector3.Distance(P.Head.position,avatar.head.position)-1)<.05f,"Half a metre closer to the glass brings the reflection a metre closer");
            P.Shift(-D.mirrorSpot.forward*.5f);yield return .1f;

            // How you look: each row steps through its choices and the reflection follows.
            foreach(var t in Step(0))yield return t;
            Check(W.State.skin==3&&avatar.skin.All(r=>r.sharedMaterial==avatar.skins[3]),"SKIN TONE > changes the reflection's skin");
            foreach(var t in Step(1))yield return t;
            Check(W.State.hair==1&&avatar.hairStyles[1].activeSelf&&avatar.hairStyles.Count(h=>h.activeSelf)==1,"HAIR > changes the hair style");
            foreach(var t in Step(2))yield return t;
            Check(W.State.hairColour==2&&avatar.hairStyles[1].GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial==avatar.hairs[2]),"HAIR COLOUR > changes the hair colour");
            float width=avatar.torso.localScale.x;foreach(var t in Step(3))yield return t;yield return .1f;
            Check(W.State.build==2&&avatar.torso.localScale.x>width,"BUILD > broadens the shoulders");
            foreach(var t in Click(Lobby(ActionKind.WardrobePrev,3)))yield return t;
            Check(W.State.build==1,"BUILD < steps back");
            foreach(var t in Step(4))yield return t;
            Check(W.State.top=="shirt"&&avatar.Wearing("shirt")&&!avatar.Wearing("tee"),"TOP > swaps the plain tee for the work shirt");
            foreach(var t in Step(5))yield return t;
            Check(W.State.head=="no-hat","With no hats earned, HAT stays on no hat");

            // What earns each piece. Pieces arrive once, are announced, and wait at the mirror.
            D.Travel(1);yield return 1f;D.ocean.Stroke();yield return 1f;
            Check(W.Has("step-pin"),"One paddle stroke, a first small step, earns the small step pin");
            yield return 4.2f;Check(Announced("Small step pin"),"The new piece is announced");
            D.Travel(3);yield return 1f;D.kitchen.AskForHelp();
            Check(W.Has("scarf"),"Asking for help earns the kind scarf");
            Check(!W.Has("jumper"),"The ocean's piece waits until the ocean is finished");
            D.ocean.RestoreProgress(13);D.Travel(1);yield return 1f;D.ocean.Stroke();yield return 1.2f;
            Check(D.rooms[1].complete&&W.Has("jumper"),"Reaching the shore earns the harbour jumper");
            foreach(int room in new[]{2,3,4})D.CompleteRoom(room);
            Check(W.Has("cap")&&W.Has("cardigan")&&W.Has("true-pin"),"Heavy things, A small spill and the archive earn the cap, the cardigan and the still true pin");
            Check(!W.Has("jacket"),"The jacket waits for all six chapters");
            D.Travel(Shredder.Room);yield return 1f;
            foreach(var page in D.shredder.pages)D.shredder.ShredNow(page);
            Check(D.shredder.Offering&&!W.Has("lanyard"),"The lanyard waits for a true line");
            D.shredder.Choose(1);
            Check(W.Has("lanyard")&&W.State.line=="I care about people."&&!W.Has("jacket"),"Choosing a line earns the lanyard; the jacket still waits for the rage room");
            D.Travel(RageRoom.Room);yield return 1f;
            foreach(var part in D.rage.parts)while(!part.Broken){D.rage.HitNow(part);yield return .05f;}
            Check(!W.Has("letitout-pin"),"The let it out pin waits for the quiet after the last break");
            yield return 11.5f;
            Check(W.Has("letitout-pin")&&W.Has("jacket"),"The quiet after the whole computer earns the let it out pin, and finishing all six chapters the sunrise jacket");
            D.Travel(5);yield return 10f;
            Check(!W.Has("beanie"),"Ten seconds on the rooftop is not yet a rest");
            yield return 12f;
            Check(W.Has("beanie")&&W.FoundCount==Outfits.Earnable,"Resting on the rooftop earns the evening beanie: every piece found");
            int fresh=W.State.fresh.Count;
            Check(fresh==Outfits.Earnable&&D.mirrorLabel.text.Contains(fresh+" NEW"),"The desk's MIRROR sign counts the new pieces ("+fresh+")");

            // Trying them on.
            foreach(var t in ToMirror())yield return t;
            Check(W.nameplate.text.Contains("I care about people."),"The mirror's nameplate shows the line you kept");
            foreach(var t in Step(4))yield return t;
            Check(W.State.top=="jumper"&&avatar.Wearing("jumper")&&!avatar.Wearing("shirt")&&!W.State.fresh.Contains("jumper"),"TOP > reaches the harbour jumper, and it is no longer new");
            Check(avatar.sleeveFore[0].sharedMaterial==avatar.sleeveUpper[0].sharedMaterial&&!avatar.skins.Contains(avatar.sleeveFore[0].sharedMaterial),"The jumper has long sleeves");
            foreach(var t in Step(5))yield return t;
            Check(W.State.head=="cap"&&avatar.Wearing("cap"),"HAT > puts on the mover's cap");
            Check(avatar.hairStyles[1].GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Crown")).All(r=>!r.gameObject.activeSelf),"Curls tuck under the cap");
            foreach(var t in Step(6,2))yield return t;
            Check(W.State.neck=="lanyard"&&avatar.Wearing("lanyard")&&!avatar.Wearing("scarf"),"NECK > twice: past the scarf to your own lanyard");
            foreach(var t in Step(7))yield return t;
            Check(W.State.pin=="step-pin"&&avatar.Wearing("step-pin"),"PIN > pins on the small step pin");
            foreach(var t in Step(7,2))yield return t;
            Check(W.State.pin=="letitout-pin"&&avatar.Wearing("letitout-pin")&&!avatar.Wearing("step-pin"),"PIN > twice more reaches the let it out pin");

            // Nothing is lost: TRY AGAIN keeps pieces, and a reload keeps everything.
            D.Travel(2);yield return 1f;D.RestartCurrentRoom();
            Check(W.Has("cap")&&W.FoundCount==Outfits.Earnable,"TRY AGAIN in a chapter keeps every piece");
            D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return 1f;
            var s=D.wardrobe.State;
            Check(D.wardrobe.FoundCount==Outfits.Earnable&&s.top=="jumper"&&s.head=="cap"&&s.neck=="lanyard"&&s.pin=="letitout-pin"&&s.skin==3&&s.hair==1&&s.hairColour==2,"After a reload: every piece, everything you wore and how you look");
            Check(D.wardrobe.avatar.Wearing("jumper")&&D.wardrobe.avatar.Wearing("cap")&&D.wardrobe.nameplate.text.Contains("I care about people."),"After a reload the reflection is dressed the same and the nameplate keeps your line");
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
            if(D)D.saveEnabled=false;if(mouse!=null)InputSystem.RemoveDevice(mouse);
            for(int i=0;i<intKeys.Length;i++){if(intExisted[i])PlayerPrefs.SetInt(intKeys[i],intValues[i]);else PlayerPrefs.DeleteKey(intKeys[i]);}
            for(int i=0;i<stringKeys.Length;i++){if(stringExisted[i])PlayerPrefs.SetString(stringKeys[i],stringValues[i]);else PlayerPrefs.DeleteKey(stringKeys[i]);}
            PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Wardrobe-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The mirror, the avatar and the wardrobe: pointer input at the mirror, the real unlock events, and a scene reload."}.Concat(lines));
            Debug.Log("Wardrobe validation: "+Status);
        }
    }
}
