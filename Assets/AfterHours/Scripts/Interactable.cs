using UnityEngine;
namespace AfterHours
{
    public enum ActionKind { Travel, Brew, Help, Rest, SmoothMotion, GentleAudience, Row, ResetRoom, SeatedMode, SeatedHeight, Resume, TurnLeft, TurnRight, TurnAround, FaceForward, Vignette, CloseMenu, IntroNext, IntroSkip, GoToMirror, WardrobePrev, WardrobeNext, ChooseLine, PutBatBack, NewComputer }
    public class Interactable : MonoBehaviour
    {
        public ActionKind kind;
        public GameDirector director;
        public int value;
        public string prompt;
        public void Activate()
        {
            if (!director) return;
            switch(kind)
            {
                case ActionKind.Travel: director.Travel(value); break;
                case ActionKind.Brew: director.kitchen.Brew(); break;
                case ActionKind.Help: director.kitchen.AskForHelp(); break;
                case ActionKind.Rest: director.Travel(0); break;
                case ActionKind.SmoothMotion: director.player.smoothMotion=!director.player.smoothMotion; director.Say(director.player.smoothMotion?"Walking on. The left stick walks; floor teleport still works.":"Teleport only. Point at the floor and release. Either stick snap turns.");break;
                case ActionKind.GentleAudience: director.gentleAudience=!director.gentleAudience; director.kitchen.RefreshAudience(); director.Say(director.gentleAudience?"Quiet kitchen: no watching crowd.":"Watching crowd enabled. You can change this anytime.");break;
                case ActionKind.Row: director.ocean.Stroke();break;
                case ActionKind.ResetRoom: if(director.menu)director.menu.Close();director.RestartCurrentRoom();break;
                case ActionKind.SeatedMode: director.player.ToggleSeated();break;
                case ActionKind.SeatedHeight: director.player.CycleSeatedHeight();break;
                case ActionKind.Resume: director.Travel(director.lastRoom>0?director.lastRoom:1);break;
                case ActionKind.TurnLeft: director.player.SnapTurn(-30);break;
                case ActionKind.TurnRight: director.player.SnapTurn(30);break;
                case ActionKind.TurnAround: director.player.SnapTurn(180);break;
                case ActionKind.FaceForward: director.player.FaceForward();break;
                case ActionKind.Vignette: director.player.ToggleVignette();break;
                case ActionKind.CloseMenu: if(director.menu)director.menu.Close();break;
                case ActionKind.IntroNext: if(director.intro)director.intro.Next();break;
                case ActionKind.IntroSkip: if(director.intro)director.intro.Skip();break;
                case ActionKind.GoToMirror: director.GoToMirror();break;
                case ActionKind.WardrobePrev: if(director.wardrobe)director.wardrobe.Cycle(value,-1);break;
                case ActionKind.WardrobeNext: if(director.wardrobe)director.wardrobe.Cycle(value,1);break;
                case ActionKind.ChooseLine: if(director.shredder)director.shredder.Choose(value);break;
                case ActionKind.PutBatBack: if(director.rage)director.rage.PutBatBack();break;
                case ActionKind.NewComputer: if(director.rage)director.rage.NewComputer();break;
            }
            if(kind==ActionKind.SmoothMotion||kind==ActionKind.GentleAudience)director.SaveCheckpoint();
            if(director.menu&&director.menu.IsOpen)director.menu.Refresh();
        }
    }
}
