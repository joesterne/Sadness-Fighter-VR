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
    // Regression test for the rowing oars, the lighthouse light, the ocean's night option, the sea sounds,
    // the menu button that clears everything, and clean mode.
    public static class OceanValidation
    {
        static IEnumerator<float> run;static double next;static float deadline;static Mouse mouse;static Keyboard keyboard;
        static readonly List<string> lines=new List<string>();
        static readonly string[] keys={CheckpointStore.Key,"AfterHours.Progress.v1","AfterHours.Seated","AfterHours.SeatedHeight","AfterHours.Vignette","AfterHours.Clean",OceanRoom.NightKey,IntroGuide.Key,Wardrobe.Key};
        static bool[] existed;static string checkpoint,wardrobe;static int[] values;
        public static string Status="Not run";
        static GameDirector D=>GameDirector.Instance;
        static VRPlayer P=>D.player;
        public static void Start()
        {
            if(!EditorApplication.isPlaying||D.player.IsXR)throw new InvalidOperationException("Start desktop Play Mode first.");
            if(run!=null)return;existed=keys.Select(PlayerPrefs.HasKey).ToArray();checkpoint=PlayerPrefs.GetString(keys[0]);wardrobe=PlayerPrefs.GetString(Wardrobe.Key);values=keys.Select(k=>k==Wardrobe.Key?0:PlayerPrefs.GetInt(k)).ToArray();
            foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.SetInt(IntroGuide.Key,1);D.saveEnabled=false;
            mouse=InputSystem.AddDevice<Mouse>("OceanValidationMouse");keyboard=InputSystem.AddDevice<Keyboard>("OceanValidationKeyboard");TestInput.Claim(mouse,keyboard);lines.Clear();Status="Running";run=Checks();deadline=0;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlay;
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("Stopped");}
        static void Queue<T>(InputDevice device,T state) where T:struct,IInputStateTypeInfo{device.MakeCurrent();InputSystem.QueueStateEvent(device,state);}
        static void Check(bool value,string label){lines.Add((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
        static void Aim(Interactable b)
        {
            P.Head.rotation=Quaternion.LookRotation(b.transform.position-P.Head.position);
            if(!Physics.Raycast(P.Head.position,P.Head.forward,out var hit,14,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=b)
                throw new Exception("Pointer target obstructed: "+b.name+" by "+(hit.collider?hit.collider.name:"nothing"));
        }
        static void Press(bool down){Queue(mouse,new MouseState().WithButton(MouseButton.Left,down));}
        static IEnumerable<float> Click(Interactable b){Aim(b);Press(true);yield return .12f;Press(false);yield return .2f;}
        static IEnumerable<float> Click(ActionKind kind)=>Click(D.menu.Button(kind));
        static IEnumerable<float> Hold(Key key,float seconds){Queue(keyboard,new KeyboardState(key));yield return seconds;Queue(keyboard,new KeyboardState());yield return .15f;}
        // One press of the menu button. With a message showing it clears the message instead of opening the menu.
        static IEnumerable<float> Tab(){foreach(var t in Hold(Key.Tab,.1f))yield return t;}
        // Opens the menu, pressing twice when a message was in the way.
        static IEnumerable<float> OpenMenu(){for(int i=0;i<2&&!D.menu.IsOpen;i++)foreach(var t in Tab())yield return t;}
        static IEnumerable<float> CloseMenu(){for(int i=0;i<2&&D.menu.IsOpen;i++)foreach(var t in Tab())yield return t;}
        static Vector3 Flat(Vector3 v)=>Vector3.ProjectOnPlane(v,Vector3.up).normalized;
        static Color Sky=>RenderSettings.ambientSkyColor;
        static bool Dark(Color night,Color day)=>night.grayscale<day.grayscale*.6f;

        static IEnumerator<float> Checks()
        {
            SceneManager.LoadScene("AfterHours");yield return .8f;
            var ocean=D.ocean;var sea=ocean.sea;var waves=ocean.waves;
            Check(ocean&&sea&&waves&&ocean.oars!=null&&ocean.beacon,"The ocean room has oars, a lighthouse light, moving water and sea sounds");

            // ---- The oars
            D.Travel(1);yield return .9f;
            Check(D.currentRoom==1,"The ocean is open");
            var origin=D.rooms[1].root.transform.position;
            Check(ocean.oars.Length==2&&ocean.oars.All(o=>o.GetComponent<Grabbable>().paddle),"The boat has two oars, and each can be held");
            foreach(var o in ocean.oars)
            {
                var local=o.transform.position-origin;
                Check(Mathf.Abs(Mathf.Abs(local.x)-1.25f)<.01f&&local.y>.7f&&local.y<.9f&&Mathf.Abs(local.z+.55f)<.01f,"The "+(o.side<0?"left":"right")+" oar sits in an oarlock on the gunwale");
                var handle=o.HandleEnd-origin;var blade=o.BladeCentre-origin;
                Check(Mathf.Abs(o.angle-Oar.RestAngle)<.5f&&Mathf.Abs(handle.x)<1.1f&&Mathf.Abs(blade.x)>2f&&blade.y>-.1f&&blade.y<.3f,"At rest the handle is inside the boat and the blade rests just above the water ("+blade.x.ToString("0.0")+", "+blade.y.ToString("0.00")+")");
            }
            var right=ocean.oars.First(o=>o.side>0);var left=ocean.oars.First(o=>o.side<0);var grab=right.GetComponent<Grabbable>();
            var lockAt=right.transform.position;
            var hand=new GameObject("Test hand").transform;
            Vector3 Hand(float z)=>new Vector3(lockAt.x-.8f,lockAt.y+.4f,lockAt.z+z);
            hand.position=Hand(0);
            Check(grab.Take(hand),"A hand takes hold of an oar");
            hand.position=Hand(-.35f);yield return .4f;
            Check(right.angle>Oar.CatchAngle,"Drawing the handle toward the body swings the blade forward ("+right.angle.ToString("0")+" degrees)");
            var caught=right.BladeCentre;int before=ocean.strokes;bool otherSweeps=false;
            for(int i=1;i<=9;i++){hand.position=Hand(-.35f+i*.075f);yield return .05f;if(left.Playing)otherSweeps=true;}
            yield return .25f;
            Check(right.angle<Oar.FinishAngle,"Pushing the handle forward swings the blade back through the water ("+right.angle.ToString("0")+" degrees)");
            Check(right.BladeCentre.z-caught.z<-.6f,"The blade moves the opposite way to the handle ("+(right.BladeCentre.z-caught.z).ToString("0.00")+" m)");
            Check(ocean.strokes==before+1,"One catch and drive is one stroke");
            Check(otherSweeps||left.Playing,"The oar nobody holds sweeps a stroke by itself");
            Check(Vector3.Distance(right.transform.position,lockAt)<.001f,"The oar never leaves its lock");
            Check(Vector3.Cross(right.BladeCentre-right.HandleEnd,lockAt-right.HandleEnd).magnitude<.01f,"The shaft passes through the lock at every angle");
            Check(right.pitch>=18f&&right.pitch<=42f&&(right.BladeCentre.y-origin.y)<.2f,"The blade dips toward the water during the drive ("+(right.BladeCentre.y-origin.y).ToString("0.00")+" m)");
            // A single push without a catch is not a stroke.
            grab.Release();yield return 1.6f;
            Check(Mathf.Abs(right.angle-Oar.RestAngle)<1f&&Mathf.Abs(right.pitch-Oar.RestPitch)<1f,"A released oar eases back to rest");
            Check(Vector3.Distance(right.transform.position,lockAt)<.001f,"A released oar is still in its lock");
            hand.position=Hand(.3f);Check(grab.Take(hand),"The oar can be taken again");before=ocean.strokes;yield return .4f;hand.position=Hand(.05f);yield return .3f;
            Check(ocean.strokes==before,"Pushing without first drawing the handle back is not a stroke");
            grab.Release();UnityEngine.Object.Destroy(hand.gameObject);yield return 1.6f;
            // The accessible stroke.
            before=ocean.strokes;
            var row=D.rooms[1].root.GetComponentsInChildren<Interactable>().First(x=>x.kind==ActionKind.Row);
            foreach(var t in Click(row))yield return t;
            Check(ocean.strokes==before+1,"ROW takes a stroke");
            Check(ocean.oars.All(o=>o.Playing),"Both oars sweep a stroke for ROW");
            yield return 1.5f;
            Check(ocean.oars.All(o=>!o.Playing&&Mathf.Abs(o.angle-Oar.RestAngle)<1f),"Both oars come back to rest after the stroke");
            Check(D.rooms[1].root.GetComponentsInChildren<TMP_Text>().Any(x=>x.text.Contains("Hold an oar")),"The sign tells the player how to use the oars");


            // ---- The waves
            Check(waves.sea==sea&&waves.room==ocean.transform&&waves.live&&waves.GetComponent<MeshRenderer>()==ocean.water&&waves.transform.IsChildOf(ocean.horizon),"The water is one moving mesh on the horizon, kept in step with the sea sounds");
            Check(waves.live.vertexCount==OceanWaves.CellsX*OceanWaves.CellsZ*6&&waves.live.bounds.size.x>=64&&waves.live.bounds.size.z>=88,"The sea is "+OceanWaves.CellsX+" by "+OceanWaves.CellsZ+" flat facets, and its bounds hold every wave");
            Check(ocean.water.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off,"The water casts no shadows, which saves the headset a pass");
            Check(Mathf.Abs(OceanWaves.MainSwell(0,0,1.7f)-OceanWaves.MainSwell(0,0,1.7f+OceanWaves.Period))<.001f&&Mathf.Abs(OceanWaves.MainSwell(0,0,OceanWaves.Crest)-OceanWaves.MainSwell(0,0,OceanWaves.Crest+OceanWaves.Period*3))<.001f,"The main swell repeats every "+OceanWaves.Period+" seconds, to match the waves in the sound");
            Check(OceanWaves.MainSwell(0,0,OceanWaves.Crest)>OceanWaves.MainSwell(0,0,OceanWaves.Crest-.7f)&&OceanWaves.MainSwell(0,0,OceanWaves.Crest)>OceanWaves.MainSwell(0,0,OceanWaves.Crest+.7f),"A crest passes the boat "+OceanWaves.Crest+" seconds into each wave, as the sound crests");
            Check(OceanWaves.MostSwell>.4f&&OceanWaves.MostSwell<.55f,"The swells together run up to "+(OceanWaves.MostSwell*2).ToString("0.00")+" m from trough to crest: a lively sea, never a wild one");
            // However high the swells run and wherever the boat has rowed to, the water under the hull stays below the deck, and climbs the hull's sides.
            {
                float inside=-99,sideHigh=-99,sideLow=99;int keptStrokes=ocean.strokes;
                for(int n=0;n<=ocean.requiredStrokes;n++)
                {
                    ocean.RestoreProgress(n);
                    for(int k=0;k<16;k++)
                    {
                        waves.Apply(k*.53f);
                        for(float x=-1.15f;x<=1.151f;x+=.1f)for(float z=-2.3f;z<=1.86f;z+=.1f)inside=Mathf.Max(inside,waves.WaterAt(x,z));
                        for(float z=-2.3f;z<=1.86f;z+=.2f){float h=waves.WaterAt(OceanWaves.HullX+.1f,z);sideHigh=Mathf.Max(sideHigh,h);sideLow=Mathf.Min(sideLow,h);}
                    }
                }
                ocean.RestoreProgress(keptStrokes);
                Check(inside<.22f&&waves.underHull<=OceanWaves.HullLevel+.001f,"No water ever shows on the deck (highest inside the hull "+inside.ToString("0.00")+" m; the deck is at 0.25 m)");
                Check(sideHigh-sideLow>.3f,"The swells rise and fall against the side of the boat ("+sideLow.ToString("0.00")+" to "+sideHigh.ToString("0.00")+" m)");
            }
            // The foam and the spray.
            Check(waves.foam&&waves.liveFoam&&waves.foam.sharedMesh==waves.liveFoam&&waves.foam.transform.parent==waves.transform&&waves.liveFoam.vertexCount==OceanWaves.FoamCount*3,"Foam rides on the water: "+OceanWaves.Whitecaps+" whitecaps and "+OceanWaves.Wash+" flecks of wash round the hull, in one mesh");
            var foamRenderer=waves.foam.GetComponent<MeshRenderer>();
            Check(foamRenderer&&foamRenderer.sharedMaterial&&foamRenderer.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off,"The foam casts no shadows");
            Check(waves.spray&&!waves.spray.main.playOnAwake&&!waves.spray.transform.IsChildOf(ocean.horizon)&&Mathf.Abs(waves.spray.transform.position.z-origin.z-OceanWaves.HullFront)<.5f,"Spray is ready at the bow, and stays with the boat");
            // Watch the sea for ten seconds: it moves beside the boat, stays in time with the sound, foams on its crests and throws spray at the bow.
            float low=99,high=-99,tall=-99,deep=99,clockOff=0;int startUpdates=waves.updates,startSprays=waves.sprays,foamLeast=int.MaxValue,foamMost=0;float startClock=sea.Clock;bool clockRan=true,sampled=false;
            for(int i=0;i<40;i++)
            {
                yield return .25f;float y=waves.WaterAt(3,0);low=Mathf.Min(low,y);high=Mathf.Max(high,y);tall=Mathf.Max(tall,waves.highest);deep=Mathf.Min(deep,waves.lowest);
                foamLeast=Mathf.Min(foamLeast,waves.foamShowing);foamMost=Mathf.Max(foamMost,waves.foamShowing);
                clockRan&=sea.Clock>=0&&sea.Clock<SeaSounds.WaveLoop;
                if(sea.swell.isPlaying){float gap=Mathf.Abs(Mathf.Repeat(sea.swell.time-sea.Clock+SeaSounds.WaveLoop*.5f,SeaSounds.WaveLoop)-SeaSounds.WaveLoop*.5f);clockOff=Mathf.Max(clockOff,gap);sampled=true;}
            }
            Check(waves.updates-startUpdates>=100,"The water is rebuilt as the game runs ("+(waves.updates-startUpdates)+" times in ten seconds)");
            Check(high-low>.25f,"The sea beside the boat rises and falls ("+(high-low).ToString("0.00")+" m in ten seconds)");
            Check(tall>.12f&&tall<1f&&deep>-.95f,"Crests stand well up out of the sea, and no trough sinks out of sight (highest "+tall.ToString("0.00")+" m, lowest "+deep.ToString("0.00")+" m)");
            Check(foamMost>OceanWaves.Wash/2&&foamMost<OceanWaves.FoamCount&&foamMost>foamLeast,"Foam opens and closes with the swells ("+foamLeast+" to "+foamMost+" flecks showing)");
            Check(waves.sprays-startSprays>=1,"A crest breaks on the bow in spray as the waves pass ("+(waves.sprays-startSprays)+" times in ten seconds)");
            Check(clockRan&&Mathf.Abs(Mathf.Repeat(sea.Clock-startClock+SeaSounds.WaveLoop*.5f,SeaSounds.WaveLoop)-SeaSounds.WaveLoop*.5f-10)<3,"The sea's beat keeps time with the game ("+sea.Clock.ToString("0.0")+" s into the wave loop)");
            lines.Add(sampled?"INFO: the beat was never more than "+clockOff.ToString("0.00")+" s from the wave sound's own position in ten seconds":"INFO: the wave sound was not playing in this run, so its position was not compared");
            Check(!sampled||clockOff<2f,"The water's beat follows the wave sound");
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<60;i++)waves.Apply(i*.1f);timer.Stop();double perUpdate=timer.Elapsed.TotalMilliseconds/60;
            lines.Add("INFO: one rebuild of the water takes "+perUpdate.ToString("0.00")+" ms in the editor (it is done 30 times a second)");
            Check(perUpdate<4,"Rebuilding the water is cheap ("+perUpdate.ToString("0.00")+" ms)");
            // The surf is on the shore, so it comes nearer as the boat is rowed.
            Check(sea.surf.spatialBlend>.99f&&sea.surf.transform.IsChildOf(ocean.horizon),"The surf comes from the shore itself, which moves with the horizon");
            float farShore=Vector3.Distance(P.Head.position,sea.surf.transform.position);int kept=ocean.strokes;ocean.RestoreProgress(Mathf.Min(ocean.requiredStrokes-1,kept+6));
            float nearShore=Vector3.Distance(P.Head.position,sea.surf.transform.position);ocean.RestoreProgress(kept);
            Check(nearShore<farShore-3,"Rowing brings the surf nearer ("+farShore.ToString("0")+" m, then "+nearShore.ToString("0")+" m)");

            // ---- The lighthouse light
            Check(ocean.beams.Length==2&&ocean.beams.All(b=>b.enabled)&&ocean.beacon.degreesPerSecond>20,"The lighthouse has a light with two beams");
            var beam=ocean.beams[0].transform;var f0=Flat(beam.forward);float t0=Time.time;yield return .6f;
            float turned=Vector3.SignedAngle(f0,Flat(beam.forward),Vector3.up),rate=turned/(Time.time-t0);
            Check(turned>5&&Mathf.Abs(rate-ocean.beacon.degreesPerSecond)<8,"The light turns clockwise seen from above ("+rate.ToString("0")+" degrees a second)");
            Check(Vector3.Distance(ocean.beacon.transform.position,D.rooms[1].root.transform.TransformPoint(ocean.horizon.localPosition+new Vector3(0,5.3f,20)))<.01f,"The light sits in the lighthouse's lantern");

            // ---- Night, by the menu
            var daySky=Sky;var daySkybox=RenderSettings.skybox;float dayIntensity=RenderSettings.sun.intensity;var dayFog=RenderSettings.fogColor;
            Check(!ocean.night&&!ocean.nightSky.activeSelf&&ocean.sun.activeSelf,"The ocean starts in daylight");
            int rolled=waves.updates;
            var sounds=new[]{sea.swell,sea.wash,sea.lapping,sea.wind,sea.surf};
            Check(sounds.All(x=>x&&x.clip&&x.loop),"The waves, the open sea, the lapping, the wind and the surf are looping sounds");
            Check(sea.swell.clip.name=="Ambience - Waves"&&sea.surf.clip.name=="Ambience - Surf"&&Mathf.Abs(sea.swell.clip.length-SeaSounds.WaveLoop)<.05f,"The waves are four eight-second waves to the loop ("+sea.swell.clip.length.ToString("0.0")+" s)");
            Check(sea.gulls.Length==2&&sea.gulls.All(x=>x)&&sea.bell&&sea.creak,"The gulls, the bell buoy and the oarlock creak are loaded");
            Check(sea.swell.volume>.6f&&sea.wash.volume>.15f&&sea.lapping.volume>.25f&&sea.surf.volume>.6f&&sea.wind.volume<.05f,"By day the sea is waves, surf and lapping, with no night wind");
            sea.Soon();yield return 1.2f;
            Check(sea.gullCount>0&&sea.bellCount==0,"A gull calls by day, and the bell buoy stays quiet");
            foreach(var t in OpenMenu())yield return t;
            Check(D.menu.IsOpen&&D.menu.night.text.Contains("OFF")&&D.menu.clean.text.Contains("OFF"),"The menu offers CLEAN MODE and OCEAN AT NIGHT, both off");
            foreach(var t in Click(ActionKind.NightOcean))yield return t;yield return 1f;
            Check(ocean.night&&PlayerPrefs.GetInt(OceanRoom.NightKey)==1&&D.menu.night.text.Contains("ON"),"OCEAN AT NIGHT switches on and is saved");
            Check(Dark(Sky,daySky)&&Dark(RenderSettings.fogColor,dayFog)&&RenderSettings.skybox==ocean.nightSkybox&&RenderSettings.skybox!=daySkybox,"Night darkens the ambient light, the fog and the sky");
            Check(RenderSettings.sun.intensity<dayIntensity*.6f,"The sun gives way to dim moonlight");
            Check(ocean.nightSky.activeSelf&&!ocean.sun.activeSelf,"The stars and moon replace the sun");
            Check(ocean.water.sharedMaterial==ocean.nightWater&&ocean.lantern.sharedMaterial==ocean.nightLantern&&ocean.beams.All(b=>b.sharedMaterial==ocean.nightBeam),"The water, the lantern and the beams change for the night");
            yield return 3f;
            Check(sea.wind.volume>.35f&&sea.swell.volume<.5f&&sea.lapping.volume<.3f&&sea.surf.volume<.6f,"By night the sea fades to wind and a deeper swell");
            Check(waves.updates>rolled+20,"The water keeps rolling at night");
            sea.Soon();yield return 1.2f;
            Check(sea.bellCount>0,"A bell buoy rings far off at night");
            foreach(var t in CloseMenu())yield return t;

            // ---- Night belongs to the ocean alone
            D.Travel(0);yield return .9f;
            Check(ocean.night&&Sky==daySky&&RenderSettings.skybox==daySkybox&&Mathf.Approximately(RenderSettings.sun.intensity,dayIntensity),"Leaving the ocean puts the day's light back for the lobby");
            D.Travel(2);yield return .9f;
            Check(Sky==daySky&&RenderSettings.skybox==daySkybox&&RenderSettings.fogColor==dayFog,"The other rooms keep their own light");
            D.Travel(1);yield return .9f;
            Check(Dark(Sky,daySky)&&RenderSettings.skybox==ocean.nightSkybox&&ocean.nightSky.activeSelf,"Returning to the ocean brings the night back");
            foreach(var t in OpenMenu())yield return t;foreach(var t in Click(ActionKind.NightOcean))yield return t;yield return 1f;
            Check(!ocean.night&&Sky==daySky&&RenderSettings.skybox==daySkybox&&ocean.sun.activeSelf&&!ocean.nightSky.activeSelf,"Switching night off in the ocean restores the day");
            Check(ocean.water.sharedMaterial==ocean.dayWater&&ocean.lantern.sharedMaterial==ocean.dayLantern&&ocean.beams.All(b=>b.sharedMaterial==ocean.dayBeam),"The water, lantern and beams return to day");
            foreach(var t in CloseMenu())yield return t;
            // Chosen from the lobby: it waits for the next visit.
            D.Travel(0);yield return .9f;
            foreach(var t in OpenMenu())yield return t;foreach(var t in Click(ActionKind.NightOcean))yield return t;
            Check(ocean.night&&Sky==daySky&&D.menu.night.text.Contains("ON"),"OCEAN AT NIGHT can be chosen from the lobby without changing the lobby");
            foreach(var t in CloseMenu())yield return t;

            // ---- Saved across a restart
            D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return .8f;ocean=D.ocean;sea=ocean.sea;
            Check(ocean.night,"The night choice survives a restart");
            D.Travel(1);yield return .9f;
            Check(Dark(Sky,daySky)&&ocean.nightSky.activeSelf&&sea.night,"The ocean opens at night after a restart");
            foreach(var t in OpenMenu())yield return t;foreach(var t in Click(ActionKind.NightOcean))yield return t;yield return 1f;
            Check(!ocean.night&&Sky==daySky,"And back to day");
            foreach(var t in CloseMenu())yield return t;
            D.Travel(0);yield return .9f;

            // ---- The menu button clears everything
            D.Say("A message the player wants gone");
            Check(P.HintVisible,"A message is showing");
            foreach(var t in Tab())yield return t;
            Check(!P.HintVisible&&!P.hint.gameObject.activeSelf&&!D.menu.IsOpen,"With a message showing, the menu button clears it and does not open the menu");
            foreach(var t in Tab())yield return t;
            Check(D.menu.IsOpen,"The next press opens the menu");
            D.Say("Another message");yield return .1f;
            Check(P.HintVisible&&D.menu.IsOpen,"A message and the menu are both showing");
            foreach(var t in Tab())yield return t;
            Check(!D.menu.IsOpen&&!P.HintVisible,"One press closes the menu and the message together");
            PlayerPrefs.SetInt(IntroGuide.Key,0);D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return .8f;ocean=D.ocean;sea=ocean.sea;
            Check(D.intro.IsShowing,"A first visit opens the guide");
            D.intro.Next();D.intro.Next();D.Say("A message during the guide");
            Check(D.intro.Step==2&&P.HintVisible,"The guide waits for the menu while a message shows");
            foreach(var t in Tab())yield return t;
            Check(D.menu.IsOpen&&D.intro.Step==3,"During the guide's menu step, the first press still opens the menu");
            foreach(var t in CloseMenu())yield return t;D.intro.Skip();PlayerPrefs.SetInt(IntroGuide.Key,1);

            // ---- Menu layout
            foreach(var t in OpenMenu())yield return t;
            var buttons=D.menu.GetComponentsInChildren<Interactable>(true).Where(b=>b.gameObject.activeInHierarchy).ToArray();
            float worst=buttons.Max(b=>Vector3.Angle(Vector3.ProjectOnPlane(P.Head.forward,Vector3.up),b.transform.position-P.Head.position));
            Check(buttons.Any(b=>b.kind==ActionKind.CleanMode)&&buttons.Any(b=>b.kind==ActionKind.NightOcean)&&worst<42f,"Every menu button, including the new ones, is within "+worst.ToString("0")+" degrees of straight ahead");

            // ---- Clean mode
            var guidance=D.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Guidance>(true)).ToArray();
            Check(guidance.Length>=20&&guidance.All(g=>g.GetComponent<TMP_Text>().enabled),"The guiding signs are marked ("+guidance.Length+") and all show");
            Check(guidance.All(g=>!g.name.EndsWith(" label")),"No button label counts as a guiding sign");
            foreach(var t in Click(ActionKind.CleanMode))yield return t;
            Check(P.cleanMode&&PlayerPrefs.GetInt("AfterHours.Clean")==1&&D.menu.clean.text.Contains("ON"),"CLEAN MODE switches on and is saved");
            Check(guidance.All(g=>!g.GetComponent<TMP_Text>().enabled),"Clean mode switches off every guiding sign");
            Check(D.rooms.All(r=>r.progress==null||!r.progress.enabled)&&!ocean.distance.enabled&&!D.lobbyProgress.enabled,"Headings and progress boards go, in every room");
            Check(D.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Interactable>(true)).All(b=>{var label=b.transform.parent?b.transform.parent.Find(b.name+" label"):null;return !label||label.GetComponent<TMP_Text>().enabled;}),"Button and door labels stay");
            D.Say("A message in clean mode");yield return .1f;
            Check(!P.HintVisible&&!P.hint.gameObject.activeSelf,"No message is shown in clean mode");
            foreach(var t in Tab())yield return t;
            Check(!D.menu.IsOpen,"The menu button closes the menu in clean mode");
            foreach(var t in Tab())yield return t;
            Check(D.menu.IsOpen,"With nothing to clear, one press still opens the menu in clean mode");
            foreach(var t in CloseMenu())yield return t;
            D.Travel(1);yield return .9f;
            Check(!P.HintVisible&&!D.rooms[1].root.GetComponentsInChildren<TMP_Text>().Any(x=>x.enabled&&x.text.Contains("Hold an oar")),"The ocean shows no message and no instructions in clean mode");
            Check(P.cleanMode,"Moving between rooms keeps clean mode");
            // Saved across a restart, and the first-visit guide stays away.
            PlayerPrefs.SetInt(IntroGuide.Key,0);D.saveEnabled=false;SceneManager.LoadScene("AfterHours");yield return .8f;ocean=D.ocean;sea=ocean.sea;
            Check(P.cleanMode&&!D.intro.IsShowing&&D.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Guidance>(true)).All(g=>!g.GetComponent<TMP_Text>().enabled),"Clean mode survives a restart, with no first-visit guide and no signs");
            PlayerPrefs.SetInt(IntroGuide.Key,1);
            foreach(var t in OpenMenu())yield return t;foreach(var t in Click(ActionKind.CleanMode))yield return t;
            Check(!P.cleanMode&&PlayerPrefs.GetInt("AfterHours.Clean")==0&&D.rooms.SelectMany(r=>r.root.GetComponentsInChildren<Guidance>(true)).All(g=>g.GetComponent<TMP_Text>().enabled),"CLEAN MODE off brings every sign back");
            Check(P.HintVisible,"And says so");
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
            if(D){D.saveEnabled=false;if(D.ocean)D.ocean.Preview(false);}
            TestInput.Release();if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<keys.Length;i++){if(!existed[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetString(keys[i],checkpoint);else if(keys[i]==Wardrobe.Key)PlayerPrefs.SetString(keys[i],wardrobe);else PlayerPrefs.SetInt(keys[i],values[i]);}PlayerPrefs.Save();
            Directory.CreateDirectory("Documentation");File.WriteAllLines("Documentation/Ocean-validation.txt",new[]{DateTime.UtcNow.ToString("u"),Status,"The rowing oars, the lighthouse light, night in the ocean only, the sea sounds, the menu button that clears everything, and clean mode."}.Concat(lines));Debug.Log("Ocean, clean mode and night validation: "+Status);
        }
    }
}
