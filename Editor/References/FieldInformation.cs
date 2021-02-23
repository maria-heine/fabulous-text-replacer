using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static FabulousReplacer.FabulousExtensions;
using System.Linq;

namespace FabulousReplacer
{
    [Flags]
    public enum FieldType
    {
        None = 0,
        Direct = 1, // A field is explicitly of UnityEngine.UI.Text type
        Listed = 2,
        Arrayed = 4,
        Nested = 8,
        External = 16 // Field containing a text component is a separate class used as a field inside another class
    }

    /*
    Note the system does not handle classes nested inside nested classes, but with all the love, go to hell if you write such code.
    */
    [Serializable]
    public class FieldInformation
    {
        [SerializeField] FieldType fieldType;
        [SerializeField] string fieldName;
        [SerializeField] string fieldDefiningTypeAssemblyName;
        [SerializeField] public List<FieldInformationParamter> fieldInformationParamters = new List<FieldInformationParamter>(5);
        // [SerializeField] FieldInformation fieldParent; // * This is how it should be done but there is no time for that

        public FieldType FieldType { get => fieldType; set => fieldType = value; }
        public string FieldName { get => fieldName; set => fieldName = value; }
        public string FieldDefiningTypeAssemblyName => fieldDefiningTypeAssemblyName;
        // public FieldInformation FieldParent => fieldParent;

        public Type FieldDefiningType
        {
            get
            {
                return Type.GetType(fieldDefiningTypeAssemblyName);
            }
            set
            {
                if (value == null)
                {
                    Debug.LogError($"What are u even doing, keeping: {fieldDefiningTypeAssemblyName}");
                }
                else
                {
                    fieldDefiningTypeAssemblyName = value.AssemblyQualifiedName;
                }
            }
        }

        //* In case of nested or external classes I assume the "owner" of the text field to be different than the class
        //* that defined that field
        public Type FieldOwnerType
        {
            get
            {
                if (fieldType.HasOneOfTheFlags(FieldType.Nested | FieldType.External))
                {
                    ExternallyOwnedFieldInformation eofi = GetFieldInformationParamter<ExternallyOwnedFieldInformation>();
                    return Type.GetType(eofi.ExternalOwnerAssemblyName);
                }
                else
                {
                    return FieldDefiningType;
                }
            }
        }

        // public T GetFieldInformationParamter<T>() where T : FieldInformationParamter
        public T GetFieldInformationParamter<T>()
        {
            foreach (var param in fieldInformationParamters)
            {
                if (param is T paramT)
                {
                    return paramT;
                }
                else
                {
                    Debug.Log($"{param.GetType()}");
                    Debug.Log($"{param.Signature}");
                }
            }

            Debug.LogError($"Failed to find FieldInformationParamter of type {typeof(T)}");

            return default(T);
        }

        public FieldInformation(Type fieldDefiningType)
        {
            this.FieldDefiningType = fieldDefiningType;
        }

        public FieldInformation(string textFieldName, Type declaringClassType)
        {
            this.fieldName = textFieldName;
            this.FieldDefiningType = declaringClassType;

            //! This requires checking and probably should be handled somewhere else
            this.FieldDefiningType = GetFieldDeclaringType(declaringClassType, textFieldName);
        }

        ~FieldInformation()
        {
            if (fieldInformationParamters == null) return;

            for (int i = fieldInformationParamters.Count - 1; i >= 0 ; i--)
            {
                UnityEngine.Object.DestroyImmediate(fieldInformationParamters[i]);
            }
        }

        public void AddFieldInformationParameter(FieldInformationParamter fieldInformationParamter)
        {
            if (fieldInformationParamters == null)
            {
                fieldInformationParamters = new List<FieldInformationParamter>();
            }

            fieldInformationParamter.UpdateSignature();
            fieldInformationParamters.Add(fieldInformationParamter);
        }
    }

    public class FieldInformationEqualityComparer : IEqualityComparer<FieldInformation>
    {
        public bool Equals(FieldInformation x, FieldInformation y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            else if (
                x.FieldType == y.FieldType &&
                x.FieldDefiningTypeAssemblyName == y.FieldDefiningTypeAssemblyName &&
                x.FieldName == y.FieldName &&
                x.fieldInformationParamters.Count == y.fieldInformationParamters.Count &&
                x.fieldInformationParamters.SequenceEqual(y.fieldInformationParamters, new FieldInformationParamterEqualityComparer())
            )
            {
                Debug.Log($"<color=cyan>YASSS! EQUAL FIELD INFORMATIONS</color>");

                return true;
            }
            else
                return false;
        }

        public int GetHashCode(FieldInformation obj)
        {
            if (obj == null)
            {
                return 0;
            }

            var sb = new StringBuilder();
            sb.Append(obj.FieldType.ToString());
            sb.Append(obj.FieldDefiningTypeAssemblyName);
            sb.Append(obj.FieldName);
            if (obj.fieldInformationParamters != null)
            {
                foreach (var fieldInformationParamter in obj.fieldInformationParamters)
                {
                    sb.Append(fieldInformationParamter.Signature);
                }
            }
            string result = sb.ToString();

            return result.GetHashCode();
        }
    }

    public class FieldInformationParamterEqualityComparer : IEqualityComparer<FieldInformationParamter>
    {
        public bool Equals(FieldInformationParamter x, FieldInformationParamter y)
        {
            if (x == null && y == null)
                return true;
            else if (x == null || y == null)
                return false;
            else if (x.GetType() == y.GetType() && x.Signature == y.Signature)
                return true;
            else
                return false;
        }

        public int GetHashCode(FieldInformationParamter obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return obj.Signature.GetHashCode();
        }
    }


    public class FieldInformationParamter : ScriptableObject 
    {
        [Multiline, SerializeField] protected string signature;

        public string Signature => signature;

        public virtual void UpdateSignature() { }
    }

    public class EnumerableFieldInformation : FieldInformationParamter
    {
        //* Index in the array or a list 
        public int index;
        public int length;

        public override void UpdateSignature()
        {
            signature += "index" + index + "\n";
            signature += "length" + length + "\n";
        }
    }

    public class ExternallyOwnedFieldInformation : FieldInformationParamter
    {
        //* Name of a nested class that holds the field with Text component
        public Type ExternalOwnerType;
        public string ExternalOwnerAssemblyName;
        public string ExternalOwnerFieldName;
        public FieldInformation fieldInformation;

        public override void UpdateSignature()
        {
            signature += ExternalOwnerType + "\n";
            signature += ExternalOwnerAssemblyName + "\n";
            signature += ExternalOwnerFieldName + "\n";
        }
    }

}