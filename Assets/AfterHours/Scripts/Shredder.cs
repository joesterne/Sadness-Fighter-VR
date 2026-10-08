using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The old résumé. A page sent into the shredder's slot stands up in it, is drawn down and shredded, and the paper
    // in the bin grows. When all five are gone, a blank page asks for one line that is still true.
    // Lives on the slot's trigger, so it sees pages that are sent, dropped or carried in.
    public class Shredder : MonoBehaviour
    {
        public const int Room=6;
        public GameDirector director;
        public Grabbable[] pages;
        public string[] farewells;
        public Transform feed;
        public GameObject[] piles;
        public ParticleSystem strips;
        public GameObject blankPage, choices;
        public TextMeshPro blankText;
        public string[] lines;
        public string line="";
        public int shredded;
        bool busy;Grabbable current;
        public const string Blank="<size=55%><color=#5B6F7A>STILL TRUE ABOUT ME</color></size>\n\n_______________\n\n<size=50%><color=#5B6F7A>Choose one line below.</color></size>";
        public bool Offering=>choices&&choices.activeSelf;
        public bool Busy=>busy;

        void OnTriggerStay(Collider other)
        {
            if(busy||!director||director.currentRoom!=Room)return;
            var item=other.GetComponentInParent<Grabbable>();
            if(!item||item.held||item.sending||item.processed||item.room!=Room||Array.IndexOf(pages,item)<0)return;
            StartCoroutine(Shred(item));
        }
        IEnumerator Shred(Grabbable item)
        {
            busy=true;current=item;item.MarkProcessed(feed);
            director.PlayAt(director.shred,feed.position,.8f);if(strips)strips.Play();
            var from=feed.position;
            for(float t=0;t<1;){t=Mathf.Min(1,t+Time.deltaTime/1.3f);item.transform.position=from+Vector3.down*(.62f*Mathf.SmoothStep(0,1,t));yield return null;}
            current=null;busy=false;Finish(item);
            if(shredded>=pages.Length)StartCoroutine(OfferSoon());
        }
        void Finish(Grabbable item){Gone(item);director.AcceptItem(Room,item.label,farewells[Array.IndexOf(pages,item)]);}
        // Leaving mid-shred finishes the page at once; arriving after the last one offers the blank page.
        void OnDisable(){if(busy&&current){var item=current;current=null;busy=false;StopAllCoroutines();Finish(item);}}
        void OnEnable(){if(director&&shredded>=pages.Length&&string.IsNullOrEmpty(line)&&!Offering)Offer(false);}
        void Gone(Grabbable item)
        {
            item.gameObject.SetActive(false);
            if(shredded<piles.Length)piles[shredded].SetActive(true);
            shredded++;
        }
        IEnumerator OfferSoon(){yield return new WaitForSeconds(3.5f);Offer(true);}
        void Offer(bool announce)
        {
            if(!string.IsNullOrEmpty(line))return;
            blankPage.SetActive(true);blankText.text=Blank;choices.SetActive(true);
            if(announce)director.Say("The old pages are gone. One page is blank. Choose one line that is still true about you.");
        }
        public void Choose(int index)
        {
            if(!Offering||index<0||index>=lines.Length)return;
            line=lines[index];choices.SetActive(false);Write();
            if(director.wardrobe)director.wardrobe.SetLine(line);
            director.CompleteRoom(Room);
        }
        void Write(){blankPage.SetActive(true);blankText.text="<size=55%><color=#5B6F7A>STILL TRUE ABOUT ME</color></size>\n\n"+line;}
        // Brings back saved progress: which pages were shredded, and the line chosen, if any.
        public void Restore(Func<Grabbable,bool> wasShredded,string savedLine)
        {
            ResetRoom();
            foreach(var page in pages)if(wasShredded(page)){page.MarkProcessed(feed);Gone(page);}
            director.rooms[Room].accepted=shredded;
            if(shredded<pages.Length)return;
            if(!string.IsNullOrEmpty(savedLine)&&lines.Contains(savedLine)){line=savedLine;Write();director.rooms[Room].complete=true;}
            else{blankPage.SetActive(true);blankText.text=Blank;choices.SetActive(true);}
        }
        public void ResetRoom()
        {
            StopAllCoroutines();busy=false;current=null;shredded=0;line="";
            foreach(var page in pages){page.gameObject.SetActive(true);page.ResetObject();}
            foreach(var pile in piles)pile.SetActive(false);
            if(strips)strips.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            blankPage.SetActive(false);choices.SetActive(false);blankText.text=Blank;
        }
        // Test fixture: shreds a page at once, as if it had been sent in.
        public void ShredNow(Grabbable page)
        {
            if(!page||page.processed||Array.IndexOf(pages,page)<0)return;
            page.MarkProcessed(feed);Finish(page);
            if(shredded>=pages.Length)Offer(true);
        }
    }
}
