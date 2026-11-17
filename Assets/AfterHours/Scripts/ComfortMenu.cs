using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    // Comfort and room options. The left controller's menu button, a left-hand palm pinch, or Tab on desktop opens and closes it.
    // It opens at arm's length just below eye level, so every option is in reach from a chair, and it moves and turns with the player.
    // It is drawn as a paper binder with curly rings along the top. The arrows at the foot turn the page over the rings: page 1 holds the
    // comfort options, page 2 resets rooms (tick rooms, or ALL ROOMS, then RESET TICKED ROOMS). It always opens on page 1.
    public class ComfortMenu : MonoBehaviour
    {
        public GameDirector director;
        public TextMeshPro status, seated, height, walking, vignette, crowd, clean, night, gentle, dark, music;
        public GameObject lobby, tryAgain;
        // The binder's pages. Each page's pivot is on the ring line, so squashing it upward looks like the sheet flipping over the rings.
        public GameObject[] pages;
        // Page 2: one tick per resettable room (indexed by room number, null for the lobby), ALL ROOMS, and what was last done.
        public TextMeshPro[] resetToggles;
        public TextMeshPro resetAll, resetNote;
        public int Page{get;private set;}
        public const float FlipSeconds=.16f;
        readonly HashSet<int> ticked=new HashSet<int>();
        public IReadOnlyCollection<int> Ticked=>ticked;
        Coroutine flipping;
        public bool IsOpen => gameObject.activeSelf;
        const float Distance=.65f, Drop=.16f;

        public void Toggle()
        {
            if(IsOpen){Close();director.PlayUi(director.menuClose,.45f);}
            else{Open();director.PlayUi(director.menuOpen,.45f);}
        }
        public void Open()
        {
            var head=director.player.Head;
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
            // Looking straight down: the top of the head still points the way the player faces.
            if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(head.up,Vector3.up);
            if(forward.sqrMagnitude<.01f)forward=director.player.transform.forward;
            OpenAt(head.position,forward);
        }
        public void OpenAt(Vector3 eye,Vector3 forward)
        {
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            // Tilted to face the eye from below, so the whole panel is square to the line of sight.
            var rotation=Quaternion.LookRotation(forward)*Quaternion.Euler(Mathf.Atan2(Drop,Distance)*Mathf.Rad2Deg,0,0);
            var centre=forward*Distance+Vector3.down*Drop;
            // Facing a wall or a counter, the menu comes closer and shrinks to match: it looks the same, but stays in front.
            float fit=1;foreach(var corner in Corners)fit=Mathf.Min(fit,Clearance(eye,centre+rotation*corner));
            transform.SetPositionAndRotation(eye+centre*fit,rotation);transform.localScale=Vector3.one*fit;
            // Opening from closed starts on page 1 with nothing ticked. Moving an open menu (a new seated height) keeps its page.
            if(!IsOpen){ShowPage(0);ticked.Clear();if(resetNote)resetNote.text="";}
            Refresh();gameObject.SetActive(true);
            // The menu travels with the player. Its buttons must never push the player's body.
            var body=director.player.GetComponent<CharacterController>();
            if(body)foreach(var c in GetComponentsInChildren<Collider>())Physics.IgnoreCollision(c,body,true);
        }
        public void Close(){if(IsOpen)gameObject.SetActive(false);flipping=null;ShowPage(Page);}

        // ---- The binder's pages
        public int PageCount=>pages!=null?pages.Length:1;
        // An arrow: the page turns over the rings with a paper sound. Nothing happens past the first or last page.
        public void Flip(int step)
        {
            int next=Mathf.Clamp(Page+step,0,PageCount-1);
            if(next==Page||pages==null)return;
            director.PlayUi(director.pageTurn,.55f);
            if(flipping!=null)StopCoroutine(flipping);
            flipping=isActiveAndEnabled?StartCoroutine(Turn(next)):null;
            if(flipping==null)ShowPage(next);
        }
        IEnumerator Turn(int next)
        {
            var from=pages[Page].transform;
            for(float t=0;t<FlipSeconds;t+=Time.unscaledDeltaTime){from.localScale=new Vector3(1,Mathf.Lerp(1,.02f,t/FlipSeconds),1);yield return null;}
            ShowPage(next);var to=pages[next].transform;
            for(float t=0;t<FlipSeconds;t+=Time.unscaledDeltaTime){to.localScale=new Vector3(1,Mathf.Lerp(.02f,1,t/FlipSeconds),1);yield return null;}
            to.localScale=Vector3.one;flipping=null;
        }
        // Shows one page flat, with every other page hidden.
        public void ShowPage(int index)
        {
            Page=Mathf.Clamp(index,0,PageCount-1);
            if(pages!=null)for(int i=0;i<pages.Length;i++)if(pages[i]){pages[i].SetActive(i==Page);pages[i].transform.localScale=Vector3.one;}
            // An arrow is shown only where there is a page to turn to. Each arrow's group holds its button and its label.
            var back=Button(ActionKind.MenuPage,-1);var forward=Button(ActionKind.MenuPage,1);
            if(back)back.transform.parent.gameObject.SetActive(Page>0);if(forward)forward.transform.parent.gameObject.SetActive(Page<PageCount-1);
        }

        // ---- Page 2: resetting rooms
        public void ToggleReset(int room)
        {
            if(!GameDirector.CanReset(room))return;
            if(!ticked.Remove(room))ticked.Add(room);
            if(resetNote)resetNote.text="";
        }
        // ALL ROOMS: ticks every room, or clears them all when every one is already ticked.
        public void ToggleAllResets()
        {
            bool all=GameDirector.Resettable.All(ticked.Contains);ticked.Clear();
            if(!all)foreach(int r in GameDirector.Resettable)ticked.Add(r);
            if(resetNote)resetNote.text="";
        }
        public bool AllTicked=>GameDirector.Resettable.All(ticked.Contains);
        public void ResetChosen()
        {
            if(ticked.Count==0){if(resetNote)resetNote.text="Tick a room first, or ALL ROOMS.";return;}
            int count=director.ResetRooms(GameDirector.Resettable.Where(ticked.Contains).ToArray());
            ticked.Clear();
            if(resetNote)resetNote.text=count==1?"1 room reset. Finished chapters stay saved.":count+" rooms reset. Finished chapters stay saved.";
        }
        // The panel's centre and corners, relative to its pivot, and the page-turn tabs on either side.
        static readonly Vector3[] Corners={Vector3.zero,new Vector3(-.28f,.345f,0),new Vector3(.28f,.345f,0),new Vector3(-.28f,-.483f,0),new Vector3(.28f,-.483f,0),new Vector3(-.33f,-.068f,0),new Vector3(.33f,-.068f,0)};
        float Clearance(Vector3 eye,Vector3 offset)
        {
            float length=offset.magnitude,fit=1;
            foreach(var hit in Physics.RaycastAll(eye,offset/length,length+.05f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(!hit.collider.transform.IsChildOf(director.player.transform))fit=Mathf.Min(fit,(hit.distance-.05f)/length);
            return Mathf.Clamp(fit,.3f,1);
        }
        public void Refresh()
        {
            var p=director.player;int room=director.currentRoom;
            if(lobby)lobby.SetActive(room!=0);
            if(tryAgain)tryAgain.SetActive(GameDirector.IsChapter(room));
            if(status&&director.rooms!=null&&room<director.rooms.Length)status.text=director.rooms[room].title.ToUpperInvariant();
            string eye=p.SeatedEyeHeight.ToString("0.00",CultureInfo.InvariantCulture)+" m";
            Set(seated,"SEATED VIEW\n"+(p.seatedMode?"ON":"OFF"));
            Set(height,"SEATED HEIGHT\n"+new[]{"LOW","MID","HIGH"}[Mathf.Clamp(p.seatedHeightIndex,0,2)]+"  "+eye);
            Set(walking,"MOVEMENT\n"+(p.smoothMotion?"WALK + TELEPORT":"TELEPORT ONLY"));
            Set(vignette,"COMFORT VIGNETTE\n"+(p.comfortVignette?"ON":"OFF"));
            Set(crowd,"KITCHEN CROWD\n"+(director.gentleAudience?"HIDDEN":"SHOWN"));
            Set(clean,"CLEAN MODE\n"+(p.cleanMode?"ON":"OFF"));
            Set(night,"OCEAN AT NIGHT\n"+(director.ocean&&director.ocean.night?"ON":"OFF"));
            Set(gentle,"GENTLE AMBUSH\n"+(director.gentleAmbush?"ON":"OFF"));
            Set(dark,"DARK OPENING\n"+(director.darkOpening?"ON":"OFF"));
            Set(music,"MUSIC\n"+(director.music&&director.music.on?"ON":"OFF"));
            if(resetToggles!=null&&director.rooms!=null)for(int r=0;r<resetToggles.Length&&r<director.rooms.Length;r++)
                Set(resetToggles[r],director.rooms[r].title.ToUpperInvariant()+"\n"+Tick(ticked.Contains(r)));
            Set(resetAll,"ALL ROOMS\n"+Tick(AllTicked));
        }
        static void Set(TextMeshPro label,string text){if(label)label.text=text;}
        static string Tick(bool on)=>on?"<color=#F2C46B>WILL RESET</color>":"KEEP AS IS";
        public Interactable Button(ActionKind kind)
        {foreach(var b in GetComponentsInChildren<Interactable>(true))if(b.kind==kind)return b;return null;}
        public Interactable Button(ActionKind kind,int value)
        {foreach(var b in GetComponentsInChildren<Interactable>(true))if(b.kind==kind&&b.value==value)return b;return null;}
    }
}
