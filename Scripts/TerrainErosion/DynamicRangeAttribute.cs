using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class DynamicRangeAttribute : PropertyAttribute
{
    public string EnumFieldName { get; }
    public Dictionary<string, int> ValueMap { get; } = new();

    public DynamicRangeAttribute(string enumFieldName, params object[] pairs)
    {
        EnumFieldName = enumFieldName;

        if (pairs.Length % 2 != 0)
        {
            Debug.LogError("DynamicRange: pairs must be in (string enumName, int max) format!");
            return;
        }

        for (int i = 0; i < pairs.Length; i += 2)
        {
            string key = pairs[i].ToString();
            int value = Convert.ToInt32(pairs[i + 1]);
            ValueMap[key] = value;
        }
    }
}

[CustomPropertyDrawer(typeof(DynamicRangeAttribute))]
public class EnumDynamicRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (DynamicRangeAttribute)attribute;

        SerializedProperty enumProp = FindSiblingProperty(property, attr.EnumFieldName);

        if (enumProp == null || enumProp.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.LabelField(position, label.text, "Invalid enum field reference");
            return;
        }

        string enumName = enumProp.enumNames[enumProp.enumValueIndex];

        int maxValue;

        if (!attr.ValueMap.TryGetValue(enumName, out maxValue))
        {
            using var e = attr.ValueMap.Values.GetEnumerator();
            if (e.MoveNext())
                maxValue = e.Current;
            else
            {
                EditorGUI.LabelField(position, label.text, "DynamicRange: no value map");
                return;
            }
        }

        property.intValue = Mathf.Clamp(property.intValue, 1, maxValue);
        EditorGUI.IntSlider(position, property, 1, maxValue, label);
    }

    private SerializedProperty FindSiblingProperty(SerializedProperty property, string name)
    {
        string path = property.propertyPath;
        int dot = path.LastIndexOf('.');
        string parent = dot >= 0 ? path.Substring(0, dot) : "";

        string fullPath = string.IsNullOrEmpty(parent) ? name : parent + "." + name;

        return property.serializedObject.FindProperty(fullPath);
    }
}
