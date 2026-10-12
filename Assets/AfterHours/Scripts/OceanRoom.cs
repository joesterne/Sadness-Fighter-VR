using System.Collections;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    public class OceanRoom : MonoBehaviour
    {
        public GameDirector director;
        public Transform horizon;
        public TextMeshPro distance;
        public int strokes;
        public int requiredStrokes=14;
        public bool blinkRowing=true;
        // The two oars, the lighthouse light, the sea sounds, and everything that changes between day and night.
        public Oar[] oars;
        public Spin beacon;
        public SeaSounds sea;
        public MeshRenderer water,lantern;
        public Renderer[] beams;
        public Material dayWater,nightWater,dayLantern,nightLantern,dayBeam,nightBeam,nightSkybox;
        public GameObject sun,nightSky;
        // Night is an option for this room only. Every other room keeps its own light.
        public const string NightKey="AfterHours.NightOcean";
        bool nightOn;
        public bool night{get{Initialize();return nightOn;}}
        Vector3 start;bool rowing,initialized,changedLook;
        // Sky, light and fog for the room, by day (taken from the scene) and by night.
        struct Look{public Color sky,equator,ground,fog,sun;public float sunIntensity,fogStart,fogEnd;public Material skybox;}
        Look day;
        Look NightLook=>new Look{sky=new Color32(0x34,0x46,0x7A,255),equator=new Color32(0x26,0x36,0x5F,255),ground=new Color32(0x15,0x1F,0x38,255),fog=new Color32(0x0A,0x13,0x25,255),sun=new Color32(0x8F,0xA7,0xE0,255),sunIntensity=.5f,fogStart=30,fogEnd=120,skybox=nightSkybox?nightSkybox:day.skybox};
        void Awake(){Initialize();}
        void Initialize()
        {
            if(initialized)return;
            start=horizon.localPosition;initialized=true;nightOn=PlayerPrefs.GetInt(NightKey,0)==1;
            var light=RenderSettings.sun;
            day=new Look{sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,fog=RenderSettings.fogColor,fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,skybox=RenderSettings.skybox,sun=light?light.color:Color.white,sunIntensity=light?light.intensity:1};
            // Outside Play Mode (the room previews) the scene is left exactly as built.
            if(Application.isPlaying)ApplyProps();
        }
        // ChangeRoom clears player.busy after the fade-in completes; clearing it here would enable input mid-fade.
        public void OnArrive(){rowing=false;RestoreProgress(strokes);SetEnvironment(true);}
        public void OnDepart()
        {
            StopAllCoroutines();rowing=false;RestoreProgress(strokes);
            if(strokes>=requiredStrokes&&!director.rooms[1].complete)director.CompleteRoom(1);
        }
        // Sky, light and fog are shared by the whole scene, so the ocean sets them as the player arrives and puts them back as they leave.
        public void SetEnvironment(bool inOcean)
        {
            Initialize();
            var look=inOcean&&nightOn?NightLook:day;
            if(inOcean&&!nightOn&&!changedLook){ApplyProps();return;}
            RenderSettings.ambientSkyColor=look.sky;RenderSettings.ambientEquatorColor=look.equator;RenderSettings.ambientGroundColor=look.ground;
            RenderSettings.fogColor=look.fog;RenderSettings.fogStartDistance=look.fogStart;RenderSettings.fogEndDistance=look.fogEnd;
            var light=RenderSettings.sun;if(light){light.color=look.sun;light.intensity=look.sunIntensity;}
            if(look.skybox&&RenderSettings.skybox!=look.skybox){RenderSettings.skybox=look.skybox;DynamicGI.UpdateEnvironment();}
            changedLook=inOcean&&nightOn;ApplyProps();
        }
        // Everything inside the room that follows the day or night choice.
        void ApplyProps()
        {
            if(sun)sun.SetActive(!nightOn);if(nightSky)nightSky.SetActive(nightOn);
            if(water&&dayWater&&nightWater)water.sharedMaterial=nightOn?nightWater:dayWater;
            if(lantern&&dayLantern&&nightLantern)lantern.sharedMaterial=nightOn?nightLantern:dayLantern;
            if(beams!=null&&dayBeam&&nightBeam)foreach(var b in beams)if(b)b.sharedMaterial=nightOn?nightBeam:dayBeam;
            if(sea)sea.SetNight(nightOn);
        }
        // The OCEAN AT NIGHT option. In the ocean the change comes with a short blink; elsewhere it waits for the next visit.
        public void ToggleNight()
        {
            Initialize();nightOn=!nightOn;PlayerPrefs.SetInt(NightKey,nightOn?1:0);PlayerPrefs.Save();
            bool here=director.currentRoom==1;
            if(here&&isActiveAndEnabled)StartCoroutine(Blink());else ApplyProps();
            director.RefreshComfort();
            director.Say(here?(nightOn?"Night on the water. The light turns slowly ahead.":"Daylight on the water."):(nightOn?"The ocean will be night the next time you go there.":"The ocean will be daylight the next time you go there."));
        }
        IEnumerator Blink()
        {
            yield return director.player.Fade(1,.15f);SetEnvironment(true);yield return director.player.Fade(0,.35f);
        }
        // For the room previews in the editor: shows the room by day or by night without touching the saved choice.
        public void Preview(bool asNight){Initialize();nightOn=asNight;SetEnvironment(asNight);if(!asNight)SetEnvironment(false);}
        void OnApplicationQuit(){if(changedLook)SetEnvironment(false);}
        public void Stroke()
        {
            if(rowing||director.currentRoom!=1||strokes>=requiredStrokes)return;
            StartCoroutine(Row());
        }
        IEnumerator Row()
        {
            rowing=true;strokes++;UpdateSign();director.SaveCheckpoint();director.PlayUi(director.row,.7f);director.SmallStep();
            // Any oar the player is not holding sweeps a stroke by itself, so the boat always looks rowed.
            if(oars!=null)foreach(var oar in oars)if(oar)oar.PlayStroke();
            if(sea&&oars!=null&&oars.Length>0&&oars[0])sea.Creak(oars[0].transform.position);
            Vector3 target=start+Vector3.back*(strokes*.9f);
            if(blinkRowing)
            {
                yield return director.player.Fade(1,.1f);horizon.localPosition=target;yield return director.player.Fade(0,.2f);
            }
            else
            {
                Vector3 from=horizon.localPosition;float t=0;while(t<1){t+=Time.deltaTime*.9f;horizon.localPosition=Vector3.Lerp(from,target,Mathf.SmoothStep(0,1,t));yield return null;}
            }
            if(strokes==4)director.Say("Shame says you are alone. A light is still on ahead.");
            if(strokes==9)director.Say("You do not have to earn the shore.");
            if(strokes>=requiredStrokes)director.CompleteRoom(1);
            yield return new WaitForSeconds(.35f);rowing=false;
        }
        void UpdateSign(){if(distance)distance.text=strokes>=requiredStrokes?"YOU MADE IT TO THE SHORE":(requiredStrokes-strokes)+"   SMALL STROKES TO SHORE";}
        public void RestoreProgress(int count){Initialize();strokes=Mathf.Clamp(count,0,requiredStrokes);horizon.localPosition=start+Vector3.back*(strokes*.9f);UpdateSign();}
        public void ResetRoom(){StopAllCoroutines();rowing=false;strokes=0;horizon.localPosition=start;UpdateSign();director.player.StartCoroutine(director.player.Fade(0,.1f));}
    }
}
