using UnityEngine;
namespace AfterHours
{
    // The sea around the boat. Under everything run five sounds: waves that roll past at the same beat as the moving water, a
    // quiet bed of open sea under them, water lapping at the hull and, at night, wind, and from the shore itself the surf, which
    // grows as the boat gets nearer. Now and then a gull cries by day or a bell buoy rings far off at night. Switching between
    // day and night fades the loops over a few seconds. Plays only while the ocean room is on, because the room's object is
    // switched off otherwise.
    public class SeaSounds : MonoBehaviour
    {
        // swell: the waves, a loop of four 8-second waves that OceanWaves keeps the water in step with. wash: the open sea under them.
        public AudioSource swell, wash, lapping, wind, surf;
        public AudioClip[] gulls;
        public AudioClip bell, creak;
        public bool night{get;private set;}
        // Counts of one-shot sounds, so a test can tell the sea is alive.
        public int gullCount{get;private set;}
        public int bellCount{get;private set;}
        // The beat of the sea, in seconds into the wave loop. It runs on the game's clock and is nudged back toward the sound's own
        // position now and then, so the water crests when the wave sound does. It is smooth where the sound's position steps.
        public const float WaveLoop=32f;
        public float Clock{get;private set;}
        float nextGull, nextBell, heard=-1;
        AudioSource voice;
        // Loop volumes by day and by night.
        const float SwellDay=.8f,SwellNight=.45f,WashDay=.2f,WashNight=.14f,LappingDay=.3f,LappingNight=.22f,WindNight=.42f,SurfDay=.8f,SurfNight=.5f;
        public void SetNight(bool value,bool instant=false)
        {
            night=value;if(instant)Snap();
        }
        // For tests: the next gull or bell comes at once.
        public void Soon(){nextGull=nextBell=Time.time+.1f;}
        void OnEnable()
        {
            Snap();Clock=0;heard=-1;
            nextGull=Time.time+Random.Range(4f,9f);nextBell=Time.time+Random.Range(7f,14f);
        }
        void Snap()
        {
            if(swell)swell.volume=night?SwellNight:SwellDay;if(wash)wash.volume=night?WashNight:WashDay;
            if(lapping)lapping.volume=night?LappingNight:LappingDay;if(wind)wind.volume=night?WindNight:0;if(surf)surf.volume=night?SurfNight:SurfDay;
        }
        void Update()
        {
            float k=Time.deltaTime*.35f;
            if(swell)swell.volume=Mathf.MoveTowards(swell.volume,night?SwellNight:SwellDay,k);
            if(wash)wash.volume=Mathf.MoveTowards(wash.volume,night?WashNight:WashDay,k);
            if(lapping)lapping.volume=Mathf.MoveTowards(lapping.volume,night?LappingNight:LappingDay,k);
            if(wind)wind.volume=Mathf.MoveTowards(wind.volume,night?WindNight:0,k);
            if(surf)surf.volume=Mathf.MoveTowards(surf.volume,night?SurfNight:SurfDay,k);
            KeepBeat();
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
        // The clock runs smoothly with the game, and leans toward where the wave sound really is. If the sound is not moving (no sound
        // device, or paused with the headset off) it is left alone and the water simply keeps its own time.
        void KeepBeat()
        {
            float dt=Time.deltaTime;Clock=Mathf.Repeat(Clock+dt,WaveLoop);
            if(!swell||!swell.isPlaying)return;
            float position=swell.time;
            if(Mathf.Approximately(position,heard))return;
            heard=position;
            float error=Mathf.Repeat(position-Clock+WaveLoop*.5f,WaveLoop)-WaveLoop*.5f;
            Clock=Mathf.Repeat(Clock+error*Mathf.Min(1,dt*1.5f),WaveLoop);
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
