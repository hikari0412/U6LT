using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JKFrame;

[UIWindowData("UI_MenuSceneMenuWindow", false, "UI_MenuSceneMenuWindow", 0)]

public class UI_MenuSceneMenuWindow : UI_WindowBase
{
    [SerializeField] Button startButton;
    [SerializeField] Button continueButton;
    [SerializeField] Button quitButton;

    public override void Init()
    {
        startButton.onClick.AddListener(StartButtonClick);
        continueButton.onClick.AddListener(ContinueButtonClick);
        quitButton.onClick.AddListener(quitButtonClick);

        //TODO:如果当前没有存档，应该隐藏继续游戏按钮
    }

    public void StartButtonClick()
    {
        //使用当前存档进行游戏
        UISystem.Close<UI_MenuSceneMenuWindow>(true);
    }

    public void ContinueButtonClick()
    {
        //创建存档进行游戏-》进入自定义角色场景
        UISystem.Close<UI_MenuSceneMenuWindow>(true);
    }

    public void quitButtonClick()
    {
        Application.Quit();
    }

    public override void OnClose()
    {
        base.OnClose();
        //释放自身的AA资源
        ResSystem.UnloadAsset(gameObject);
    }
}
