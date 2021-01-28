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
        // TODO This really requires checking whether we are getting everything we want.
        public const BindingFlags FIELD_SEARCH_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        #region PRIVATE

        private static void IterateOverFields<T>(MonoBehaviour owner, Action<FieldInfo, T> onMatchingField)
        {
            FieldInfo[] fields = owner.GetType().GetFields(FIELD_SEARCH_FLAGS);

            fields.ExecuteOnAllFieldsOfType<T>(owner, onMatchingField);
        }

        private static void ExecuteOnAllFieldsOfType<T>(this FieldInfo[] fields, MonoBehaviour owner, Action<FieldInfo, T> onEachField)
        {
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType.IsArray)
                {
                    Array arr = field.GetValue(owner) as Array;

                    if (arr == null) continue;

                    foreach (object obj in arr)
                    {
                        if (obj is T objT)
                        {
                            onEachField.Invoke(field, objT);
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<T>))
                {
                    // Debug.Log($"Found a list for {mono}, field {field.Name}");

                    List<T> list = field.GetValue(owner) as List<T>;

                    if (list == null) continue;

                    foreach (T obj in list)
                    {
                        if (obj.GetType() == typeof(T))
                        {
                            onEachField.Invoke(field, obj);
                        }
                    }
                }
                else if (field.FieldType.IsClass)
                {
                    if (field.FieldType == typeof(T))
                    {
                        T obj = (T)field.GetValue(owner);

                        if (obj != null)
                        {
                            onEachField.Invoke(field, obj);
                        }
                    }
                }
            }
        }
        #endregion

        #region PUBLIC

        public static string GetFieldNameForAComponent<T>(this T component, MonoBehaviour owner)
            where T : Component
        {
            FieldInfo[] fields = owner.GetType().GetFields(FIELD_SEARCH_FLAGS);

            string fieldName = null;

            fields.ExecuteOnAllFieldsOfType<T>(owner, (field, value) =>
            {
                if (value == component)
                {
                    fieldName = field.Name;
                }
            });

            if (fieldName is null)
            {
                Debug.LogError($"Failed to find a field name for component {component} at {owner}");
            }

            return fieldName;
        }


        public static bool TryGetAllFieldsOfType<T>(this MonoBehaviour mono, out List<FieldInfo> foundTFields)
            where T : Component
        {
            FieldInfo[] fields = mono.GetType().GetFields(FIELD_SEARCH_FLAGS);
            foundTFields = new List<FieldInfo>(fields.Length);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType.IsArray)
                {
                    Array arr = field.GetValue(mono) as Array;

                    if (arr == null) continue;

                    foreach (object obj in arr)
                    {
                        Debug.Log($"Found an array for {mono}, field {field.Name}");
                        if (obj.GetType() == typeof(T))
                        {
                            foundTFields.Add(field);
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<T>))
                {
                    // Debug.Log($"Found a list for {mono}, field {field.Name}");

                    List<T> list = field.GetValue(mono) as List<T>;

                    if (list == null) continue;

                    foreach (T obj in list)
                    {
                        if (obj.GetType() == typeof(T))
                        {
                            // Debug.Log($"Found a list for {mono}, field {field.Name}");
                            foundTFields.Add(field);
                        }
                    }
                }
                else if (field.FieldType.IsClass)
                {
                    if (field.FieldType == typeof(T))
                    {
                        if (field.GetValue(mono) != null)
                            foundTFields.Add(field);
                    }
                }
            }

            return foundTFields.Count > 0;
        }

        //TODO remove this one
        public static bool TryExtractTextReferences(this GameObject prefab, Text text, IEnumerable<MonoBehaviour> monoBehaviourToCheck, out List<MonoBehaviour> textReferences)
        {
            textReferences = new List<MonoBehaviour>();

            foreach (MonoBehaviour mono in monoBehaviourToCheck)
            {
                if (mono.IsReferencingComponent(text, out string fieldName))
                {
                    textReferences.Add(mono);
                }
            }

            return textReferences.Count > 0;
        }

        #endregion // PUBLIC

        #region PRIVATE

        public static bool IsReferencingComponent(this Component thisComponent, Component anotherComponent, out string referencingFieldName)
        {
            referencingFieldName = null;
            
            FieldInfo[] fields = thisComponent.GetType().GetFields(FIELD_SEARCH_FLAGS);

            foreach (FieldInfo field in fields)
            {
                if (IsFieldReferencingComponent(thisComponent, field, anotherComponent))
                {
                    referencingFieldName = field.Name;
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
