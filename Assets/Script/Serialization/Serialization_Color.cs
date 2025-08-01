using UnityEngine;
using System;

/// <summary>
/// 可序列化的颜色（颜色本来是结构体无法序列化）
/// </summary>
[Serializable]

public struct Serialization_Color
{
    public float r, g, b, a;

    // 构造函数
    public Serialization_Color(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    public override string ToString()
    {
        return $"({r},{g},{b},{a})";
    }
  

    /// <summary>
    /// 重写这个类的GetHashCode()方法，
    /// 并且是通过把自己转换成Unity的Color类型后，调用Color自带的哈希算法来生成自己的哈希值
    /// </summary>
    public override int GetHashCode()
    {
        return this.ConvertToUnityColor().GetHashCode();
    }
}


/// <summary>
/// 通过Color.的方式直接进行两种类型的转换，故使用拓展方法
/// </summary>
public static class Serialization_ColorExtensions
{
    public static Color ConvertToUnityColor(this Serialization_Color color)
    {
        return new Color(color.r, color.g, color.b, color.a);
    }

    public static Serialization_Color ConverTToSerializatioNColor(this Color color)
    {
        return new Serialization_Color(color.r, color.g, color.b, color.a);
    }

    // 其他使用Color的地方不涉及类型转换固不用更改，出现需要用或修改CustomCharacterData存储的数据时要做转换。
    // 当然也可以做运算符重载添加隐式转换的逻辑，这样无论是UnityColor还是序列化的Color都不用显示转换了，但为了逻辑清晰不推荐。
    // public static implicit operator Serialization_Color (Color color)
    // {
    //     return new Serialization_Color(color.r, color.g, color.b, color.a);
    // }
}
