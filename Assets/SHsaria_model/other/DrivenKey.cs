using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DrivenKeyframe
{
    public float driverValue;
    public float[] drivenValues = new float[4];
}

public class DrivenKey : MonoBehaviour
{
    public Transform driverObject;
    public PropertyType driverProperty;
    public Transform drivenObject;
    public PropertyType[] drivenProperties = new PropertyType[4];
    public List<DrivenKeyframe> keyframes = new List<DrivenKeyframe>();

    private Vector3 initialDriverPosition;
    private Vector3 initialDrivenPosition;
    private Vector3 initialDriverRotation;
    private Vector3 initialDrivenRotation;

    private float previousDriverAngle = 0f;
    private float accumulatedDriverDelta = 0f;
    private bool initialized = false;

    private float[] accumulatedDrivenRotations = new float[4];

    public enum PropertyType
    {
        PositionX, PositionY, PositionZ,
        RotationX, RotationY, RotationZ
    }

    private void Start()
    {
        if (drivenProperties == null)
            drivenProperties = new PropertyType[0];

        // 修复 keyframe 长度
        foreach (var frame in keyframes)
        {
            if (frame.drivenValues == null || frame.drivenValues.Length < drivenProperties.Length)
            {
                float[] corrected = new float[drivenProperties.Length];
                if (frame.drivenValues != null)
                    frame.drivenValues.CopyTo(corrected, 0);
                frame.drivenValues = corrected;
            }
        }

        if (driverObject != null)
        {
            initialDriverPosition = driverObject.localPosition;
            initialDriverRotation = driverObject.localEulerAngles;
        }
        if (drivenObject != null)
        {
            initialDrivenPosition = drivenObject.localPosition;
            initialDrivenRotation = drivenObject.localEulerAngles;

            for (int i = 0; i < drivenProperties.Length; i++)
            {
                accumulatedDrivenRotations[i] = GetInitialDrivenValue(drivenProperties[i]);
            }
        }
    }

    private void Update()
    {
        if (driverObject == null || drivenObject == null || keyframes.Count < 2)
            return;

        float driverValue;

        if (IsRotation(driverProperty))
        {
            float currentAngle = GetRawAngle(driverObject, driverProperty);

            if (!initialized)
            {
                previousDriverAngle = currentAngle;
                initialized = true;
            }

            float delta = Mathf.DeltaAngle(previousDriverAngle, currentAngle);
            accumulatedDriverDelta += delta;
            previousDriverAngle = currentAngle;

            driverValue = accumulatedDriverDelta;
        }
        else
        {
            driverValue = GetPropertyValue(driverObject, driverProperty) - GetInitialDriverValue(driverProperty);
        }

        float[] drivenValues = Interpolate(driverValue, drivenProperties.Length);

        for (int i = 0; i < drivenProperties.Length; i++)
        {
            accumulatedDrivenRotations[i] = GetInitialDrivenValue(drivenProperties[i]) + drivenValues[i];
            SetPropertyValue(drivenObject, drivenProperties[i], accumulatedDrivenRotations[i]);
        }
    }

    private bool IsRotation(PropertyType property)
    {
        return property == PropertyType.RotationX ||
               property == PropertyType.RotationY ||
               property == PropertyType.RotationZ;
    }

    private float GetRawAngle(Transform obj, PropertyType property)
    {
        switch (property)
        {
            case PropertyType.RotationX: return obj.localEulerAngles.x;
            case PropertyType.RotationY: return obj.localEulerAngles.y;
            case PropertyType.RotationZ: return obj.localEulerAngles.z;
            default:
                Debug.LogWarning("GetRawAngle used on non-rotation property.");
                return 0f;
        }
    }

    private float GetPropertyValue(Transform obj, PropertyType property)
    {
        switch (property)
        {
            case PropertyType.PositionX: return obj.localPosition.x;
            case PropertyType.PositionY: return obj.localPosition.y;
            case PropertyType.PositionZ: return obj.localPosition.z;
            case PropertyType.RotationX: return obj.localEulerAngles.x;
            case PropertyType.RotationY: return obj.localEulerAngles.y;
            case PropertyType.RotationZ: return obj.localEulerAngles.z;
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
            case PropertyType.RotationX: return initialDriverRotation.x;
            case PropertyType.RotationY: return initialDriverRotation.y;
            case PropertyType.RotationZ: return initialDriverRotation.z;
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
            case PropertyType.RotationX: return initialDrivenRotation.x;
            case PropertyType.RotationY: return initialDrivenRotation.y;
            case PropertyType.RotationZ: return initialDrivenRotation.z;
            default: return 0f;
        }
    }

    private void SetPropertyValue(Transform obj, PropertyType property, float value)
    {
        switch (property)
        {
            case PropertyType.PositionX:
            case PropertyType.PositionY:
            case PropertyType.PositionZ:
                Vector3 pos = obj.localPosition;
                if (property == PropertyType.PositionX) pos.x = value;
                if (property == PropertyType.PositionY) pos.y = value;
                if (property == PropertyType.PositionZ) pos.z = value;
                obj.localPosition = pos;
                break;

            case PropertyType.RotationX:
            case PropertyType.RotationY:
            case PropertyType.RotationZ:
                Vector3 rotation = obj.localEulerAngles;
                if (property == PropertyType.RotationX) rotation = new Vector3(value, rotation.y, rotation.z);
                if (property == PropertyType.RotationY) rotation = new Vector3(rotation.x, value, rotation.z);
                if (property == PropertyType.RotationZ) rotation = new Vector3(rotation.x, rotation.y, value);
                obj.localRotation = Quaternion.Euler(rotation);
                break;
        }
    }

    private float[] Interpolate(float driverValue, int count)
    {
        keyframes.Sort((a, b) => a.driverValue.CompareTo(b.driverValue));

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            DrivenKeyframe a = keyframes[i];
            DrivenKeyframe b = keyframes[i + 1];
            if (driverValue >= a.driverValue && driverValue <= b.driverValue)
            {
                float t = (driverValue - a.driverValue) / (b.driverValue - a.driverValue);
                float[] result = new float[count];
                for (int j = 0; j < count; j++)
                {
                    result[j] = Mathf.Lerp(a.drivenValues[j], b.drivenValues[j], t);
                }
                return result;
            }
        }

        float[] fallback = new float[count];
        float[] source = (driverValue < keyframes[0].driverValue) ? keyframes[0].drivenValues : keyframes[keyframes.Count - 1].drivenValues;
        for (int j = 0; j < count; j++)
        {
            fallback[j] = source[j];
        }
        return fallback;
    }
}
