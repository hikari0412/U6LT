using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;//字典需要的

/// <summary>
/// 可序列化的字典
/// </summary>
[Serializable]

public class Serialization_Dic<K,V>
{
    private List<K> keyList;
    private List<V> valueList;

    //不用序列化，存和读取时不用，只在运行时用
    // 原因是C#和Unity都无法直接序列化Dictionary（泛型字典），所以要用别的结构间接保存它。
    [NonSerialized]
    private Dictionary<K, V> dictionary;
    public Dictionary<K, V> Dictionary { get => dictionary; }
    public Serialization_Dic(Dictionary<K, V> dictionary)
    {
        this.dictionary = dictionary;
    }

    //默认构造函数，创建一个空的字典
    //这个构造函数在Unity编辑器中序列化时会被调用，确保能创建一个空的字典。
    public Serialization_Dic()
    {
        this.dictionary = new Dictionary<K, V>();
    }

    //这个是C#自带的“序列化前执行钩子”，只在序列化（存储/保存）对象前自动调用。
    //代码是在序列化之前，把当前字典内容全部转存进keyList和valueList。
    //这样List就能被序列化，Dictionary即使不被序列化也不会丢数据。
    [OnSerializing] 
    private void OnSerializing(StreamingContext context)
    {
        //避免前一次的数据残留，所以new一个新的List
        //保证序列化时的数据100%同步最新字典内容
        keyList = new List<K>(dictionary.Count);
        valueList = new List<V>(dictionary.Count);
        foreach (var kv in dictionary)
        {
            keyList.Add(kv.Key);
            valueList.Add(kv.Value);
        }
    }

    // 反序列化读取List后组装字典
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        dictionary = new Dictionary<K, V>(keyList.Count);
        for (int i = 0; i < keyList.Count; i++)
        {
            dictionary.Add(keyList[i], valueList[i]);
        }
        keyList.Clear();
        valueList.Clear();
    }

}
