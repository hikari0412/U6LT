using UnityEngine;
using UnityEngine.Playables;

public class TimelineEvents : MonoBehaviour
{
    public PlayableDirector director;

    public void OnTimelineFinished()
    {
        // 在这里写你要做的事
        Debug.Log("Timeline 播放完毕！");
        
        // 停止Timeline
        director.Stop();
    }
}
