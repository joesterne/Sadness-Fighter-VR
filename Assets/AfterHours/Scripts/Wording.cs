using System;
using UnityEngine;
namespace AfterHours
{
    public enum InputKind { Hands, Controllers }
    // Whether the player is using tracked hands or controllers right now, and the words that fit. Hands are the default:
    // the game starts in hands wording and only changes when the headset reports controllers in use.
    public static class Wording
    {
        // A change has to hold this long before the wording follows, so a hand passing a controller never flickers the signs.
        public const float SwitchDelay=.6f;
        public static InputKind Kind { get; private set; } = InputKind.Hands;
        public static bool Controllers=>Kind==InputKind.Controllers;
        // Bumps on every change, so signs can notice cheaply.
        public static int Version { get; private set; }
        static bool confirmed; static InputKind pending; static float pendingSince=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset(){Kind=InputKind.Hands;Version=0;confirmed=false;pendingSince=-1;}
        public static void Set(InputKind kind){if(Kind==kind)return;Kind=kind;Version++;pendingSince=-1;}
        public static string Pick(string hands,string controllers)=>Controllers?controllers:hands;
        // What the headset reports this frame, or null when it reports nothing useful. The first report is believed at once;
        // later changes have to hold for SwitchDelay seconds.
        public static void Observe(InputKind? seen,float now)
        {
            if(!seen.HasValue){pendingSince=-1;return;}
            if(seen.Value==Kind){confirmed=true;pendingSince=-1;return;}
            if(!confirmed){confirmed=true;Set(seen.Value);return;}
            if(pendingSince<0||pending!=seen.Value){pending=seen.Value;pendingSince=now;return;}
            if(now-pendingSince>=SwitchDelay)Set(seen.Value);
        }
        // Common phrases, so the same action is never described two ways.
        public static string Choose=>Pick("Point with your hand, then touch your thumb and index finger together to choose.","Point with your controller, then squeeze the trigger to choose.");
        public static string OpenMenu=>Pick("Look at your left palm and pinch to open the menu.","Press the menu button on your left controller to open the menu.");
        public static string MenuShort=>Pick("a left palm pinch","the left menu button");
        public static string HoldAndLetGo=>Pick("Pinch an object to hold it. Point where it belongs and let go.","Squeeze the trigger or the grip to hold an object. Point where it belongs and let go.");
        public static string PutBack=>Pick("pinch","squeeze the trigger");
        public static string Lobby=>Pick("The menu's LOBBY button returns here from any room.","B / Y returns here from any room.");
        public static string Turning=>Pick("Teleports turn you to face where you point. TURN in the menu turns you too.","Right stick turns; pull it back to turn around. Teleports turn you to face where you point.");
    }
}
