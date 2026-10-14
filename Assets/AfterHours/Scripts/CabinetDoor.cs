using UnityEngine;
namespace AfterHours
{
    // A kitchen cupboard door. This object is the hinge: the door hangs from it and swings about its up axis, out into the room.
    // Pointing at a door and pressing opens it; pointing again shuts it. Opening is silent apart from the creak and knock, and nothing is counted.
    public class CabinetDoor : MonoBehaviour
    {
        // Degrees the door swings when open: positive when hinged on the left (the free edge comes toward the player), negative on the right.
        public float openAngle=100;
        public bool open{get;private set;}
        // Where the door is now, in degrees, between 0 (shut) and openAngle.
        public float angle{get;private set;}
        float Target=>open?openAngle:0;
        public bool Settled=>Mathf.Abs(angle-Target)<.5f;

        public void Toggle(){open=!open;}
        // Shuts or opens the door at once, with no swing.
        public void Snap(bool isOpen){open=isOpen;angle=Target;transform.localRotation=Quaternion.Euler(0,angle,0);}
        void Update()
        {
            if(Settled){if(angle!=Target){angle=Target;transform.localRotation=Quaternion.Euler(0,angle,0);}return;}
            // Quick at first, easing as it arrives.
            float step=Mathf.Max(40,Mathf.Abs(Target-angle)*6)*Time.deltaTime;
            angle=Mathf.MoveTowards(angle,Target,step);transform.localRotation=Quaternion.Euler(0,angle,0);
        }
    }
}
