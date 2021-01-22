using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace FabulousReplacer
{
    public static class ReferenceFinder
    {
        const BindingFlags fieldSearchFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        #region PUBLIC

        public static bool TryGetAllFieldsOfType<T>(this MonoBehaviour mono, out List<FieldInfo> foundTFields)
            where T : Component
        {
            FieldInfo[] fields = mono.GetType().GetFields(fieldSearchFlags);
            foundTFields = new List<FieldInfo>(fields.Length);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType.IsArray)
                {
                    Array arr = field.GetValue(mono) as Array;

                    if (arr == null) continue;

                    foreach (object obj in arr)
                    {
                        if (obj.GetType() == typeof(T))
                        {
                            Debug.Log($"Found an array for {mono}, field {field.Name}");
                            foundTFields.Add(field);
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<T>))
                {
                    Debug.Log($"Found a list for {mono}, field {field.Name}");

                    List<T> list = field.GetValue(mono) as List<T>;

                    if (list == null) continue;

                    foreach (T obj in list)
                    {
                        if (obj.GetType() == typeof(T))
                        {
                            Debug.Log($"Found a list for {mono}, field {field.Name}");
                            foundTFields.Add(field);
                        }
                    }
                }
                else if (field.FieldType.IsClass)
                {
                    if (field.FieldType == typeof(T))
                    {
                        if (field. GetValue(mono) != null)
                            foundTFields.Add(field);
                    }
                }
            }

            return foundTFields.Count > 0;
        }

        public static bool TryExtractTextReferences(this GameObject prefab, Text text, IEnumerable<MonoBehaviour> monoBehaviourToCheck, out List<MonoBehaviour> textReferences)
        {
            textReferences = new List<MonoBehaviour>();

            foreach (MonoBehaviour mono in monoBehaviourToCheck)
            {
                if (mono.IsReferencingComponent(text))
                {
                    textReferences.Add(mono);
                }
            }

            return textReferences.Count > 0;
        }

        #endregion // PUBLIC

        #region PRIVATE

        private static bool IsReferencingComponent(this Component thisComponent, Component anotherComponent)
        {
            FieldInfo[] fields = thisComponent.GetType().GetFields(fieldSearchFlags);

            foreach (FieldInfo field in fields)
            {
                if (IsFieldReferencingComponent(thisComponent, field, anotherComponent))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFieldReferencingComponent<T>(T instance, FieldInfo field, T component) where T : Component
        {
            _ = component != null ? component : throw new ArgumentNullException(nameof(component));
            _ = instance != null ? instance : throw new ArgumentNullException(nameof(instance));
            _ = field ?? throw new ArgumentNullException(nameof(field));

            if (field.FieldType.IsArray)
            {
                Array arr = field.GetValue(instance) as Array;

                if (arr == null) 
                {
                    Debug.LogError($"array null {field} for {instance.GetType()}");
                    return false;
                }

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
                List<T> list = field.GetValue(instance) as List<T>;

                if (list == null) return false;

                foreach (T obj in list)
                {
                    if (obj != null && component != null && obj.GetType() == component.GetType())
                    {
                        if (obj == component) return true;
                    }
                }
            }
            else if (field.FieldType.IsClass)
            {
                if (field.FieldType == component.GetType())
                {
                    var o = field.GetValue(instance) as T;
                    if (o == component) return true;
                }
            }

            return false;
        }

        #endregion // PRIVATE 
    }
}
