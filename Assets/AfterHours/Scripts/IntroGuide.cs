using TMPro;
using UnityEngine;
namespace AfterHours
{
    // The first visit: a content note, then pinching and the menu, each taught by doing it once. Shown until finished or skipped.
    public class IntroGuide : MonoBehaviour
    {
        public GameDirector director;
        public TextMeshPro title, body, nextLabel;
        public GameObject next, skip;
        public const string Key="AfterHours.IntroDone";
        public int Step { get; private set; } = -1;
        public bool IsShowing=>gameObject.activeSelf;
        const int MenuStep=2;
        static readonly string[] titles={"Before you begin","Point and pinch","Your menu","Begin anywhere"};
        static readonly string[] buttons={"CONTINUE","GOT IT","","START"};
        string Body(int step)
        {
            bool desktop=director&&director.player&&!director.player.IsXR;
            switch(step)
            {
                case 0: return "After Hours is about losing a job: the shame, the worry, and what is still true.\nTake a break whenever you like. It is a gentle experience, not a substitute for professional support.";
                case 1: return desktop?"Point with the mouse and click to choose.\nTry it on the button below.":"Point with your hand, then touch your thumb and index finger together to choose.\nTry it on the button below.";
                case 2: return desktop?"Press Tab to open the menu.\nIt has seated view, turning, comfort options and the lobby.":"Look at your left palm and pinch to open the menu.\nWith controllers, press the menu button.\nIt has seated view, turning, comfort options and the lobby.";
                default: return "Point at a door and choose ENTER, or CONTINUE at the desk.\nEvery small step saves. Stop whenever you like.";
            }
        }
        public void Begin(){Step=0;Show();gameObject.SetActive(true);}
        public void Next(){if(Step<0)return;Step++;if(Step>=titles.Length)Finish();else Show();}
        public void Skip(){Finish();}
        // Leaving the lobby counts as having found your way: the guide closes for good.
        public void Dismiss(){if(!IsShowing)return;gameObject.SetActive(false);Step=-1;PlayerPrefs.SetInt(Key,1);PlayerPrefs.Save();}
        // Hides the guide without marking it done (automated tests).
        public void Hide(){gameObject.SetActive(false);Step=-1;}
        void Update(){if(Step==MenuStep&&director.menu&&director.menu.IsOpen)Next();}
        void Show()
        {
            title.text=titles[Step];body.text=Body(Step);
            next.SetActive(buttons[Step].Length>0);if(nextLabel)nextLabel.text=buttons[Step];
            skip.SetActive(Step<titles.Length-1);
        }
        void Finish()
        {
            gameObject.SetActive(false);Step=-1;PlayerPrefs.SetInt(Key,1);PlayerPrefs.Save();
            director.Say("Welcome to After Hours. A few minutes is enough. Progress saves after each small step.");
        }
    }
}
