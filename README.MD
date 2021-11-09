HgPatch is an all-in-one patch format for games made in GameMaker.  
It currently supports importing and replacing code, sprites, masks, audio, fonts, backgrounds, textures, rooms, and objects.  
However, it is rather temperamental in its current state, so it's very important that you understand how to use it.  
  
#Legal Notes (In Plain English):  
	First things first: I am not a lawyer, and I am most certainly not *your* lawyer. This is not legal counsel - if you have issues related to the topics presented here, please contact a licensed attorney. My advice may help you stay out of trouble, but it is not a golden ticket to avoiding lawsuits, and it will not cover every conceivable legal topic you may need to address.  
	  
	##UNDERSTANDING COPYRIGHT:  
		Copyright protections exist to help artists and other workers of various types (authors, musicians, screenwriters, painters, programmers, etc.) make money off of their work. The Copyright owner gets to decide who does and doesn't get to distribute their work, and doing so without their permission is likely to land you in legal trouble. When a work is copyrighted, distributing that work in whole or in part may constitute copyright infringement. Most importantly, YOU MUST NOT EVER INCLUDE COPYRIGHTED WORK IN ANYTHING YOU DISTRIBUTE, as doing so is a form of Copyright infringement! However, it is equally important as a creator to understand the nature of fair use. If you are making your own transformative (i.e. mostly made by you) work, using only the reasonable minimum amount of copyrighted content possible, are not distributing it for direct profit, and your work cannot be construed as directly competing with the original work, it may constitute a case of fair use. If your use of copyrighted materials constitutes fair use, it is not necessary to contact or obtain approval from the rights holder, although you may wish to do this anyways. For example, distributing a mod as a patch CONTAINING ONLY YOUR ORIGINAL WORK is likely to be permissible. However, if you charge money for your mod, allow users to play your mod without the base game, provide tools for circumventing Digital Rights Management measures of a game, or distribute large quantities of ripped code and assets with your mod, a court might be more likely to conclude your actions constitute an act of copyright infringement.  
	  
	##UNDERSTANDING TRADEMARKS:  
		Trademarks are moreso a tool for the consumer than a tool of the rightsholder. The purpose of trademarks is to help consumers discern the nature and origin of products and other content they see - Trademarked materials can include logos, slogans, brand names, and other branding that can be used to identify products. Avoid copying these details, especially if doing so might mislead a consumer into believing you have a connection with or are acting on the behalf of the trademark owner when this isn't actually the case. It is acceptable to include trademarked materials referentially (E.g. including the name "Nike" in a review of Nike's athletic shoes), or if you include a trademarked name coincidentally (E.g. the name "Mario" referring not to the Nintendo character, but to another person who happens to share the same name). This won't usually be an issue for you, but it's something to be aware of.  
  
#Pros and Cons:  
	##Pros:  
		+Ease of application  
		+User Friendly  
		+Streamlines the patch creation process  
		+No code needs to be written to patch your content into a game  
		+Distribute patches with lower risk of copyright infringement  
	##Cons:  
		-May not make efficient use of computing resources  
		-Patching process cannot be reversed easily  
		-Does not support special sprite types (I.E. Splines and SWFs)  
		-Edge cases may result in errors  
		-Applying multiple mods is likely to cause errors  
  
###WARNING:  
###NEVER DIRECTLY CREATE A PATCH FROM YOUR WORKING FOLDER! ALWAYS MAKE BACKUPS!  
It is recommended that you copy your mod folder and make a patch from the copy, rather than the original, as there may be defects in patch creation that could alter data in your work.  
  
##File Structure  
	The patch is a simple folder in windows, containing several subfolders for different types of content and a patch.cfg  
file. If you aren't using one of your content folders (e.g. your mod has no sprites), DO NOT delete your unused files.  
This may cause issues.  
  
###Special Files  
	patch.cfg  
		This is your patch configuration file, which determines what methods are used when importing your assets.  
		This must be at the root of the patch folder.  
	<assetType>Info.txt  
		Optional files in some asset folders used to alter parameters for assets.  
		Exists primarily for sprites, paths, fonts, sounds, backgrounds, and more.  
  
###Audio Notes:  
	Currently audio patch creation for games with audio groups in separate files requires that the data.win be in the same folder as the audiogroup.dat files, both for patch creation and patch application. This means that for patch creation, it is best to have your mod and the original, vanilla game exist in separate directories, with all other files duplicated, so that the patch creator can correctly compare audio. This is also advised because future patcher versions are planned to find differences in other files automatically.  
	  
	Currently, there is no supported way to add new audio groups.  
  
##Filenames  
	File names are very important, and are how the script knows where to put all the stuff you import without a separate  
file telling it. This can be very powerful, but it's also important to know how you need to format your file names.  
File naming rules will follow some basic conventions, and will be similar if not identical to those generated by  
exporting files in UndertaleModTool (which HgPatch is based upon).  
	####Sprite Filenames:  
		Sprite filenames follow the format <spritename>_<framenumber>.png  
		For example, spr_kris_d_0.png would be a valid sprite. Note that the frame number begins at 0, and not at 1.  
	####Mask Filenames:  
		Masks should follow the same format as sprites  
	####Other Filenames:  
		Other file names are simply the name of the asset, plus the extension.  
		  
##Edge Cases and Known Failings  
	Currently, HgPatcher does not track asset or content deletions in many cases. Thus, the patch creator and patch applier have no way to remove assets from a game, only ways to add and alter them. This is planned as a future feature.  
	  
	Right now you may experience issues with code accuracy. Currently, code is decompiled and exported as GML. However, decompiling this GML during patch creation and recompiling it later at patch application may produce inaccuracies. It is planned to include an assembly fallback option if if it is detected that the GML decompilation/recompilation will result in something other than the original code entry.  
	  
	Asset order is not tracked currently, so changing asset order (as well as the aforementioned case of asset removals) may cause errors due to GameMaker's indexed assets.  
	  
	There is not yet a way to change files with the patch besides the data.win and any associated audio group files.  
	  
	Rooms and GameObjects are copied in their entirety, regardless of how much data has actually changed.  
	