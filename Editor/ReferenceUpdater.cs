using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FabulousReplacer
{
    public class ReferenceUpdater
    {
        // Button _updateReferencesButton;
        UpdatedReferenceAddressBook _updatedReferenceAddressBook;

        public ReferenceUpdater(UpdatedReferenceAddressBook referenceAddressBook, Button updateReferencesButton)
        {
            _updatedReferenceAddressBook = referenceAddressBook;

            Debug.Log(_updatedReferenceAddressBook);
            // Debug.Log(_updatedReferenceAddressBook.prefabsUpdatedReferences);
            

            updateReferencesButton.clicked += () =>
            {
                RunUpdateReferencesLogic();
            };
        }

        // TODO yay babyyyy continue hereee
        private void RunUpdateReferencesLogic()
        {
            foreach (var kvp in _updatedReferenceAddressBook)
            {
                Debug.Log(kvp.Key);

                foreach (UpdatedReference reference in kvp.Value)
                {
                    Debug.Log(reference.originalPrefab);
                    Debug.Log(reference.originalText);
                }
            }
        }
    }
}
