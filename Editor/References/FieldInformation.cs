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
        [SerializeField] string textFieldName;
        [SerializeField] Type fieldOwnerType;
        [SerializeField] string fieldOwnerAssemblyName;
        [SerializeField] public List<FieldInformationParamter> fieldInformationParamters = new List<FieldInformationParamter>(5);

        public FieldType FieldType { get => fieldType; set => fieldType = value; }
        public string TextFieldName { get => textFieldName; set => textFieldName = value; }
        public string FieldOwnerAssemblyName => fieldOwnerAssemblyName;

        public Type FieldDefiningType
        {
            get
            {
                if (fieldOwnerType == null)
                {
                    fieldOwnerType = Type.GetType(fieldOwnerAssemblyName);
                }

                return fieldOwnerType;
            }
            set
            {
                if (value == null)
                {
                    Debug.LogError($"What are u even doing, keeping: {fieldOwnerAssemblyName}.");
                }
                else
                {
                    fieldOwnerAssemblyName = value.AssemblyQualifiedName;
                    fieldOwnerType = value;
                }
            }
        }

        public Type FieldOwnerType
        {
            get
            {
                if (FieldType.HasFlag(FieldType.Nested))
                {
                    ExternallyOwnedFieldInformation eofi = GetFieldInformationParamter<ExternallyOwnedFieldInformation>();
                    return Type.GetType(eofi.ExternalOwnerAssemblyName);
                }
                else return FieldDefiningType;
            }
        }

        public T GetFieldInformationParamter<T>() where T : FieldInformationParamter
        {
            foreach (var param in fieldInformationParamters)
            {
                if (param is T)
                {
                    return param as T;
                }
            }

            return null;
        }

        public FieldInformation(Type declaringClassType)
        {
            this.FieldDefiningType = declaringClassType;
        }

        public FieldInformation(string textFieldName, Type declaringClassType)
        {
            this.textFieldName = textFieldName;
            this.fieldOwnerType = declaringClassType;
            //! This requires checking and probably should be handled somewhere else
            this.FieldDefiningType = GetFieldDeclaringType(declaringClassType, textFieldName);
        }

        public void AddFieldInformationParameter(FieldInformationParamter fieldInformationParamter)
        {
            if (fieldInformationParamters == null) fieldInformationParamters = new List<FieldInformationParamter>();
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
                x.FieldOwnerAssemblyName == y.FieldOwnerAssemblyName &&
                x.TextFieldName == y.TextFieldName &&
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
            sb.Append(obj.FieldOwnerAssemblyName);
            sb.Append(obj.TextFieldName);
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


    [Serializable]
    public class FieldInformationParamter
    {
        [Multiline, SerializeField] protected string signature;

        public string Signature => signature;

        public virtual void UpdateSignature() { }
    }

    [Serializable]
    public class EnumerableFieldInformation : FieldInformationParamter
    {
        //* Index in the array or a list 
        public int index;
        public int length;
    }

    [Serializable]
    public class ExternallyOwnedFieldInformation : FieldInformationParamter
    {
        //* Name of a nested class that holds the field with Text component
        public Type ExternalOwnerType;
        public string ExternalOwnerAssemblyName;
        public string ExternalOwnerFieldName;

        public override void UpdateSignature()
        {
            signature += ExternalOwnerType + "\n";
            signature += ExternalOwnerAssemblyName + "\n";
            signature += ExternalOwnerFieldName + "\n";
        }
    }

}