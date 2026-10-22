using UnityEngine;
namespace AfterHours
{
    // Calm background music that plays quietly under every room's own ambience. The lobby and the rooftop keep the after hours
    // theme; the chapters share a slower, softer piece. A room change swaps the piece while the screen is dark, and each piece
    // carries on from where it was left, so coming back to the lobby does not start the theme over. MUSIC in the menu fades it out
    // (and back in), and the choice is kept between sessions.
    public class Music : MonoBehaviour
    {
        public const string Key="AfterHours.Music";
        public const float Volume=.2f, FadeSeconds=1.5f;
        public GameDirector director;
        public AudioSource source;
        // theme plays in these rooms; calm everywhere else. Without calm, the theme plays everywhere.
        public AudioClip theme, calm;
        public int[] themeRooms={0,5};
        public bool on{get;private set;}=true;
        public float Level=>source?source.volume:0;
        public AudioClip Playing=>source&&source.isPlaying?source.clip:null;
        float themeTime, calmTime;
        void Awake()
        {
            on=PlayerPrefs.GetInt(Key,1)==1;
            if(source){source.loop=true;source.playOnAwake=false;source.spatialBlend=0;source.volume=on?Volume:0;}
        }
        public AudioClip ClipFor(int room)=>!calm||System.Array.IndexOf(themeRooms,room)>=0?theme:calm;
        public void Toggle()
        {
            on=!on;PlayerPrefs.SetInt(Key,on?1:0);PlayerPrefs.Save();
            if(director)director.Say(on?"Music on.":"Music off. The rooms keep their own quiet sounds.");
        }
        void Update()
        {
            if(!source)return;
            var wanted=director?ClipFor(director.currentRoom):theme;
            if(wanted&&source.clip!=wanted)
            {
                // Remember where the old piece was, and pick the new one up where it was left.
                if(source.clip==theme)themeTime=source.time;else if(source.clip==calm)calmTime=source.time;
                bool was=source.isPlaying;source.clip=wanted;
                float at=wanted==theme?themeTime:calmTime;source.time=at<wanted.length?at:0;
                if(was)source.Play();
            }
            float target=on?Volume:0;
            source.volume=Mathf.MoveTowards(source.volume,target,Volume/FadeSeconds*Time.unscaledDeltaTime);
            // Silent music is paused, so it costs nothing and resumes where it stopped.
            if(source.volume<=0){if(source.isPlaying)source.Pause();}
            else if(!source.isPlaying&&source.clip){source.UnPause();if(!source.isPlaying)source.Play();}
        }
    }
}
