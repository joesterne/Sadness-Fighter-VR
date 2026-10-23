using UnityEngine;
namespace AfterHours
{
    public enum ActionKind { Travel, Brew, Help, Rest, SmoothMotion, GentleAudience, Row, ResetRoom, SeatedMode, SeatedHeight, Resume, TurnLeft, TurnRight, TurnAround, FaceForward, Vignette, CloseMenu, IntroNext, IntroSkip, GoToMirror, WardrobePrev, WardrobeNext, ChooseLine, PutBatBack, NewComputer, CleanMode, NightOcean, MirrorTap, Cabinet, Steam, AmbushFeeling, AmbushReply, AmbushBalloon, AmbushNose, StandUp, GentleAmbush, DarkOpening, WardrobeBuy, Music, KillItWithFire }
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
                case ActionKind.SmoothMotion: director.player.smoothMotion=!director.player.smoothMotion; director.Say(director.player.smoothMotion?Wording.Pick("Walking is on, but it needs the controller sticks. Floor teleport still works with your hands.","Walking on. The left stick walks; floor teleport still works."):Wording.Pick("Teleport only. Point at the floor and release.","Teleport only. Point at the floor and release. Either stick snap turns."));break;
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
                case ActionKind.WardrobeBuy: director.BuyTryingOn();break;
                case ActionKind.ChooseLine: if(director.shredder)director.shredder.Choose(value);break;
                case ActionKind.PutBatBack: if(director.rage)director.rage.PutBatBack();break;
                case ActionKind.NewComputer: if(director.rage)director.rage.NewComputer();break;
                case ActionKind.CleanMode: director.player.ToggleClean();break;
                case ActionKind.NightOcean: if(director.ocean)director.ocean.ToggleNight();break;
                case ActionKind.MirrorTap: if(director.wardrobe&&director.wardrobe.avatar)director.wardrobe.avatar.Tap();break;
                case ActionKind.Cabinet: if(director.kitchen)director.kitchen.ToggleCabinet(value);break;
                case ActionKind.Steam: if(director.kitchen)director.kitchen.Steam();break;
                case ActionKind.AmbushFeeling: if(director.ambush)director.ambush.Feel(value);break;
                case ActionKind.AmbushReply: if(director.ambush)director.ambush.Reply(value);break;
                case ActionKind.AmbushBalloon: if(director.ambush)director.ambush.PopBalloon(value);break;
                case ActionKind.AmbushNose: if(director.ambush)director.ambush.Honk(value);break;
                case ActionKind.StandUp: if(director.ambush)director.ambush.StandUp();break;
                case ActionKind.KillItWithFire: if(director.web)director.web.Burn();break;
                case ActionKind.GentleAmbush: director.gentleAmbush=!director.gentleAmbush; if(director.ambush)director.ambush.OptionChanged(); director.Say(director.gentleAmbush?"Gentle ambush: the clowns are smaller and softer from the start.":"The clowns will be as big as they were. You can change this anytime.");break;
                case ActionKind.Music: if(director.music)director.music.Toggle();break;
                case ActionKind.DarkOpening: director.darkOpening=!director.darkOpening; director.Say(director.darkOpening?"Dark opening on: the ambush starts in the dark, then the lights come on.":"Dark opening off: the ambush starts with the lights on.");break;
            }
            if(kind==ActionKind.SmoothMotion||kind==ActionKind.GentleAudience||kind==ActionKind.GentleAmbush||kind==ActionKind.DarkOpening)director.SaveCheckpoint();
            if(director.menu&&director.menu.IsOpen)director.menu.Refresh();
        }
    }
}
