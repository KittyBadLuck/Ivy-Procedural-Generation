# Ivy Procedural Generation
Tool in Unity, permitting to snap create procedural ivy in edit mode. 

Made in Unity 2021.3.17 (but should work for higher version)

#Included
 -This README with instruction 
-Full unity project (can be opened directly with Unity Hub) 

#How it work: 
-You can find the script under the "Tool" drop down menu. Otherwise, you can press Shift+Alt+V
-A menu will oepn, where you can change the different settings. All of the one for vines will be created automatically, however the leaves need a prefab. 
-You can see a circle under your cursor, if its green, it means that the tool is activated. You just have to click wherever you want.
-Create as much branch as you want. Once satisfied you can click "combine all" to combine all vines to a single object, as well as leaves. 
-You can click "clear all ivy" if you want to destroy everything. 
-If you want to change material to all instance of vines without combining it, add the new material in the menu and click on "apply material to all" (texture is supported)
-Ctrl-Z to cancel any change.

#Possible bug: 
-If you delete a tag, it may induce a bug if it is still registered in the inspector of the script, creating a warning message in console. Recreate et re assign the tag (name Leaf and Branch)
-Currently, the script struggle on sharp edges, im working on fixing it.
-The leaves instance are not ideal, working on how to adjust them.

Lou de Tarade
