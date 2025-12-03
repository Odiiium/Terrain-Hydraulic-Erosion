using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;

[CustomEditor(typeof(MonoBehaviour), true)]
public class ButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var buttonAttr = method.GetCustomAttribute<ButtonAttribute>();
            if (buttonAttr == null) continue;

            string label = string.IsNullOrEmpty(buttonAttr.Label)
                ? method.Name
                : buttonAttr.Label;

            if (GUILayout.Button(label))
            {
                method.Invoke(target, null);
                EditorUtility.SetDirty(target);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : Attribute
    {
        public string Label;

        public ButtonAttribute(string label = null)
        {
            Label = label;
        }
    }
}