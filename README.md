## fabulous text replacer

- [ ] Adding a cute animated gif to the fabuloud replacer editor script
- [x] Getting all RectTransforms containing Text component
- [x] Editing an asset and saving it afterwards
- [ ] Copying style and look of an old text component into the new one
- [ ] Changing specific line of code
    - [ ] Check if not all other references are lost
- [ ] Assigning reference to a newly added texmeshpro component to the edited code that referenced previous Text component
    - [ ] Is it even possible to assign inspector references through code?
    - [ ] that should be easy, no?
    - [ ] check with any new component and a filed added to some test script and AssetDB.SaveAll
- [ ] The worst part
    - [ ] after rewriting a script like that, won't all the references be lost?
    - for multiple prefabs using the same script, a reference to a text field will be lost
