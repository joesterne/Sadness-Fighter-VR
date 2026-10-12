using UnityEngine;
namespace AfterHours
{
    // The sea around the boat. Three loops run under everything (the swell, water lapping at the hull and, at night, wind),
    // and now and then a gull cries by day or a bell buoy rings far off at night. Switching between day and night fades the
    // loops over a few seconds. Plays only while the ocean room is on, because the room's object is switched off otherwise.
    public class SeaSounds : MonoBehaviour
    {
        public AudioSource swell, lapping, wind;
        public AudioClip[] gulls;
        public AudioClip bell, creak;
        public bool night{get;private set;}
        // Counts of one-shot sounds, so a test can tell the sea is alive.
        public int gullCount{get;private set;}
        public int bellCount{get;private set;}
        float nextGull, nextBell;
        AudioSource voice;
        // Loop volumes by day and by night.
        const float SwellDay=.6f,SwellNight=.4f,LappingDay=.3f,LappingNight=.22f,WindNight=.42f;
        public void SetNight(bool value,bool instant=false)
        {
            night=value;if(instant)Snap();
        }
        // For tests: the next gull or bell comes at once.
        public void Soon(){nextGull=nextBell=Time.time+.1f;}
        void OnEnable()
        {
            Snap();
            nextGull=Time.time+Random.Range(4f,9f);nextBell=Time.time+Random.Range(7f,14f);
        }
        void Snap(){if(swell)swell.volume=night?SwellNight:SwellDay;if(lapping)lapping.volume=night?LappingNight:LappingDay;if(wind)wind.volume=night?WindNight:0;}
        void Update()
        {
            float k=Time.deltaTime*.35f;
            if(swell)swell.volume=Mathf.MoveTowards(swell.volume,night?SwellNight:SwellDay,k);
            if(lapping)lapping.volume=Mathf.MoveTowards(lapping.volume,night?LappingNight:LappingDay,k);
            if(wind)wind.volume=Mathf.MoveTowards(wind.volume,night?WindNight:0,k);
            if(!night&&gulls!=null&&gulls.Length>0&&Time.time>=nextGull)
            {
                Cry(gulls[Random.Range(0,gulls.Length)],Random.Range(.35f,.6f),14,26,5,12);gullCount++;
                nextGull=Time.time+Random.Range(9f,22f);
            }
            if(night&&bell&&Time.time>=nextBell)
            {
                Cry(bell,.45f,25,45,1,3);bellCount++;
                nextBell=Time.time+Random.Range(14f,30f);
            }
        }
        // A sound from a random direction around the boat. Close enough to place by ear, far enough to feel distant.
        void Cry(AudioClip clip,float volume,float minDistance,float maxDistance,float minHeight,float maxHeight)
        {
            if(!clip)return;
            if(!voice)
            {
                var go=new GameObject("Sea voice");go.transform.SetParent(transform,false);voice=go.AddComponent<AudioSource>();
                voice.spatialBlend=.85f;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=12;voice.maxDistance=70;voice.playOnAwake=false;voice.priority=96;
            }
            float angle=Random.Range(0f,Mathf.PI*2),distance=Random.Range(minDistance,maxDistance);
            voice.transform.localPosition=new Vector3(Mathf.Sin(angle)*distance,Random.Range(minHeight,maxHeight),Mathf.Cos(angle)*distance);
            voice.PlayOneShot(clip,volume);
        }
        // The oarlock creaks as an oar goes through a stroke.
        public void Creak(Vector3 at){if(creak)AudioSource.PlayClipAtPoint(creak,at,.5f);}
    }
}
