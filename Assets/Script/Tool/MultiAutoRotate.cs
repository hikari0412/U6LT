using System;
using System.Collections.Generic;
using UnityEngine;

public class MultiAutoRotate : MonoBehaviour
{
    [Serializable]
    public class RotatingItem
    {
        [Tooltip("要旋转的物体")]
        public Transform target;

        [Tooltip("每秒旋转角速度（度/秒），XYZ分别对应三个轴")]
        public Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);
    }

    [Tooltip("需要被旋转的物体列表")]
    public List<RotatingItem> items = new List<RotatingItem>();

    [Tooltip("是否使用不受暂停影响的时间（一般用不到，可以先关掉）")]
    public bool useUnscaledTime = false;

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        foreach (var item in items)
        {
            if (item == null || item.target == null) continue;

            // 围绕自身 pivot、自身坐标系旋转
            item.target.Rotate(item.rotationSpeed * deltaTime, Space.Self);
        }
    }
}
