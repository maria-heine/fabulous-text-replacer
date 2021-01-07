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

## other things

Unicode ranges for arabic (obviously I am not sure, correct me if I'm wrong) to be used in TextMeshPro font asset generator tool:

- 600-6FF,750-77F
