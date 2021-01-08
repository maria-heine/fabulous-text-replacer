using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ReferenceFinder
{
    const BindingFlags fieldSearchFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    public static void SearchForObjectsReferencingComponent<T>(Component[] searchPool, T component)  where T : Component
    {
        foreach(Component c in searchPool)
        {
            FieldInfo[] fields = c.GetType().GetFields(fieldSearchFlags);

            foreach (FieldInfo field in fields)
            {
                if (FieldReferencesComponent(c, field, component))
                {
                    
                }
            }
        }
    }

    private static bool FieldReferencesComponent<T>(Component c, FieldInfo field, T component) where T : Component
    {
        if (field.FieldType.IsArray)
        {
            var arr = field.GetValue(c) as Array;

            CheckEnumerableField(arr);

            foreach (object obj in arr)
            {
                if (obj != null && component != null && obj.GetType() == component.GetType())
                {
                    var o = obj as T;
                    if (o == component) return true;
                }
            }
        }
        else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<T>))
        {
            // todo Really doubting if this would work, not sure either if we have some lists of Text component
            var arr = field.GetValue(c) as List<T>;

            CheckEnumerableField(arr);
        }
        else if (field.FieldType.IsClass)
        {
            if (field.FieldType == component.GetType())
            {
                var o = field.GetValue(c) as T;
                if (o == component) return true;
            }
        }

        return false;
    }

    private static bool CheckEnumerableField(IEnumerable enumerable)
    {
        return false;
    }
}
