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

        private static void IterateOverFieldsOfType<T>(Component owner, Action<FieldInfo, T> onMatchingField, bool includeParnetMonoFields = false)
            where T : Component
        {
            Type ownerType = owner.GetType();

            List<FieldInfo> fields = ownerType.GetFields(FIELD_SEARCH_FLAGS).ToList();

            if (includeParnetMonoFields)
            {
                while (ownerType.BaseType != null)
                {
                    ownerType = ownerType.BaseType;
                    fields.AddRange(ownerType.GetFields(FIELD_SEARCH_FLAGS));
                }
            }

            fields.ToArray().ExecuteOnAllFieldsOfType<T>(owner, onMatchingField);
        }

        private static void ExecuteOnAllFieldsOfType<T>(this FieldInfo[] fields, Component owner, Action<FieldInfo, T> onEachField)
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
        
        // //TODO remove this one
        // [Obsolete]
        // public static bool TryExtractTextReferences(this GameObject prefab, Text text, IEnumerable<MonoBehaviour> monoBehaviourToCheck, out List<MonoBehaviour> textReferences)
        // {
        //     textReferences = new List<MonoBehaviour>();
        //     foreach (MonoBehaviour mono in monoBehaviourToCheck)
        //     {
        //         if (mono.IsReferencingComponent<T>(text, out string fieldName))
        //         {
        //             textReferences.Add(mono);
        //         }
        //     }

        //     return textReferences.Count > 0;
        // }

        #endregion // PUBLIC

        #region PRIVATE

        public static bool IsReferencingComponentOfType<T>(this Component thisComponent, Component anotherComponent, out string referencingFieldName)
            where T : Component
        {
            referencingFieldName = null;

            string wat = null;

            IterateOverFieldsOfType<T>(thisComponent, onMatchingField: (fieldInfo, component) => {
                if (IsFieldReferencingComponent(thisComponent, fieldInfo, anotherComponent))
                {
                    wat = fieldInfo.Name;
                }
            }, includeParnetMonoFields: true);

            if (wat != null)
            {
                referencingFieldName = wat;
                return true;
            }
            else
            {
                return false;
            }
            
            // FieldInfo[] fields = thisComponent.GetType().GetFields(FIELD_SEARCH_FLAGS);

            // foreach (FieldInfo field in fields)
            // {
            //     if (IsFieldReferencingComponent(thisComponent, field, anotherComponent))
            //     {
            //         referencingFieldName = field.Name;
            //         return true;
            //     }
            // }

            // return false;
        }

        //TODO Replace with Iterate above
        private static bool IsFieldReferencingComponent<T>(T fieldOwner, FieldInfo field, T component) where T : Component
        {
            _ = component != null ? component : throw new ArgumentNullException(nameof(component));
            _ = fieldOwner != null ? fieldOwner : throw new ArgumentNullException(nameof(fieldOwner));
            _ = field ?? throw new ArgumentNullException(nameof(field));

            if (field.FieldType.IsArray)
            {
                Array arr = field.GetValue(fieldOwner) as Array;

                if (arr == null)
                {
                    Debug.LogError($"array null {field} for {fieldOwner.GetType()}");
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
                List<T> list = field.GetValue(fieldOwner) as List<T>;

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
                    var o = field.GetValue(fieldOwner) as T;
                    if (o == component) return true;
                }
            }

            return false;
        }

        #endregion // PRIVATE 
    }
}
