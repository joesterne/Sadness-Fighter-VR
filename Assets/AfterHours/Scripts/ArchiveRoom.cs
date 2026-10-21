using UnityEngine;

namespace AfterHours
{
    // "The infinite archive": a lit reading floor in the middle of stacks that rise out of sight and run on in every direction until the dark takes
    // them. The dark is a deep blue haze that starts a few metres out, and the sky is painted the same blue, so stacks, bridges and lamps fade into
    // it with no edge or horizon. Sky, light and fog belong to the whole scene, so the room sets them as it switches on and puts them back as it
    // switches off (the same way the rooftop does). A few loose pages drift down through the aisles.
    public class ArchiveRoom : MonoBehaviour
    {
        public const int Room=4;
        // The haze: the colour everything far away fades to, and where the fading starts and ends (metres from the eye).
        public static readonly Color Haze=new Color32(0x0D,0x17,0x26,255);
        public const float HazeStart=6,HazeEnd=48;
        public GameDirector director;
        public Material skybox;
        // The far stacks, bridges and lamps: a few large meshes of their own, kept out of the room's batches (so rays near the signs stay short).
        public Transform scenery;
        // Everything that moves keeps its own renderer, under this group (the scene's merging leaves it alone).
        public Transform moving;
        public Transform[] pages;
        public float[] speeds;
        // Pages fall from this height to the floor, then start again at the top.
        public const float PageTop=30;

        struct Look{public Color sky,equator,ground,fog,sun;public float sunIntensity,fogStart,fogEnd;public Material skybox;}
        // A cool, dim light from above, with enough bounce that the desk, the notes and the trays stay clear and friendly.
        static Look Stacks=>new Look{
            sky=new Color32(0x6C,0x80,0x9C,255),equator=new Color32(0x4A,0x5B,0x72,255),ground=new Color32(0x22,0x2D,0x3D,255),
            fog=Haze,fogStart=HazeStart,fogEnd=HazeEnd,sun=new Color32(0xD6,0xDF,0xF0,255),sunIntensity=1.05f};
        Look day;bool haveDay,changed;
        Vector3[] homes;float clock;

        static Look Capture()
        {
            var light=RenderSettings.sun;
            return new Look{sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,fog=RenderSettings.fogColor,fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,
                sun=light?light.color:Color.white,sunIntensity=light?light.intensity:1,skybox=RenderSettings.skybox};
        }
        void Apply(Look look)
        {
            RenderSettings.ambientSkyColor=look.sky;RenderSettings.ambientEquatorColor=look.equator;RenderSettings.ambientGroundColor=look.ground;
            RenderSettings.fogColor=look.fog;RenderSettings.fogStartDistance=look.fogStart;RenderSettings.fogEndDistance=look.fogEnd;
            var light=RenderSettings.sun;if(light){light.color=look.sun;light.intensity=look.sunIntensity;}
            var sky=look.skybox?look.skybox:skybox;
            if(sky&&RenderSettings.skybox!=sky){RenderSettings.skybox=sky;DynamicGI.UpdateEnvironment();}
        }
        // Switching the room on (the player arrives) brings the dark in; switching it off (they leave) puts the day back.
        void OnEnable(){if(Application.isPlaying)Enter();}
        void OnDisable(){if(Application.isPlaying)Leave();}
        void OnApplicationQuit(){Leave();}
        void Enter(){if(!changed){day=Capture();haveDay=true;}var look=Stacks;look.skybox=skybox;Apply(look);changed=true;}
        void Leave(){if(haveDay&&changed){Apply(day);changed=false;}}
        // For pictures made outside Play Mode: the room lit as it is on arrival. EndPreview puts everything back.
        public void Preview(){Enter();}
        public void EndPreview(){Leave();}
        public bool Showing=>changed;

        void Update()
        {
            if(pages==null)return;
            if(homes==null||homes.Length!=pages.Length){homes=new Vector3[pages.Length];for(int i=0;i<pages.Length;i++)if(pages[i])homes[i]=pages[i].localPosition;}
            clock+=Time.deltaTime;
            for(int i=0;i<pages.Length;i++)
            {
                var page=pages[i];if(!page)continue;
                // Each page falls slowly and sways from side to side as it goes, turning over now and then.
                var p=page.localPosition;p.y-=speeds[i]*Time.deltaTime;if(p.y<.1f)p.y+=PageTop;
                float phase=clock*.6f+i*1.7f;p.x=homes[i].x+Mathf.Sin(phase)*.8f;p.z=homes[i].z+Mathf.Cos(phase*.7f)*.5f;
                page.localPosition=p;page.localRotation=Quaternion.Euler(Mathf.Sin(phase)*35,i*37+clock*12,Mathf.Cos(phase*1.3f)*25);
            }
        }
    }
}
