using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The greenhouse of small beginnings. Five seed packets wait on the potting bench, each a small intention. A packet sent to the
    // planting bed (or dropped or carried into it) goes into the next empty pot, and its intention goes on a stake in the soil.
    // WATER, or the watering can, waters every pot that has been planted, and a sprout comes up. When all five are planted and watered
    // the chapter is done. After that the garden grows only when you come back: each return grows every watered plant one stage, up to
    // a flower. Nothing is held against you: a plant never wilts, and there is no streak to keep.
    // Lives on the bed's trigger, so it sees packets that are sent, dropped or carried in.
    public class Greenhouse : MonoBehaviour
    {
        public const int Room=8;
        // A pot's stages: 0 planted (fresh soil), 1 a sprout, 2 leaves, 3 in flower.
        public const int Sprout=1, Bloom=3;
        public GameDirector director;
        public Grabbable[] packets;
        // What planting each packet says.
        public string[] replies;
        // Each pot: where the packet goes in, its fresh soil, and the stake that carries the intention.
        public Transform[] soil;
        public GameObject[] mounds;
        public TextMeshPro[] stakes;
        // Each pot's plant, a part for each stage: growth[pot*Bloom+k] appears at stage k+1. Parts are cumulative, so a flower keeps its leaves.
        public GameObject[] growth;
        public ParticleSystem water;
        public AudioClip plantSound, waterSound, growSound;
        // Which packet went into each pot (-1 while empty), and how far each pot has grown.
        public int[] sown, stage;
        // For tests: how often it was watered, and how many returns grew something.
        public int waterings{get;private set;}
        public int returns{get;private set;}
        Vector3[] sizes;
        float nextWater;
        public int Planted=>sown==null?0:sown.Count(x=>x>=0);
        public bool AllWatered=>Planted==packets.Length&&stage.All(s=>s>=Sprout);

        void Awake(){sizes=growth.Select(g=>g?g.transform.localScale:Vector3.one).ToArray();Prepare();}
        void Prepare()
        {
            if(sown==null||sown.Length!=soil.Length){sown=Enumerable.Repeat(-1,soil.Length).ToArray();}
            if(stage==null||stage.Length!=soil.Length)stage=new int[soil.Length];
        }
        void OnTriggerStay(Collider other)
        {
            if(!director||director.currentRoom!=Room)return;
            var item=other.GetComponentInParent<Grabbable>();
            if(!item||item.held||item.sending||item.processed||item.room!=Room)return;
            int packet=Array.IndexOf(packets,item);if(packet>=0)Plant(packet,true);
        }
        // The packet goes into the next empty pot. announce: a new planting while playing, rather than restored progress.
        void Plant(int packet,bool announce,int pot=-1,int grown=0)
        {
            Prepare();if(pot<0)pot=Array.IndexOf(sown,-1);if(pot<0)return;
            var item=packets[packet];item.MarkProcessed(soil[pot]);item.gameObject.SetActive(false);
            sown[pot]=packet;stage[pot]=Mathf.Clamp(grown,0,Bloom);Show(pot);
            if(!announce)return;
            director.PlayAt(plantSound,soil[pot].position,.7f);
            director.AcceptItem(Room,item.label,replies[packet]);
        }
        GameObject Part(int pot,int reached)=>reached>=1&&reached<=Bloom&&pot*Bloom+reached-1<growth.Length?growth[pot*Bloom+reached-1]:null;
        // Shows a pot as it is now: soil and stake once planted, and every part its plant has reached, at full size.
        void Show(int pot)
        {
            bool planted=sown[pot]>=0;
            if(mounds[pot])mounds[pot].SetActive(planted);
            if(stakes[pot]){stakes[pot].transform.parent.gameObject.SetActive(planted);stakes[pot].text=planted?packets[sown[pot]].label:"";}
            for(int k=1;k<=Bloom;k++)
            {
                var part=Part(pot,k);if(!part)continue;
                part.SetActive(planted&&stage[pot]>=k);
                if(sizes!=null)part.transform.localScale=sizes[pot*Bloom+k-1];
            }
        }
        // A part comes up out of the soil over a moment and a half.
        IEnumerator Grow(int pot,int reached,float delay)
        {
            var part=Part(pot,reached);if(!part)yield break;
            var size=sizes[pot*Bloom+reached-1];part.transform.localScale=Vector3.zero;part.SetActive(true);
            if(delay>0)yield return new WaitForSeconds(delay);
            director.PlayAt(growSound,part.transform.position,.45f);
            for(float t=0;t<1;){t=Mathf.Min(1,t+Time.deltaTime/1.4f);part.transform.localScale=size*Mathf.SmoothStep(0,1,t);yield return null;}
        }
        // Leaving mid-growth leaves every plant at its full size.
        void OnDisable(){StopAllCoroutines();if(sown!=null&&sown.Length==soil.Length)for(int pot=0;pot<soil.Length;pot++)Show(pot);}

        // WATER and the watering can. Every planted pot that has not come up yet sprouts.
        public void Water()
        {
            if(!director||director.currentRoom!=Room||Time.time<nextWater)return;
            Prepare();nextWater=Time.time+1.5f;waterings++;
            if(water)water.Play();director.PlayAt(waterSound,water?water.transform.position:transform.position,.6f);
            if(Planted==0){director.Say("Plant a seed first, then water it. There is no hurry.");return;}
            bool grew=false;
            for(int pot=0;pot<soil.Length;pot++)if(sown[pot]>=0&&stage[pot]<Sprout){stage[pot]=Sprout;grew=true;StartCoroutine(Grow(pot,Sprout,.5f+pot*.15f));}
            director.SmallStep();
            if(!grew)director.Say("They have all the water they need. They grow when you come back, not when you push.");
            else if(AllWatered)director.CompleteRoom(Room);
            else director.Say("Watered. It does not have to grow today to count.");
            director.SaveCheckpoint();
        }
        // Coming back to the greenhouse: every plant that has been watered grows one stage, up to a flower. Returns what the room says, or null.
        public string OnArrive()
        {
            Prepare();bool grew=false;float delay=.4f;
            for(int pot=0;pot<soil.Length;pot++)
                if(sown[pot]>=0&&stage[pot]>=Sprout&&stage[pot]<Bloom){stage[pot]++;grew=true;StartCoroutine(Grow(pot,stage[pot],delay));delay+=.2f;}
            if(!grew)return null;
            returns++;director.SaveCheckpoint();
            return stage.Where((s,pot)=>sown[pot]>=0).All(s=>s>=Bloom)?"Everything here is in flower. You came back, and that was enough.":"You came back. Everything you watered grew a little while you were away.";
        }
        // Brings back saved progress: which packet is in each pot, and how far each has grown.
        public void Restore(int[] savedSown,int[] savedStage)
        {
            ResetRoom();
            savedSown=savedSown??Array.Empty<int>();savedStage=savedStage??Array.Empty<int>();
            for(int pot=0;pot<soil.Length&&pot<savedSown.Length;pot++)
            {
                int packet=savedSown[pot];
                if(packet<0||packet>=packets.Length||sown.Contains(packet))continue;
                Plant(packet,false,pot,pot<savedStage.Length?savedStage[pot]:0);
            }
            director.rooms[Room].accepted=Planted;
            if(AllWatered)director.rooms[Room].complete=true;
        }
        public void ResetRoom()
        {
            StopAllCoroutines();Prepare();
            foreach(var packet in packets){packet.gameObject.SetActive(true);packet.ResetObject();}
            for(int pot=0;pot<soil.Length;pot++){sown[pot]=-1;stage[pot]=0;Show(pot);}
            if(water)water.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        // Test fixture: plants a packet at once, as if it had been sent in.
        public void PlantNow(Grabbable packet){int index=Array.IndexOf(packets,packet);if(index>=0&&!packet.processed)Plant(index,true);}
    }
}
