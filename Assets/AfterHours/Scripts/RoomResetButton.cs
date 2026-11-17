using TMPro;
using UnityEngine;
namespace AfterHours
{
    // RESET ROOM under a lobby door's ENTER button. A room holds progress, so it asks first: the first press changes the label to
    // TAP AGAIN TO RESET, and a second press within a few seconds puts that room back the way it started. Otherwise it settles back.
    public class RoomResetButton : MonoBehaviour
    {
        public const string Idle="RESET ROOM",Confirm="TAP AGAIN TO RESET";
        public const float ConfirmSeconds=4;
        public GameDirector director;
        public int room;
        public TextMeshPro label;
        float armedUntil=-1;
        public bool Armed=>armedUntil>=0&&Time.time<armedUntil;

        public void Press()
        {
            if(!director)return;
            if(!Armed){armedUntil=Time.time+ConfirmSeconds;Show(Confirm);return;}
            Disarm();director.ResetRooms(new[]{room});
        }
        void Update(){if(armedUntil>=0&&Time.time>=armedUntil)Disarm();}
        void OnDisable(){Disarm();}
        void Disarm(){armedUntil=-1;Show(Idle);}
        void Show(string text){if(label&&label.text!=text)label.text=text;}
    }
}
