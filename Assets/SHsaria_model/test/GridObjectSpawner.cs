using UnityEngine;

public class GridObjectSpawner : MonoBehaviour
{
    [Header("使用方法：右键组件标题栏 → 选择“生成网格”，即可自动生成。")]
    [Space(5)]

    [Header("要复制的物体")]
    public GameObject objectToClone;

    [Header("X方向复制数 (填0或负数则不在X方向复制)")]
    public int countX = 3;

    [Header("Z方向复制数 (填0或负数则不在Z方向复制)")]
    public int countZ = 3;

    [Header("X轴间隔")]
    public float intervalX = 5f;

    [Header("Z轴间隔")]
    public float intervalZ = 5f;

    [Header("每个复制体的旋转 (欧拉角)")]
    public Vector3 rotationEuler = Vector3.zero;

    [Header("每个复制体的缩放")]
    public Vector3 scale = Vector3.one;

    [Header("父级节点（为空则为本物体）")]
    public Transform parent;

    [ContextMenu("生成网格")]
    public void SpawnGrid()
    {
        if (objectToClone == null)
        {
            Debug.LogError("objectToClone 未指定！");
            return;
        }

        // 清除原有
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        // 至少有一个方向为1或更大才生成
        if (countX < 1 && countZ < 1)
        {
            Debug.LogWarning("countX 和 countZ 都小于 1，无物体生成。");
            return;
        }

        Quaternion rot = Quaternion.Euler(rotationEuler);
        Transform parentTrans = parent == null ? transform : parent;
        Vector3 origin = parentTrans.position;

        for (int x = 0; x < (countX < 1 ? 1 : countX); x++)
        {
            for (int z = 0; z < (countZ < 1 ? 1 : countZ); z++)
            {
                Vector3 offset = new Vector3(
                    (countX < 1 ? 0 : x * intervalX),
                    0,
                    (countZ < 1 ? 0 : z * intervalZ)
                );
                Vector3 pos = origin + offset;

                GameObject clone = Instantiate(objectToClone, pos, rot, parentTrans);
                clone.transform.localScale = scale;
                clone.name = $"{objectToClone.name}_{x}_{z}";
            }
        }
    }
}
