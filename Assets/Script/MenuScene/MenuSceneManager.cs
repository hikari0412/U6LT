using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;

public class MenuSceneManager : MonoBehaviour
{
    void Start()
    {
        // 打印已注册窗口Key（debug用）
        foreach (var key in JKFrame.UISystem.GetAllWindowKeys())
        {
            Debug.Log("已注册窗口Key: " + key);
        }

        // 打开菜单窗口
        UISystem.Show("UI_MenuSceneMenuWindow");
    }

}
