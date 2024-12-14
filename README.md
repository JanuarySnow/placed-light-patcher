# Placed Light Patcher

When using this patcher, Placed Light should be as **high in your load order as possible**, so it doesn't overwrite important cell changes.

This is a very simple Synthesis patcher that will forward the following records from Placed Light: 
* [Cell](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/CELL)
    * Lighting
    * Lighting Template
    * Sky/Weather from Region
* [Light](https://en.uesp.net/wiki/Skyrim_Mod:Mod_File_Format/LIGH) (if the winning record is using vanilla values)
