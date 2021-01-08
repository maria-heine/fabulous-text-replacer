## fabulous text replacer

## usage

The package is made to be used with Unity's package manager, installation goes by adding the following line into `manifestest.json`:

```
"dependencies": 
{
    "com.mariaheineboombyte.fabulous-text-replacer": "https://github.com/maria-heine/fabulous-text-replacer",
}
```

## todo / functionalities

- [.] Adding a cute animated gif to the fabuloud replacer editor script
- [x] Getting all RectTransforms containing Text component
  - [.] Make sure those are really all Text components that need to be replaced
- [x] Editing an asset and saving it afterwards
- [.] Copying style and look of an old text component into the new one
- [x] Prepare an adapter class between Text and TextMeshPro
    - To avoid changing big chunks of unrelated code. We only want to replace Text fields and provide an adapter so that the rest of the codebase remains oblivious to the fact that their good old friend UnityEngine.UI.Text is being replaced by a terminator-type pixel-perfect hi-tech TMPro.Text component. No time for tears. 
    - [.] A sketch is in place, but the adapter will require overrides for all the used methods and properties
- [.] Changing specific line of code
  - [.] Check if not all other references are lost
- [.] Assigning reference to a newly added texmeshpro component to the edited code that referenced previous Text component
  - [.] Is it even possible to assign inspector references through code?
  - [.] that should be easy, no?
  - [.] check with any new component and a filed added to some test script and AssetDB.SaveAll
- [.] The worst part
  - [.] after rewriting a script like that, won't all the references be lost?
  - for multiple prefabs using the same script, a reference to a text field will be lost

## sequence

0. Finding all prefabs that have text components existing in their heirarchy

Then for each prefab:

- Finding all other prefabs referencing that prefab
- Assume only that prefab parent references its components (no cross prefab children referencing)
  - [.] How to find all the places where children of nested prefabs are referenced by other prefabs in Zula?
1. Finding all prefabs referencing that Text (has to belong to same hierarchy)
2. For each of those component check if they are present in more than one prefab and save all those places
  - This is necessary because ...
  - This will definitely be the case for components being part of nested prefabs
Replacing Text component with TextMeshPro component. 

Cases:

- [.] (easiest) Text is only referenced within a prefab and is not a nested prefab
  - [.] Edit code references within that scope.
  - [.] Substitute Text with TextMeshPro
  - [.] Save reference to the new TextMeshPro
  - [.] Save prefab
- Nested prefab is Text
  - [.] Search for all other places where that nested prefab is used
- Text is a child of a nested prefab

## other things

Links:
- https://docs.unity3d.com/ScriptReference/PrefabUtility.html

Unicode ranges for arabic (obviously I am not sure, correct me if I'm wrong) to be used in TextMeshPro font asset generator tool:

- 600-6FF,750-77F


