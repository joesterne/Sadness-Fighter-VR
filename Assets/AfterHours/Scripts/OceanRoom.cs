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
        Vector3 start;bool rowing,initialized;
        void Awake(){Initialize();}
        void Initialize(){if(initialized)return;start=horizon.localPosition;initialized=true;}
        // ChangeRoom clears player.busy after the fade-in completes; clearing it here would enable input mid-fade.
        public void OnArrive(){rowing=false;RestoreProgress(strokes);}
        public void OnDepart()
        {
            StopAllCoroutines();rowing=false;RestoreProgress(strokes);
            if(strokes>=requiredStrokes&&!director.rooms[1].complete)director.CompleteRoom(1);
        }
        public void Stroke()
        {
            if(rowing||director.currentRoom!=1||strokes>=requiredStrokes)return;
            StartCoroutine(Row());
        }
        IEnumerator Row()
        {
            rowing=true;strokes++;UpdateSign();director.SaveCheckpoint();director.PlayUi(director.row,.7f);director.SmallStep();
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
