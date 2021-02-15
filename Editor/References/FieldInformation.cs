using System;
using System.Collections.Generic;
using UnityEngine;
using static FabulousReplacer.FabulousExtensions;

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
        [SerializeField] public List<FieldInformationParamter> fieldInformationParamters; 
        
        public FieldType FieldType { get => fieldType; set => fieldType = value; }
        public string TextFieldName { get => textFieldName; set => textFieldName = value; }
        public string DeclaringClassAssemblyName => fieldOwnerAssemblyName;

        public Type FieldOwnerType
        {
            get
            {
                if (fieldOwnerType == null)
                {
                    Debug.LogError($"{fieldOwnerAssemblyName} Type is already null");
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

        public FieldInformation(Type declaringClassType)
        {
            this.FieldOwnerType = declaringClassType;
        }

        public FieldInformation(string textFieldName, Type declaringClassType)
        {
            this.textFieldName = textFieldName;
            this.fieldOwnerType = declaringClassType;
            //! This requires checking and probably should be handled somewhere else
            this.FieldOwnerType = GetFieldDeclaringType(declaringClassType, textFieldName);
        }

        public void AddFieldInformationParameter(FieldInformationParamter fieldInformationParamter)
        {
            if (fieldInformationParamters == null) fieldInformationParamters = new List<FieldInformationParamter>();
            fieldInformationParamters.Add(fieldInformationParamter);
        }
    }

    
    [Serializable]
    public class FieldInformationParamter 
    { 
        [Multiline, SerializeField] protected string preview;

        public virtual void UpdatePreview() { }
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

        public override void UpdatePreview()
        {
            preview += ExternalOwnerType + "\n";
            preview += ExternalOwnerAssemblyName + "\n";
            preview += ExternalOwnerFieldName + "\n";
        }
    }

}