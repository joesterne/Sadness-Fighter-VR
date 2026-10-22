using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
namespace AfterHours
{
    public enum BreakSound { Plastic, Glass, Metal, Paper }

    // What one hit does to a part of the computer: pieces shown or hidden, the part knocked askew, and debris thrown.
    [Serializable] public class BreakStage
    {
        public GameObject[] show=Array.Empty<GameObject>(), hide=Array.Empty<GameObject>();
        public Rigidbody[] debris=Array.Empty<Rigidbody>();
        // The part's pose after this hit, relative to how it stood: an extra rotation (degrees), offset and squash (zero keeps it).
        public Vector3 tilt, shift, scale;
        // What the screen says after this hit (the monitor only). Empty keeps the last text.
        public string screen;
    }

    // One part of the old work computer. Every hit breaks it a little more, and the last one breaks it apart.
    public class Breakable : MonoBehaviour
    {
        public string label;
        public BreakSound sound;
        public Transform visual;
        public BreakStage[] stages=Array.Empty<BreakStage>();
        public Collider[] hitVolumes=Array.Empty<Collider>();
        public TextMeshPro screenText;
        public string screenStart, brokenLine;
        // Things standing on this part (the laptop and the work phone on the side desk). When it breaks, they go down with it.
        public Breakable[] riders=Array.Empty<Breakable>();
        public int damage;
        [NonSerialized] public float nextHit;
        public int HitsToBreak=>stages.Length;
        public bool Broken=>damage>=stages.Length;
        // Debris waits inside the part. Where each piece belongs is remembered, so a new computer puts every piece back.
        Rigidbody[] pieces;Transform[] parents;Vector3[] positions;Quaternion[] rotations;bool[] shown;
        Vector3 visualPosition,visualScale;Quaternion visualRotation;bool ready;
        void Ready()
        {
            if(ready)return;ready=true;
            pieces=stages.SelectMany(s=>s.debris).Where(r=>r).Distinct().ToArray();
            parents=pieces.Select(p=>p.transform.parent).ToArray();positions=pieces.Select(p=>p.transform.localPosition).ToArray();
            rotations=pieces.Select(p=>p.transform.localRotation).ToArray();shown=pieces.Select(p=>p.gameObject.activeSelf).ToArray();
            visualPosition=visual.localPosition;visualRotation=visual.localRotation;visualScale=visual.localScale;
        }
        // Puts the part back as it was built: whole, upright, every piece in place.
        public void Mend()
        {
            Ready();StopAllCoroutines();damage=0;
            for(int i=0;i<pieces.Length;i++)
            {
                var p=pieces[i];p.isKinematic=true;p.transform.SetParent(parents[i],false);
                p.transform.localPosition=positions[i];p.transform.localRotation=rotations[i];p.gameObject.SetActive(shown[i]);
            }
            for(int k=stages.Length-1;k>=0;k--){foreach(var g in stages[k].hide)if(g)g.SetActive(true);foreach(var g in stages[k].show)if(g)g.SetActive(false);}
            Pose(-1);Refresh();
        }
        // One more hit. Its debris flies with the bat and away from the player (away is level, pointing from the player
        // to the part). Returns how many pieces flew, or -1 if the part was already broken.
        public int Strike(Vector3 push,Vector3 away)
        {
            Ready();if(Broken)return -1;
            var stage=stages[damage];damage++;
            int thrown=Throw(stage,push,away,true);Show(stage);Pose(damage-1);Refresh();
            if(isActiveAndEnabled)StartCoroutine(Shake(push));
            return thrown;
        }
        // Brings back saved damage without the drama: debris settles where it broke off.
        public void Restore(int saved)
        {
            Mend();damage=Mathf.Clamp(saved,0,stages.Length);
            for(int k=0;k<damage;k++){Throw(stages[k],Vector3.zero,Vector3.zero,false);Show(stages[k]);}
            Pose(damage-1);Refresh();
        }
        int Throw(BreakStage stage,Vector3 push,Vector3 away,bool fly)
        {
            var room=transform.root;int count=0;
            foreach(var p in stage.debris)
            {
                if(!p)continue;count++;
                // Leave the part first, so hiding the broken part never hides its pieces.
                p.transform.SetParent(room,true);p.gameObject.SetActive(true);p.isKinematic=false;
                // A piece starts inside the part's own hit volume: it must fall away from it, not be pushed out of it.
                foreach(var c in p.GetComponentsInChildren<Collider>())foreach(var v in hitVolumes)if(v)Physics.IgnoreCollision(c,v,true);
                if(!fly){p.linearVelocity=Vector3.zero;p.angularVelocity=Vector3.zero;continue;}
                // Small bits scatter wider and spin faster than big pieces. Nothing flies back toward the player.
                bool small=p.mass<.05f;
                var velocity=push+UnityEngine.Random.insideUnitSphere*(small?1.5f:.85f)+Vector3.up*UnityEngine.Random.Range(.6f,1.6f)+away*UnityEngine.Random.Range(.2f,.8f);
                float toward=Vector3.Dot(velocity,away);if(toward<0)velocity-=away*toward;
                p.linearVelocity=velocity;p.angularVelocity=UnityEngine.Random.insideUnitSphere*(small?14:9);
            }
            return count;
        }
        static void Show(BreakStage stage){foreach(var g in stage.show)if(g)g.SetActive(true);foreach(var g in stage.hide)if(g)g.SetActive(false);}
        void Pose(int stage)
        {
            Vector3 tilt=Vector3.zero,shift=Vector3.zero,scale=Vector3.one;
            for(int k=0;k<=stage&&k<stages.Length;k++){tilt=stages[k].tilt;shift=stages[k].shift;if(stages[k].scale!=Vector3.zero)scale=stages[k].scale;}
            visual.localRotation=visualRotation*Quaternion.Euler(tilt);visual.localPosition=visualPosition+shift;visual.localScale=Vector3.Scale(visualScale,scale);
        }
        void Refresh()
        {
            foreach(var c in hitVolumes)if(c)c.enabled=!Broken;
            if(!screenText)return;
            string text=screenStart;for(int k=0;k<damage;k++)if(!string.IsNullOrEmpty(stages[k].screen))text=stages[k].screen;
            screenText.text=text;
        }
        // A short jolt in the direction of the hit, then the part settles into its new pose.
        IEnumerator Shake(Vector3 push)
        {
            var rest=visual.localPosition;var away=transform.InverseTransformDirection(push.normalized)*.025f;
            for(float t=0;t<.18f;t+=Time.deltaTime){visual.localPosition=rest+away*Mathf.Sin(t*55)*(1-t/.18f);yield return null;}
            visual.localPosition=rest;
        }
        void OnDisable(){if(ready&&visual)Pose(damage-1);}
    }
}
