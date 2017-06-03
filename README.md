# Jake's Rust Plugins

1. [Anti Foundation Stack](#anti-foundation-stack)
2. [Blueprints Revived](#blueprints-revived)
3. [Combat Log](#combat-log)
4. [Craft Queue Saver](#craft-queue-saver)
5. [Jake UI Framework](#jake-ui-framework)
6. [No Despawning](#no-despawning)
7. [Recycle Tweaks](#recycle-tweaks)
8. [Visual Debug](#visual-debug)
9. [Weapons On Back](#weapons-on-back)

# Plugins


## Anti Foundation Stack

* [Oxide Listing](http://google.com/?q=link+to+oxide+page)

Luckily, this bug has been fixed in the main game.

A simple bug fix to stop players from stacking square foundations inside eachother. [(To prevent this)](https://gyazo.com/8b5b1c10cb8e65da78e4d1258205d8c8)

## Blueprints Revived (The Blueprint System)

* [Oxide Listing](http://oxidemod.org/plugins/blueprints-revived.2433/)

[Preview](https://gyazo.com/5da30c0e61e4d822e1d9862a446a7d5e)

Bringing back the original blueprint system into current rust. Trying to keep the original feel while bringing in all the new features.

**Many changes and balances were made:**

Blueprints (fragments, pages, books, librarys)  
Revealing a blueprint gives a random item blueprint from the respective tier  
Accurate loot tables to July 2016 branch (with current items added on top)  
Accurate blueprint tiers (balanced with current items)  
Recycle blueprints to down-convert them (pages to fragments for example)  
Research tables work extremely similar to original version (had to use a custom UI to have accuracte research percentage)  
Research tables appear in all monuments (will be loaded in and removed when the plugin is loaded / unloaded) [1](https://gyazo.com/53425691b3cd0902c47876859e9d8093) [2](https://gyazo.com/520caddc390c42333528cd544b7d44c0) [3](https://gyazo.com/520caddc390c42333528cd544b7d44c0)

Arrow raiding (Only on wood, and extremely slow on stone)  
Softsiding doors and ladder hatches  
Old raid towers / building privledge (irrelivant now that you can build twig inside building blocked)  
No heavy armour  
Spears un-nerfed (headshot damage x1.5)  
Crossbows and bows un-nerfed (headshot damage x1.5)  
Radiation disabled (by default)  
Hemp gives 20 cloth and no seeds (as per original system)  
P250 and revolver back to original clip size (revolver 6 bullets, p250 8 bullets)  
Configurable blueprint fragment rate (for modded servers)  
And more I forgot about!  

### Details:

It was incredibly satisfying to see players run around in only the [gear they had researched](https://gyazo.com/08f2e2633cd72e5709809c38bbfe47b5), the lucky naked that found a [C4 inside a crate](https://gyazo.com/08f2e2633cd72e5709809c38bbfe47b5). Servers were [pretty popular](https://gyazo.com/d328f49286f20fcf46fa7d8c4b5d5545) and it was great seeing so many people enjoy my work.

There were many "hacky" solutions that had to be used. This mod was decievingly simple: there was a bunch of stuff going around in the background to make it look as normal and unmodded as possible for the player.

The research table on it's own did absolutely nothing. I had to add a custom monobehavior to each one, that would add a sphere collider. When players entered the sphere collider, it would raycast 4m in front of the player 4x a second to see if they were looking at the research table. If so, it would show the icons and "Open" text just like the original table did, and allow the UI to open when the player pressed "E".

Since blueprint fragments were completely out of the game, I had to upload custom workshop skins and reskin an existing item to look 
like fragments. When the items were dropped, they had to be turned from presents into Blueprint items, and vise-versa when picked up.

When uploading a workshop skin, you disconnect your internet, find the folder in %TEMP%, replace the icon.png with your custom icon, and reconnect your internet to upload your custom workshop item. Forget how I figured that out, but that was the spark that got me to actually take this mod on.

Luckily, presents had an "unwrap" and "combine" option, which was good enough to use as the base item. After a bunch of code to have them behave as seperate items based on the skin ID, the "blueprint fragments" were back in the game.

## Combat Log

A SUPER simple plugin, that grabs the combatlog of a player based on steamID. Eventually the plugin will change the way combatlog works, by filtering hits on players, animals, building blocks, heli, etc. (Would only display player attacks, the rest are irrelivant 99% of the time)

## Craft Queue Saver

* [Oxide Listing](http://oxidemod.org/plugins/craft-queue-saver.2498/)

Players love to afk craft tons of materials, and leave their computer running. The problem is when they disconnect: they drop their entire crafting queue likely losing their items, or when the server restarts their crafting queue is simply deleted. This plugin serializes their crafting queue, restoring it when they log back in. If they are killed while logged out, their crafting queue will be dropped, and when the server wipes it will clear everyone's crafting queue to prevent people keeping items over wipes.

## Easy Chat Commands

* [Oxide Listing](http://oxidemod.org/plugins/easychatcommands.2308/)

Need to provide your players a link to your discord, teamspeak, website, etc, but don't want to bother coding? Just add the command and what text it should display when called to a config file, and you are good to go! (Simple Quality of Life utilities for server owners are surprisingly popular.

## Jake UI Framework

Oxide's Community UI manager was crappy and cumbersome. I made an object orientated version that also made it easy to add callbacks, variable text, etc.

Each element derives from a base class **UIBaseElement**, which handles the resizing and parent/child relations between elements.

Then each CUI element has it's own class with editable properties, and a big constructor if you want to declare the UI in one line.

Buttons are easier to use: just add an action to **UIButton.onClicked** for hooks when a button is clicked.

To show or hide UI to a player, call UIElement.Show(BasePlayer) or UIElement.Hide(BasePlayer) respectively. You can have a panel containing multiple elements inside it, and it will show or hide all child elements as well.

If you want to update UI with a new value, call UIElement.Refresh(BasePlayer) (use null as the parameter to refresh for all players). It will only update for players who currently see the UI, not show it to all players.

Add a delegate to **EITHER:**


**UIElement.conditionalShow:** Return true/false to show or hide an element.

**UIElement.conditionalSize:** Change the size of an element. Useful for a bar display.

**UILabel.variableText:** Change the label's text.

**UIRawImage.variablePNG:** Change the image.



Example \#1:

    //Shows the countdown left when researching, or blank when not researching

    researchCountdownLabel.variableText = delegate (BasePlayer player)
    {
        var data = GetResearchData(player);
        if (!(data.isResearchingItem && data.usingResearchTable))
        {
            return "";
        }
        return data.timeLeft.ToString();

    };

Example \#2:
    
    //Changes the durability icon by changing the height of a green box based on the durability of a weapon
    
    itemDurabilityPanel.conditionalSize = delegate (BasePlayer player)
    {
        var data = GetResearchData(player);
        if (data.targetItem == null)
        {
            return itemDurabilityPanel.size;
        }
        return new Vector2(itemDurabilityPanel.size.x, data.targetItem.conditionNormalized * data.targetItem.maxConditionNormalized * 1f);
    };

## No Despawning

* [Oxide Listing](http://oxidemod.org/plugins/no-despawning.2467/)
* [Screenshots](https://gyazo.com/e3de3eae60f72688d298fe165b6e2774)

Could also be called "PersistantItems". A super cool plugin I wrote over a random weekend: it optimizes dropped items on a server, while also allowing them to stay around way longer. 

When this was first designed, all items had super long despawn times. It was soon discovered that the only way players get rid of trash is by dropping it on the ground and letting it despawn. As it currently stands, items such as burlap clothing only stick around for 15 minutes, while an assault rifle will stay around for 3 days, and an m249 will stay around for an entire week.

### Details:

The optimization was to disable any inactive items, so that we wouldn't hit the collider limit by having a bunch of entities just sitting around on the ground, plus the more items with physics, the more the server is stressed.

I split the world into a grid with [20m X 20m cells](https://gyazo.com/d92de59ad084254af2d86dc634f99b7c), and assigned all items into their own cells. Once all items inside a grid stopped moving, the rigidbodies would all be disabled. Once a new item is dropped in the grid OR an entity with dropped items on it was destroyed, all items in that grid will be reactivated, and would stay active until an minimum timout and they stop moving.

Since items would be sticking around for a lot longer than 5 minutes and people would still try to despawn, I made multiple of the same item stack. This means that when destroying a chest filled up with wood, instead of dropping 30 individual stacks, it drops one stack of 30,000. When picking up the wood, you will only take 1 stack at a time, until it is all gone. 

Despawn time also scales with the over-stack amount: 10k wood despawns in 10x the time.

This had the side-effect of making it very difficult to crash a server by dropping a bunch of pumkins or rocks in one place, though you could still still use the much less effective method of "running around and dropping them one by one in different locations".

[Non-stackable items stack on the ground as well, while preserving attachment, ammo, etc.](https://www.youtube.com/watch?v=uCsUEroNQ3o)

## Recycle Tweaks

Another simple plugin that allows empty cans to be recycled. It was also going to take care of other items in the blueprint system like the old hazmat gear, but that was taken out before I had the chance to add it in.

## Shorter Nights

This one was written by audi (i_love_code), it made night shorter while making it very hard for the player to tell we skipped it. It switches between equal daylight levels (22:00 -> 5:00). Unless the player watches the sky and sees the moon jump, they won't notice the "magic" as it happens. 

## Visual Debug

* [Oxide Listing](http://oxidemod.org/plugins/visual-debug.2468/)
* [Screenshots](https://gyazo.com/0758809fb4e5d55bf32e48194571356a)

Allows you to look around at all the variables, and call functions inside any running plugin. Designed to be similar to Visual Studio's object explorer when debugging.

I am actually super proud of this plugin: it was my first real dive into reflection, and it is really cool all the debugging and fun stuff you can do with it :)

Was super useful to see what funky stuff was going on when our modded server was really laggy. Perhaps there was an edge case causing a dictionary not to be cleared, or you wanted to call a reset function, but couldn't reload the plugin to add a chat command.

## Queue Holder

* [Oxide Listing](http://oxidemod.org/plugins/queue-holder.2411/)

There is nothing worst than your room-mate using the microwave on wipe day, cutting out your internet **just** long enough to be met with an 80 person queue! This plugin alliviates that problem: after disconnecting, you have a 5 minute grace perioid to rejoin safely, skipping the queue.

## Weapons On Back

* [Oxide Listing](http://oxidemod.org/plugins/weapons-on-back.2188/)
* [Screenshots](https://gyazo.com/bdd1de1c0029f87df1c43a54a3bf0ca8)
* [Preview](https://gyazo.com/bdd1de1c0029f87df1c43a54a3bf0ca8)

**The mod that inspired the feature in the main game!**

My **first** popular mod. I was playing around and figured out that setting the parent of an entity would also bind it clientside. I mocked something up, and played around with rotations to get each weapon positioned correctly. I set the parent bone to "spine1", so they would move correctly as the player looked around. I settled on showing only the best weapon not currently equiped, as it was easy to determine best weapon, made sense logically when looking at players and was the hardest to abuse. It looked great once I got all the weapons in the right spot, and ended up on nearly every modded server. And then eventually every server! :)
