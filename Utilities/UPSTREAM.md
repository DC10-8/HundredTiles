# Attribution and local changes

ExpandedElectricityManager.cs and ExpandedWaterManager.cs are derived from
[81 Tiles 2](https://github.com/algernon-A/EightyOne2), commit
04fa96241fab752b5d90a47a311246613e1af66d, copyright algernon
(K. Algernon A. Sheppard), licensed under MIT. See UPSTREAM-LICENSE.txt.

Local changes use namespace HundredTiles.Utilities, retain origin 231 while
expanding the grid to 508 cells, update NetManager/BuildingManager upper bounds
and row widths to 299/300, and include single-row scheduling ranges.
These components are not yet wired into the active game or save lifecycle.
