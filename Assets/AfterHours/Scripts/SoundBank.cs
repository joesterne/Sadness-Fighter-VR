using System.Collections.Generic;
using UnityEngine;
namespace AfterHours
{
    // The editor's scene builder loads the game's audio here so the world builder can wire it into each room.
    public static class SoundBank
    {
        static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        public static void Set(string name,AudioClip clip){clips[name]=clip;}
        public static AudioClip Get(string name){clips.TryGetValue(name,out var clip);return clip;}
    }
}
