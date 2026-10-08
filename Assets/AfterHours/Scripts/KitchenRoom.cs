using TMPro;
using UnityEngine;
namespace AfterHours
{
    public class KitchenRoom : MonoBehaviour
    {
        public GameDirector director;
        public Grabbable cup;
        public Transform nozzle;
        public GameObject[] crowd, spills;
        public Transform coffee;
        public ParticleSystem spillSpray;
        public TextMeshPro display;
        // A murmur that grows with the watching crowd and falls silent when it leaves.
        public AudioSource murmur;
        public bool helped;
        public int mistakes, fills;
        float nextBrew;
        public void Brew()
        {
            if(Time.time<nextBrew||director.currentRoom!=3)return;nextBrew=Time.time+1;
            if(fills>=3){director.Say("You have enough. Let this cup be enough.");return;}
            bool placed=Vector3.Distance(cup.transform.position,nozzle.position)<.44f;
            if(!helped||!placed)
            {
                mistakes=Mathf.Min(8,mistakes+1);RefreshAudience();if(spillSpray)spillSpray.Play();director.PlayAt(director.spill,nozzle.position,.8f);
                if(spills.Length>0)spills[(mistakes-1)%spills.Length].SetActive(true);
                display.text=!helped?"PRESSURE FAULT\n<color=#EB997F>Support is available.</color>":"PLACE CUP\n<color=#ABC4C5>Below the brass spout.</color>";
                director.Say(!helped?"The machine is failing. That is not a verdict on you. ASK FOR HELP is beside the machine.":"Bring the cup under the spout, then try again. It is okay to adjust.");
                director.SaveCheckpoint();
                return;
            }
            fills++;director.SmallStep();coffee.gameObject.SetActive(true);director.PlayAt(director.pour,nozzle.position,.8f);coffee.localScale=new Vector3(.76f+fills*.045f,.015f,.76f+fills*.045f);coffee.localPosition=new Vector3(0,1.018f,0);
            display.text=fills<3?"A LITTLE AT A TIME\n"+fills+" / 3":"ENOUGH\n<color=#ABC4C5>Take a breath.</color>";
            director.Say("A little warmth. Press BREW again when you want more.");if(fills>=3)director.CompleteRoom(3);
            director.SaveCheckpoint();
        }
        public void AskForHelp()
        {
            helped=true;RefreshAudience();display.text="PRESSURE RELEASED\n<color=#ABC4C5>You do not have to do this alone.</color>";
            director.Say("Someone says: 'That machine does this to everyone.' Put the cup below the spout and brew three small pours.");
            director.Earn("scarf");
            director.SaveCheckpoint();
        }
        public void RestoreProgress(bool help,int errors,int pours)
        {
            helped=help;mistakes=Mathf.Clamp(errors,0,8);fills=Mathf.Clamp(pours,0,3);RefreshAudience();
            for(int i=0;i<spills.Length;i++)spills[i].SetActive(i<mistakes);
            coffee.gameObject.SetActive(fills>0);coffee.localScale=new Vector3(.76f+fills*.045f,.015f,.76f+fills*.045f);coffee.localPosition=new Vector3(0,1.018f,0);
            display.text=fills>=3?"ENOUGH\n<color=#ABC4C5>Take a breath.</color>":fills>0?"A LITTLE AT A TIME\n"+fills+" / 3":helped?"PRESSURE RELEASED\n<color=#ABC4C5>Place your cup below the spout.</color>":"READY?\nOne cup. One ordinary task.";
        }
        public void RefreshAudience()
        {
            int watching=0;for(int i=0;i<crowd.Length;i++){bool on=!helped&&!director.gentleAudience&&i<mistakes;crowd[i].SetActive(on);if(on)watching++;}
            if(murmur)murmur.volume=watching==0?0:Mathf.Lerp(.12f,.5f,watching/(float)Mathf.Max(1,crowd.Length));
        }
        public void ResetRoom(){helped=false;mistakes=0;fills=0;nextBrew=0;RefreshAudience();foreach(var s in spills)s.SetActive(false);coffee.gameObject.SetActive(false);display.text="READY?\nOne cup. One ordinary task.";}
    }
}


