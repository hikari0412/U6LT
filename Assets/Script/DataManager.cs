using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using JKFrame;

/// <summary>
/// 数据管理器  
/// </summary>

public static class DataManager
{
    static DataManager()
    {
        //初始化数据
        LoadSaveData();
    }

    /// <summary>
    /// 是否有存档  
    /// </summary>
    public static bool HaveArchive{get ; private set;}

    /// <summary>
    /// 加载存档数据    
    /// </summary>
    private static void LoadSaveData()
    {
        //加载存档
        SaveItem saveItem = SaveSystem.GetSaveItem(0);
        HaveArchive = saveItem != null;
    }

    /// <summary>  
    /// 创建新存档  
    /// </summary>
    public static void CreateArchive()
    {
       if(HaveArchive)
       {
           SaveSystem.DeleteSaveItem(0);
       }
       SaveSystem.CreateSaveItem();
    }
}
