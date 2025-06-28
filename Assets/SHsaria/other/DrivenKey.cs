using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public class DrivenKeyframe
{
    public float driverValue;
    // 被驱动属性对应的数值，现支持最多4个属性
    public float[] drivenValues = new float[4];
}

public class DrivenKey : MonoBehaviour
{
    public Transform driverObject; // 驱动对象
    public PropertyType driverProperty; // 驱动属性
    public Transform drivenObject; // 被驱动对象
    // 被驱动属性（最多4个）
    public PropertyType[] drivenProperties = new PropertyType[4];
    public List<DrivenKeyframe> keyframes = new List<DrivenKeyframe>(); // 关键帧

    private Vector3 initialDriverPosition;
    private Vector3 initialDrivenPosition;
    private Vector3 initialDriverRotation;
    private Vector3 initialDrivenRotation;

    public enum PropertyType
    {
        PositionX, PositionY, PositionZ,
        RotationX, RotationY, RotationZ
    }

    private void Start()
    {
        if (driverObject != null)
        {
            initialDriverPosition = driverObject.localPosition;
            initialDriverRotation = driverObject.localEulerAngles;
        }
        if (drivenObject != null)
        {
            initialDrivenPosition = drivenObject.localPosition;
            initialDrivenRotation = drivenObject.localEulerAngles;
        }
    }

    private void Update()
    {
        if (driverObject == null || drivenObject == null || keyframes.Count < 2)
            return;

        // 计算驱动对象的增量值（相对于初始状态）
        float driverValue = GetPropertyValue(driverObject, driverProperty) - GetInitialDriverValue(driverProperty);
        float[] drivenValues = Interpolate(driverValue);

        for (int i = 0; i < drivenProperties.Length; i++)
        {
            // 应用驱动的增量值到被驱动对象的初始状态上
            SetPropertyValue(drivenObject, drivenProperties[i], GetInitialDrivenValue(drivenProperties[i]) + drivenValues[i]);
        }
    }

    private float GetPropertyValue(Transform obj, PropertyType property)
    {
        switch (property)
        {
            case PropertyType.PositionX: return obj.localPosition.x;
            case PropertyType.PositionY: return obj.localPosition.y;
            case PropertyType.PositionZ: return obj.localPosition.z;
            case PropertyType.RotationX: return NormalizeAngle(obj.localEulerAngles.x);
            case PropertyType.RotationY: return NormalizeAngle(obj.localEulerAngles.y);
            case PropertyType.RotationZ: return NormalizeAngle(obj.localEulerAngles.z);
            default: return 0f;
        }
    }

    private float GetInitialDriverValue(PropertyType property)
    {
        switch (property)
        {
            case PropertyType.PositionX: return initialDriverPosition.x;
            case PropertyType.PositionY: return initialDriverPosition.y;
            case PropertyType.PositionZ: return initialDriverPosition.z;
            case PropertyType.RotationX: return NormalizeAngle(initialDriverRotation.x);
            case PropertyType.RotationY: return NormalizeAngle(initialDriverRotation.y);
            case PropertyType.RotationZ: return NormalizeAngle(initialDriverRotation.z);
            default: return 0f;
        }
    }

    private float GetInitialDrivenValue(PropertyType property)
    {
        switch (property)
        {
            case PropertyType.PositionX: return initialDrivenPosition.x;
            case PropertyType.PositionY: return initialDrivenPosition.y;
            case PropertyType.PositionZ: return initialDrivenPosition.z;
            case PropertyType.RotationX: return NormalizeAngle(initialDrivenRotation.x);
            case PropertyType.RotationY: return NormalizeAngle(initialDrivenRotation.y);
            case PropertyType.RotationZ: return NormalizeAngle(initialDrivenRotation.z);
            default: return 0f;
        }
    }

    private void SetPropertyValue(Transform obj, PropertyType property, float value)
    {
        Vector3 temp;
        switch (property)
        {
            case PropertyType.PositionX: temp = obj.localPosition; temp.x = value; obj.localPosition = temp; break;
            case PropertyType.PositionY: temp = obj.localPosition; temp.y = value; obj.localPosition = temp; break;
            case PropertyType.PositionZ: temp = obj.localPosition; temp.z = value; obj.localPosition = temp; break;
            case PropertyType.RotationX: temp = obj.localEulerAngles; temp.x = value; obj.localEulerAngles = temp; break;
            case PropertyType.RotationY: temp = obj.localEulerAngles; temp.y = value; obj.localEulerAngles = temp; break;
            case PropertyType.RotationZ: temp = obj.localEulerAngles; temp.z = value; obj.localEulerAngles = temp; break;
        }
    }

    private float[] Interpolate(float driverValue)
    {
        keyframes.Sort((a, b) => a.driverValue.CompareTo(b.driverValue));

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            DrivenKeyframe a = keyframes[i];
            DrivenKeyframe b = keyframes[i + 1];
            if (driverValue >= a.driverValue && driverValue <= b.driverValue)
            {
                float t = (driverValue - a.driverValue) / (b.driverValue - a.driverValue);
                return new float[]
                {
                    Mathf.Lerp(a.drivenValues[0], b.drivenValues[0], t),
                    Mathf.Lerp(a.drivenValues[1], b.drivenValues[1], t),
                    Mathf.Lerp(a.drivenValues[2], b.drivenValues[2], t),
                    Mathf.Lerp(a.drivenValues[3], b.drivenValues[3], t)
                };
            }
        }

        // driverValue 比所有关键帧都小
        if (driverValue < keyframes[0].driverValue)
            return keyframes[0].drivenValues;

        // driverValue 比所有关键帧都大
        return keyframes[keyframes.Count - 1].drivenValues;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
