# Placed Light Patcher

When using this patcher, Placed Light should be as **early in your load order as possible**, so it doesn't overwrite important cell changes.

This is a very simple Synthesis patcher that will forward the following records from [Placed Light](https://www.nexusmods.com/skyrimspecialedition/mods/135488): 
* [Cell](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/CELL)
    * Image Space
    * Lighting
    * Lighting Template
    * Sky/Weather from Region
* [Light](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/LIGH) (if the winning record is using vanilla values)
