using UnityEngine;

namespace JinXinFramework.Event
{
    public struct FormSwitchedEvent : IEvent
    {
        public bool IsMech;
        public Transform Follow;
        public Transform LookAt;
        public FormSwitchedEvent(bool isMech, Transform follow, Transform lookAt)
        {
            IsMech = isMech;
            Follow = follow;
            LookAt = lookAt;
        }
    }
}
