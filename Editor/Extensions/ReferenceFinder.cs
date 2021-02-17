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
        public const BindingFlags FIELD_SEARCH_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        #region PRIVATE

        private static void IterateOverFieldsOfType<T>(
            object owner,
            Func<object, FieldInfo, T, bool> onTypeMatchingField,
            Func<object, FieldInfo, bool> onCustomClass,
            bool includeParnetMonoFields = false)
            where T : Component
        {
            Type ownerType = owner.GetType();

            List<FieldInfo> fields = ownerType.GetFields(FIELD_SEARCH_FLAGS | BindingFlags.DeclaredOnly).ToList();

            if (includeParnetMonoFields)
            {
                while (ownerType.BaseType != null)
                {
                    ownerType = ownerType.BaseType;
                    fields.AddRange(ownerType.GetFields(FIELD_SEARCH_FLAGS | BindingFlags.DeclaredOnly));
                }
            }

            if (fields.Count == 0) return;

            Debug.Log($"{fields.Count}");
            

            fields.ToArray().ExecuteOnAllFieldsOfType<T>(owner, onTypeMatchingField, onCustomClass);
        }

        private static void ExecuteOnAllFieldsOfType<T>(
            this FieldInfo[] fields,
            object owner,
            Func<object, FieldInfo, T, bool> onTypeMatchingField,
            Func<object, FieldInfo, bool> onCustomClass)
            where T : Component
        {
            foreach (FieldInfo field in fields)
            {
                Debug.Log($"<color=red>{field.Name}</color>");
                
                if (field.FieldType.IsArray)
                {
                    if (field.FieldType.GetElementType() == typeof(T))
                    {
                        Array arr = field.GetValue(owner) as Array;

                        if (arr == null) continue;

                        foreach (T obj in arr)
                        {
                            if (onTypeMatchingField(owner, field, obj))
                            {
                                return;
                            }
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    if (field.FieldType.GenericTypeArguments.Single() == typeof(T))
                    {
                        List<T> list = field.GetValue(owner) as List<T>;
                        Debug.Log($"<color=yellow>{field.DeclaringType.GetField("someOtherString").GetValue(owner)} got a list {field.Name} of elements {list.Count}</color>");

                        if (list == null) continue;

                        foreach (T obj in list)
                        {
                            Debug.Log($"{obj}");
                            
                            if (onTypeMatchingField(owner, field, obj))
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        IEnumerable enumerable = (IEnumerable)field.GetValue(owner);

                        Debug.Log($"<color=cyan> {owner} \n</color>");

                        foreach (var item in enumerable)
                        {
                            Debug.Log($"{item}");
                            
                            if (onCustomClass(item, field))
                            {
                                return;
                            }
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
                            if (onTypeMatchingField(owner, field, obj))
                            {
                                return;
                            }
                        }
                    }
                    else if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string) && field.FieldType != typeof(object))
                    {
                        //* Here we are checking if the field is a custom class
                        // Debug.Log($"<color=cyan>Custom class encountered: {field.FieldType}</color>");

                        var newOwner = field.GetValue(owner);

                        if (onCustomClass(newOwner, field))
                        {
                            return;
                        }
                    }
                }
            }
        }
        #endregion // PRIVATE

        #region PUBLIC

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

        public static bool IsReferencingComponentOfType<T>(this object someObject, T component, ref FieldInformation referencedFieldInformation)
            where T : Component
        {
            FieldInformation fieldInformation = referencedFieldInformation;

            IterateOverFieldsOfType<T>(
                owner: someObject,
                onTypeMatchingField: (fieldOwner, fieldInfo, fieldValue) =>
                {
                    if (fieldValue == component)
                    {
                        Type fieldOwnerType = fieldOwner.GetType();

                        fieldInformation = new FieldInformation(fieldOwnerType);

                        fieldInformation.TextFieldName = fieldInfo.Name;
                        fieldInformation.FieldType = FieldType.Direct;

                        if (fieldInfo.FieldType.IsGenericType)
                        {
                            if (fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                            {
                                //* This means text field is hidden within a list
                                fieldInformation.FieldType = fieldInformation.FieldType | FieldType.Listed;
                                Debug.Log($"<color=magenta>A listed field {fieldInformation.FieldType} {fieldInfo.Name} {fieldOwner.GetType()}</color>");
                            }
                        }
                        else if (fieldInfo.FieldType.IsArray)
                        {
                            //* This means text field is hidden within an array
                            fieldInformation.FieldType = fieldInformation.FieldType | FieldType.Arrayed;
                            Debug.Log($"<color=BF55CB>ARRAY! {fieldInformation.FieldType} {fieldInfo.Name} {fieldOwner.GetType()}</color>");
                        }

                        return true;
                    }
                    else return false;
                },
                onCustomClass: (fieldOwner, fieldInfo) =>
                {
                    //! follow the white rabbit
                    if (fieldOwner.IsReferencingComponentOfType(component, ref fieldInformation))
                    {
                        ExternallyOwnedFieldInformation externallyOwnedFieldInformation = new ExternallyOwnedFieldInformation();
                        externallyOwnedFieldInformation.ExternalOwnerFieldName = fieldInfo.Name;

                        //! Note that this might be a problem in case of inherited fields, check it
                        externallyOwnedFieldInformation.ExternalOwnerType = someObject.GetType();
                        externallyOwnedFieldInformation.ExternalOwnerAssemblyName = someObject.GetType().AssemblyQualifiedName;
                        externallyOwnedFieldInformation.UpdateSignature();
                        fieldInformation.AddFieldInformationParameter(externallyOwnedFieldInformation);

                        FieldType fieldType;

                        fieldInformation.FieldType &= ~FieldType.Direct; 

                        if (fieldOwner.GetType().IsNested)
                        {
                            //* This means a nested class
                            fieldType = FieldType.Nested;
                        }
                        else
                        {
                            //* This means an external class is a field that holds reference to the text component
                            fieldType = FieldType.External;
                        }

                        // if (fieldInfo.FieldType.IsGenericType)
                        // {
                        //     if (fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                        //     {
                        //         //* This means text field is hidden within a list
                        //         fieldType |= FieldType.Listed;
                        //     }
                        // }
                        // else if (fieldInfo.FieldType.IsArray)
                        // {
                        //     //* This means text field is hidden within an array
                        //     fieldType |= FieldType.Arrayed;
                        // }

                        fieldInformation.FieldType = fieldType;

                        return true;
                    }
                    else return false;
                },
                includeParnetMonoFields: true);

            if (fieldInformation != null)
            {
                referencedFieldInformation = fieldInformation;
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion // PUBLIC
    }
}
