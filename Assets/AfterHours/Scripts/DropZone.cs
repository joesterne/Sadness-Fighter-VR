using UnityEngine;
namespace AfterHours
{
    public class DropZone : MonoBehaviour
    {
        public string category;
        public int room;
        public Transform[] slots;
        int filled;
        float nextHint;
        void OnTriggerStay(Collider other)
        {
            var item=other.GetComponentInParent<Grabbable>();
            if(!item || item.held || item.processed || item.room!=room || GameDirector.Instance.currentRoom!=room)return;
            if(!string.IsNullOrEmpty(category)&&category!=item.category)
            { if(Time.time>nextHint){GameDirector.Instance.Say("Try the tray that matches this note. There is no penalty."); nextHint=Time.time+4;} return; }
            if(filled>=slots.Length)return;
            item.MarkProcessed(slots[filled++]);GameDirector.Instance.AcceptItem(room,item.label);
        }
        public void ResetZone(){filled=0;}
        public bool RestoreItem(Grabbable item)
        {
            if(item.room!=room||(!string.IsNullOrEmpty(category)&&category!=item.category)||filled>=slots.Length)return false;
            item.MarkProcessed(slots[filled++]);return true;
        }
    }
}
