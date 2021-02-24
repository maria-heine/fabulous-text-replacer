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
        public const BindingFlags GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        #region PRIVATE

        private static void IterateOverFieldsOfType<T>(
            object owner,
            Action<object, FieldInfo, T> onTypeMatchingField,
            Action<object, FieldInfo> onCustomClass,
            bool includeParnetMonoFields = false)
            where T : Component
        {
            Type ownerType = owner.GetType();

            List<FieldInfo> fields = ownerType.GetFields(GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS | BindingFlags.DeclaredOnly).ToList();

            if (includeParnetMonoFields)
            {
                while (ownerType.BaseType != null)
                {
                    ownerType = ownerType.BaseType;
                    fields.AddRange(ownerType.GetFields(GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS | BindingFlags.DeclaredOnly));
                }
            }

            if (fields.Count == 0) return;

            fields.ToArray().ExecuteOnAllFieldsOfType<T>(owner, onTypeMatchingField, onCustomClass);
        }

        private static void ExecuteOnAllFieldsOfType<T>(
            this FieldInfo[] fields,
            object owner,
            Action<object, FieldInfo, T> onTypeMatchingField,
            Action<object, FieldInfo> onCustomClass)
            where T : Component
        {
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType.IsArray)
                {
                    if (field.FieldType.GetElementType() == typeof(T))
                    {
                        Array arr = field.GetValue(owner) as Array;

                        if (arr == null) continue;

                        foreach (T obj in arr)
                        {
                            onTypeMatchingField(owner, field, obj);
                        }
                    }
                    else
                    {
                        IEnumerable enumerable = (IEnumerable)field.GetValue(owner);

                        foreach (var item in enumerable)
                        {
                            onCustomClass(item, field);
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    if (field.FieldType.GenericTypeArguments.Single() == typeof(T))
                    {
                        List<T> list = field.GetValue(owner) as List<T>;

                        if (list == null) continue;

                        foreach (T obj in list)
                        {
                            onTypeMatchingField(owner, field, obj);
                        }
                    }
                    else
                    {
                        IList enumerable = (IList)field.GetValue(owner);

                        foreach (var item in enumerable)
                        {
                            onCustomClass(item, field);
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
                            onTypeMatchingField(owner, field, obj);
                        }
                    }
                    else if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string) && field.FieldType != typeof(object))
                    {
                        //* Here we are checking if the field is a custom class

                        var newOwner = field.GetValue(owner);

                        onCustomClass(newOwner, field);
                    }
                }
            }
        }
        #endregion // PRIVATE

        #region PUBLIC

        public static bool TryGetAllFieldsOfType<T>(this MonoBehaviour mono, out List<FieldInfo> foundTFields)
            where T : Component
        {
            FieldInfo[] fields = mono.GetType().GetFields(GENEROUS_NONSTATIC_FIELD_SEARCH_FLAGS);
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
                    List<T> list = field.GetValue(mono) as List<T>;

                    if (list == null) continue;

                    foreach (T obj in list)
                    {
                        if (obj.GetType() == typeof(T))
                        {
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

        // ::::::::::::::help me:::::
        // ____.∧__∧:::::::::::::::::
        // ___(<'º yº) =3 ::::::🖥️:::
        // ___/　  ⌒ヽ⊃🍔 :::|===|::
        // _🍷(人＿＿つ_つ.:::::|===|::
        // ___FAT IS GOOD____________
        public static bool IsReferencingComponentOfType<T>(this object someObject, T component, ref List<FieldInformation> referencingFields)
            where T : Component
        {
            List<FieldInformation> methodLocalReferencingFields = null;
            FieldInformation fieldInformation;

            IterateOverFieldsOfType<T>(
                owner: someObject,
                onTypeMatchingField: (fieldOwner, fieldInfo, fieldValue) =>
                {
                    if (fieldValue == component)
                    {
                        Type fieldOwnerType = fieldOwner.GetType();

                        fieldInformation = new FieldInformation(fieldInfo.Name, fieldOwnerType);

                        fieldInformation.FieldType = FieldType.Direct;

                        if (fieldInfo.FieldType.IsGenericType)
                        {
                            if (fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                            {
                                //* This means text field is hidden within a list
                                fieldInformation.FieldType |= FieldType.Listed;
                                var list = (List<T>)fieldInfo.GetValue(fieldOwner);
                                EnumerableFieldInformation efi = ScriptableObject.CreateInstance<EnumerableFieldInformation>();
                                efi.index = list.IndexOf(fieldValue);
                                efi.length = list.Count;
                                fieldInformation.AddFieldInformationParameter(efi);
                            }
                        }
                        else if (fieldInfo.FieldType.IsArray)
                        {
                            //* This means text field is hidden within an array
                            fieldInformation.FieldType |= FieldType.Arrayed;
                            var array = (T[])fieldInfo.GetValue(fieldOwner);
                            EnumerableFieldInformation efi = ScriptableObject.CreateInstance<EnumerableFieldInformation>();
                            efi.index = Array.IndexOf(array, fieldValue);
                            efi.length = array.Length;
                            fieldInformation.AddFieldInformationParameter(efi);
                        }

                        if (methodLocalReferencingFields == null)
                        {
                            methodLocalReferencingFields = new List<FieldInformation>();
                        }

                        methodLocalReferencingFields.Add(fieldInformation);
                    }
                },
                onCustomClass: (fieldOwner, fieldInfo) =>
                {
                    //! follow the white rabbit
                    List<FieldInformation> externalFieldsInformation = null;

                    if (fieldOwner.IsReferencingComponentOfType(component, ref externalFieldsInformation))
                    {
                        if (externalFieldsInformation == null) Debug.LogError("oof");

                        foreach (FieldInformation externalField in externalFieldsInformation)
                        {
                            ExternallyOwnedFieldInformation eofi = ScriptableObject.CreateInstance<ExternallyOwnedFieldInformation>();
                            
                            eofi.fieldInformation = new FieldInformation(fieldInfo.Name, someObject.GetType());

                            if (fieldInfo.FieldType.IsGenericType && fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                            {
                                eofi.fieldInformation.FieldType |= FieldType.Listed;
                                var list = (IList)fieldInfo.GetValue(someObject);
                                EnumerableFieldInformation efi = ScriptableObject.CreateInstance<EnumerableFieldInformation>();
                                efi.index = list.IndexOf(fieldOwner);
                                efi.length = list.Count;
                                eofi.fieldInformation.AddFieldInformationParameter(efi);
                            }
                            else if (fieldInfo.FieldType.IsArray)
                            {
                                eofi.fieldInformation.FieldType |= FieldType.Arrayed;
                                var array = (Array)fieldInfo.GetValue(someObject);
                                EnumerableFieldInformation efi = ScriptableObject.CreateInstance<EnumerableFieldInformation>();
                                efi.index = Array.IndexOf(array, fieldOwner);
                                efi.length = array.Length;
                                eofi.fieldInformation.AddFieldInformationParameter(efi);
                            }
                            else
                            {
                                eofi.fieldInformation.FieldType = FieldType.Direct;
                            }


                            eofi.ExternalOwnerFieldName = fieldInfo.Name;
                            //! Note that this might be a problem in case of inherited fields, check it
                            eofi.ExternalOwnerType = someObject.GetType();
                            eofi.ExternalOwnerAssemblyName = someObject.GetType().AssemblyQualifiedName;
                            externalField.AddFieldInformationParameter(eofi);

                            FieldType fieldType = externalField.FieldType;
                            fieldType &= ~FieldType.Direct;

                            if (fieldOwner.GetType().IsNested)
                            {
                                //* This means a nested class
                                fieldType |= FieldType.Nested;
                            }
                            else
                            {
                                //* This means an external class is a field that holds reference to the text component
                                fieldType |= FieldType.External;
                            }

                            externalField.FieldType = fieldType;
                        }

                        if (methodLocalReferencingFields == null)
                        {
                            methodLocalReferencingFields = new List<FieldInformation>();
                        }

                        methodLocalReferencingFields.AddRange(externalFieldsInformation);
                    }
                },
                includeParnetMonoFields: true);

            if (methodLocalReferencingFields != null)
            {
                referencingFields = methodLocalReferencingFields;
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
