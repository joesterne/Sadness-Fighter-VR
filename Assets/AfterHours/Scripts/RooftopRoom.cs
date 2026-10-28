using UnityEngine;

namespace AfterHours
{
    // "Room for tomorrow": the rooftop at sunrise. It is bright, saturated and toy-like: a gradient sky with a flat, glowing sun, warm light with a lot of
    // bounce, a haze that fades the skyline into the dawn, clouds that drift and a balloon that bobs. Sky, light and fog belong to the whole scene, so the
    // room sets them as it switches on and puts them back as it switches off (the same way the ambush and the night ocean do).
    public class RooftopRoom : MonoBehaviour
    {
        public const int Room=5;
        // Where the painted sun sits in the sky: degrees clockwise from straight ahead (the board), and degrees above the horizon.
        public const float SunAzimuth=24,SunElevation=13,SunRadius=4.4f;
        public static Vector3 SunDirection
        {
            get{float az=SunAzimuth*Mathf.Deg2Rad,el=SunElevation*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(az)*Mathf.Cos(el),Mathf.Sin(el),Mathf.Cos(az)*Mathf.Cos(el));}
        }
        public GameDirector director;
        public Material skybox;
        // Everything that moves while the player rests here keeps its own renderer, under this group (the scene's merging leaves it alone).
        public Transform moving;
        public Transform[] drifting;
        public float[] speeds;
        public Transform balloon;
        // The tower of blocks on its table, to play while resting.
        public BlockTower tower;

        struct Look{public Color sky,equator,ground,fog,sun;public float sunIntensity,fogStart,fogEnd;public Quaternion sunRotation;public Material skybox;}
        // The sun is ahead of the player and low, so the faces they see are lit by bounce: the ambient is bright and warm, and the sun itself golden.
        static Look Sunrise=>new Look{
            sky=new Color32(0xC4,0xC6,0xF6,255),equator=new Color32(0xF7,0xCB,0xBC,255),ground=new Color32(0xDB,0x9E,0x82,255),
            fog=new Color32(0xFF,0xD2,0xAA,255),fogStart=34,fogEnd=135,
            sun=new Color32(0xFF,0xD8,0xA0,255),sunIntensity=1.25f,sunRotation=Quaternion.Euler(22,180+SunAzimuth,0)};
        Look day;bool haveDay,changed;
        Vector3 balloonHome;float clock;

        static Look Capture()
        {
            var light=RenderSettings.sun;
            return new Look{sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,fog=RenderSettings.fogColor,fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,
                sun=light?light.color:Color.white,sunIntensity=light?light.intensity:1,sunRotation=light?light.transform.rotation:Quaternion.identity,skybox=RenderSettings.skybox};
        }
        void Apply(Look look)
        {
            RenderSettings.ambientSkyColor=look.sky;RenderSettings.ambientEquatorColor=look.equator;RenderSettings.ambientGroundColor=look.ground;
            RenderSettings.fogColor=look.fog;RenderSettings.fogStartDistance=look.fogStart;RenderSettings.fogEndDistance=look.fogEnd;
            var light=RenderSettings.sun;if(light){light.color=look.sun;light.intensity=look.sunIntensity;light.transform.rotation=look.sunRotation;}
            var sky=look.skybox?look.skybox:skybox;
            if(sky&&RenderSettings.skybox!=sky){RenderSettings.skybox=sky;DynamicGI.UpdateEnvironment();}
        }
        // Switching the room on (the player arrives) puts the sunrise in; switching it off (they leave) puts the day back.
        void OnEnable(){if(Application.isPlaying)Enter();}
        void OnDisable(){if(Application.isPlaying)Leave();}
        void OnApplicationQuit(){Leave();}
        void Enter()
        {
            if(!changed){day=Capture();haveDay=true;}
            var look=Sunrise;look.skybox=skybox;Apply(look);changed=true;
            if(balloon&&balloonHome==Vector3.zero)balloonHome=balloon.localPosition;
        }
        void Leave(){if(haveDay&&changed){Apply(day);changed=false;}}
        // For pictures made outside Play Mode: the room lit as it is on arrival. EndPreview puts everything back.
        public void Preview(){if(!changed){day=Capture();haveDay=true;}var look=Sunrise;look.skybox=skybox;Apply(look);changed=true;}
        public void EndPreview(){Leave();}
        public bool Showing=>changed;

        void Update()
        {
            clock+=Time.deltaTime;
            if(drifting!=null)for(int i=0;i<drifting.Length;i++)
            {
                var cloud=drifting[i];if(!cloud)continue;
                var p=cloud.localPosition;p.x+=speeds[i]*Time.deltaTime;if(p.x>120)p.x-=240;cloud.localPosition=p;
            }
            if(balloon)
            {
                if(balloonHome==Vector3.zero)balloonHome=balloon.localPosition;
                balloon.localPosition=balloonHome+Vector3.up*Mathf.Sin(clock*.35f)*.9f;
                balloon.Rotate(0,2.5f*Time.deltaTime,0,Space.Self);
            }
        }
    }
}
