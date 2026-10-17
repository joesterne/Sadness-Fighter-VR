using UnityEngine;

namespace AfterHours
{
    // Thumb microgestures, for hands-only play: small movements of the thumb against a relaxed hand, which Quest 3 recognises without
    // any controller and which work with a hand resting in a lap.
    //   Swipe the thumb back along the side of the finger: "Back to the lobby?"
    //   Tap the thumb against the side of the finger, within a few seconds: go.
    //   Any other swipe, or waiting: stay where you are.
    // It does what the right controller's B button does, with one step more so that a stray swipe cannot send anyone anywhere.
    // The game saves on the way out, so nothing is lost either way.
    public class ThumbShortcuts : MonoBehaviour
    {
        public VRPlayer player;
        public const float Window=4;
        public enum Outcome { Ignored, Asked, Stayed, Went }
        [System.NonSerialized] public float armedUntil;
        [System.NonSerialized] public int gesturesSeen;
        float lastAt=-10; OVRHand.MicrogestureType lastGesture=OVRHand.MicrogestureType.NoGesture;
        public bool Armed=>Time.time<armedUntil;
        void Update()
        {
            if(!player||!player.IsXR)return;
            // Asking each frame also tells the headset that the game wants microgestures.
            for(int i=0;i<2;i++)
            {
                var hand=i==0?player.leftHand:player.rightHand;if(!hand)continue;
                var gesture=hand.GetMicrogestureType();
                if(gesture==OVRHand.MicrogestureType.NoGesture||gesture==OVRHand.MicrogestureType.Invalid)continue;
                // One gesture is reported once; a repeat of the same one within a moment is the same movement.
                if(gesture==lastGesture&&Time.time-lastAt<.3f)continue;
                lastGesture=gesture;lastAt=Time.time;
                var result=Handle(gesture);
                Debug.Log("[AfterHours] Thumb gesture "+gesture+" on the "+(i==0?"left":"right")+" hand: "+result);
            }
        }
        // What each gesture does. Tests call this directly, since the editor has no hands.
        public Outcome Handle(OVRHand.MicrogestureType gesture)
        {
            gesturesSeen++;
            var d=GameDirector.Instance;
            if(!d||!player||player.busy)return Outcome.Ignored;
            switch(gesture)
            {
                case OVRHand.MicrogestureType.SwipeBackward:
                    if(d.currentRoom==0)return Outcome.Ignored;
                    armedUntil=Time.time+Window;
                    player.ShowHint("Back to the lobby? Tap your thumb on the side of your finger to go. Swipe forward to stay.",Window);
                    d.PlayUi(d.menuOpen,.45f);
                    return Outcome.Asked;
                case OVRHand.MicrogestureType.ThumbTap:
                    if(!Armed)return Outcome.Ignored;
                    armedUntil=0;d.Travel(0);
                    return Outcome.Went;
                case OVRHand.MicrogestureType.SwipeForward:
                case OVRHand.MicrogestureType.SwipeLeft:
                case OVRHand.MicrogestureType.SwipeRight:
                    if(!Armed)return Outcome.Ignored;
                    armedUntil=0;player.ShowHint("Staying here.",2);d.PlayUi(d.menuClose,.45f);
                    return Outcome.Stayed;
            }
            return Outcome.Ignored;
        }
    }
}
