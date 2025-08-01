using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JKFrame;

public class MenuSceneManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (var key in JKFrame.UISystem.GetAllWindowKeys())
        {
            Debug.Log("已注册窗口Key: " + key);
        }

        //UISystem.Show<UI_MenuSceneMenuWindow>();
        UISystem.Show("UI_MenuSceneMenuWindow");
    }

}
