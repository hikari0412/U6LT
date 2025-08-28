using System.Collections.Generic;
using UnityEngine;
using JKFrame;

public class GameManager : SingletonMono<GameManager>
{
    /// <summary>
    /// 创建新存档
    /// </summary>
    public void CreateNewArchiveAndEnterGame()
    {
        //初始化存档
        DataManager.CreateArchive();
        //进入游戏场景
        SceneSystem.LoadScene("GameScene");
    }

    /// <summary>
    /// 使用存档，进入游戏
    /// </summary>
    public void UseCurrentArchiveAndEnterGame()
    {
        // TODO:读取存档并进入游戏场景
    }
}
