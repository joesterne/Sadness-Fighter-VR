using UnityEngine;
namespace AfterHours
{
    // Calm background music that plays quietly under every room's own ambience. The lobby and the rooftop keep the after hours
    // theme; each chapter has a slower, softer piece of its own (Quiet hours where none is set). A room change swaps the piece while the screen is dark, and each piece
    // carries on from where it was left, so coming back to the lobby does not start the theme over. MUSIC in the menu fades it out
    // (and back in), and the choice is kept between sessions.
    public class Music : MonoBehaviour
    {
        public const string Key="AfterHours.Music";
        public const float Volume=.2f, FadeSeconds=1.5f;
        public GameDirector director;
        public AudioSource source;
        // theme plays in these rooms; a room's own piece (byRoom, by room number) or calm everywhere else. Without calm, the theme plays everywhere.
        public AudioClip theme, calm;
        public AudioClip[] byRoom;
        // The rage room's beat gives way to this once everything there is broken and the room goes quiet, and comes back with a new computer.
        public AudioClip afterRage;
        public int[] themeRooms={0,5};
        public bool on{get;private set;}=true;
        public float Level=>source?source.volume:0;
        public AudioClip Playing=>source&&source.isPlaying?source.clip:null;
        readonly System.Collections.Generic.Dictionary<AudioClip,float> left=new System.Collections.Generic.Dictionary<AudioClip,float>();
        void Awake()
        {
            on=PlayerPrefs.GetInt(Key,1)==1;
            if(source){source.loop=true;source.playOnAwake=false;source.spatialBlend=0;source.volume=on?Volume:0;}
        }
        public AudioClip ClipFor(int room)
        {
            if(!calm||System.Array.IndexOf(themeRooms,room)>=0)return theme;
            return byRoom!=null&&room>=0&&room<byRoom.Length&&byRoom[room]?byRoom[room]:calm;
        }
        // What should be playing now: the room's piece, or the quiet piece in a rage room that has gone quiet.
        public AudioClip Wanted
        {
            get
            {
                if(!director)return theme;
                var rage=director.rage;int room=director.currentRoom;
                if(room==RageRoom.Room&&afterRage&&rage&&(rage.Quieting||rage.Calm))return afterRage;
                return ClipFor(room);
            }
        }
        public void Toggle()
        {
            on=!on;PlayerPrefs.SetInt(Key,on?1:0);PlayerPrefs.Save();
            if(director)director.Say(on?"Music on.":"Music off. The rooms keep their own quiet sounds.");
        }
        void Update()
        {
            if(!source)return;
            var wanted=Wanted;bool easing=false;
            // Within the rage room the beat eases out before the quiet piece comes in (and back), rather than cutting. A room change
            // swaps at once, while the screen is dark.
            if(wanted&&source.clip&&source.clip!=wanted&&source.volume>0&&director&&director.currentRoom==RageRoom.Room&&!director.player.busy)easing=true;
            else if(wanted&&source.clip!=wanted)
            {
                // Remember where the old piece was, and pick the new one up where it was left.
                if(source.clip)left[source.clip]=source.time;
                bool was=source.isPlaying;source.clip=wanted;
                left.TryGetValue(wanted,out float at);source.time=at<wanted.length?at:0;
                if(was)source.Play();
            }
            float target=on&&!easing?Volume:0;
            source.volume=Mathf.MoveTowards(source.volume,target,Volume/FadeSeconds*Time.unscaledDeltaTime);
            // Silent music is paused, so it costs nothing and resumes where it stopped.
            if(source.volume<=0){if(source.isPlaying)source.Pause();}
            else if(!source.isPlaying&&source.clip){source.UnPause();if(!source.isPlaying)source.Play();}
        }
    }
}
