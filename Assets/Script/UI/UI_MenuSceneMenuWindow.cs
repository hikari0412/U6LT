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
        //continueButton.onClick.AddListener(ContinueButtonClick);
        quitButton.onClick.AddListener(quitButtonClick);

        //如果当前没有存档，应该隐藏继续游戏按钮
        if (!DataManager.HaveArchive)
        {
            continueButton.gameObject.SetActive(false);
        }
    }

    public void StartButtonClick()
    {
        UISystem.Close<UI_MenuSceneMenuWindow>(true);

        //创建存档进行游戏
        GameManager.Instance.CreateNewArchiveAndEnterGame();
        
    }

    public void ContinueButtonClick()
    {
        UISystem.Close<UI_MenuSceneMenuWindow>(true);
        //使用存档进行游戏
    }

    public void quitButtonClick()
    {
        //退出游戏
        Application.Quit();
    }

    public override void OnClose()
    {
        base.OnClose();
        //释放自身的AA资源
        ResSystem.UnloadAsset(gameObject);
    }
}
