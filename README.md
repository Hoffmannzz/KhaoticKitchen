# Kitchen Chaos
[![GitHub license](https://img.shields.io/github/license/HyagoOliveira/KitchenChaos?style=flat-square)](https://github.com/HyagoOliveira/KitchenChaos/blob/main/LICENSE)

![Kitchen Chaos Thumbnail](/Wiki/Thumbnail.png "Kitchen Chaos")

Click [here](https://youtu.be/qiwCZmpDRUY) to watch a quick gameplay session.

---

## Summary

This game is a study case for the game Overcooked! 2.

All art assets like Textures, models, audio clips etc where created by the indie game maker **Code Monkey** for this [Free Complete Unity Course](https://youtu.be/AmGSEH7QcDg). 

However, the game structure and the source-code were created by me and they are very different from the course.

This project uses many [Unity Packages](http://34.125.146.81:4873/) created and published by me. All of them are on the MIT license so you can freely use them into your project.

## How To Play

You must prepare, cook and serve up some tasty orders before time ends!

After the initial count down, Orders will be received on the screen's top left corner.
You must collect the right ingredients, prepare and plate them before delivery.

Your score will increase according in how quickly you deliver those orders.

![Kitchen Chaos Screenshot](/Wiki/Screenshot.png "Kitchen Chaos Screenshot")

Play alone and switch between chefs to improve the kitchen dynamics, or cook together with friends in **Local Co-op** or **Online** (up to 4 players, one chef each). See [Multiplayer](#multiplayer).

**Play the in-game Tutorial for further instructions**.

Click [here](https://nostgames.itch.io/kitchen-chaos) to play the game on **itch.io**!

## Controls

- **Tab** - Switch between Chefs.
- **AWSD** or **Arrow Keys** - Movement.
- **Q** - Interact with Items (Cutting Table, Stove Table) 
- **E** - Interact with Plate and Ingredients (Tomato, Cheese, Bread etc)

> Gamepad is also supported.

## Multiplayer

The main menu has two extra buttons: **CO-OP** and **ONLINE**. Each player controls their own chef (Blue, Green, Red and Yellow). The Red and Yellow chefs join the kitchen when there are 3 or 4 players. The tutorial stays single player.

### Local Co-op

Up to 4 players on the same computer. In the CO-OP menu, each player presses a button on their own device to join, then anyone presses **Enter** or **Start**.

| Device | Join | Move | Pick up / drop | Chop / cook |
|---|---|---|---|---|
| Keyboard (left) | E | WASD | E | Q |
| Keyboard (right) | . or Right Ctrl | Arrow keys | . or Right Ctrl | / or Right Shift |
| Gamepad | A (Cross) | Left stick / D-Pad | A (Cross) | X (Square) |

**P** or **Start** pauses the match. **Q**, **/** or **B** (Circle) leaves the join screen before the match starts.

### Online

One player hosts and the others join. The host's game runs the kitchen and everyone else follows it, so all players need the same build of the game.

1. Everyone opens **ONLINE** and types a name.
2. The host picks one of these:
    - **HOST WITH JOIN CODE**: shows a short code. Friends anywhere type it and press **JOIN WITH CODE**. This uses Unity Relay, so nobody needs to change router settings. It needs a one-time project setup (see below).
    - **HOST WITH MY IP**: friends type the host's IP address and press **JOIN BY IP**. This works on the same network (the lobby lists the host's local IPs). Over the internet, the host must forward UDP port 7777 on their router, or everyone joins the same virtual network with a tool like [Tailscale](https://tailscale.com) or [ZeroTier](https://www.zerotier.com) and uses its IP.
3. When everyone is in the lobby, the host presses **START MATCH**.

During an online match, **P** or **Start** opens a menu to leave (online matches cannot be paused). When the host leaves, the match ends for everyone. After the results screen, everyone returns to the lobby and the host can start another match.

Online play is not available in the WebGL (browser) build. Use a desktop build.

#### Setting up join codes (Unity Relay)

Join codes need the project to be linked to a Unity Cloud project with Relay turned on. This is a one-time setup for whoever builds the game. Players do not need a Unity account.

1. In Unity, sign in with your Unity account, then open **Edit > Project Settings > Services** and link the project to a new or existing cloud project.
2. On the [Unity Cloud dashboard](https://cloud.unity.com), open that project and turn on **Relay** (under Multiplayer). Relay has a free tier; check Unity's current pricing for the limits.
3. Build the game again.

Without this setup, **HOST WITH MY IP** still works.

#### Testing online on one computer

Build a desktop player (**File > Build Settings > Build**), run it, and press Play in the editor at the same time. Host from one of them with **HOST WITH MY IP**, then join from the other with the IP `127.0.0.1`.

#### How it works

Online play uses [Netcode for GameObjects](https://docs-multiplayer.unity3d.com/netcode/current/about/) only for connections and messages ([`NetcodeTransport`](/Assets/Scripts/Networking/Netcode/NetcodeTransport.cs)). The game state is kept in sync by [`NetworkMatch`](/Assets/Scripts/Networking/NetworkMatch.cs):

- Every machine runs the same game code. The host decides everything that changes the kitchen (interactions, cooking and burning, new and failed orders, returned plates, timers) and sends each change to the clients, which replay it in the same order.
- Counters, holders and items get the same id on every machine: scene objects are numbered by their place in the hierarchy, and spawned items use the id chosen by the host.
- Each machine moves its own chef and sends its position. The host also sends the positions of chefs and of items lying on the floor about 20 times per second.

To keep a new gameplay change in sync, wrap it with `NetworkGame.Replicate(...)` on the host and handle its `NetEventType` in `NetworkMatch`.

## How To add new Recipes

1. Inside the [Recipe folder](/Assets/Settings/Recipes), create a new Recipe Data asset by using the Create menu, Kitchen Chaos > Recipes > Recipe;
2. Open the [IngredientsToRecipe prefab](/Assets/Prefabs/Recipes/IngredientsToRecipe.prefab) and link the new Recipe asset into the Recipe field;

    ![IngredientsToRecipe](/Wiki/IngredientsToRecipe.png "Ingredients To Recipe")
3. Place each child ingredient in the right position;
4. Right click on the `Prefab To Recipe` script and choose **Transfer Ingredients to Recipe**;

    ![TransferIngredientsToRecipe](/Wiki/TransferIngredientsToRecipe.png "Transfer Ingredients To Recipe")
5. Finally add this new Recipe asset int to RecipeSettings Scriptable Object.

    ![RecipeSettings](/Wiki/RecipeSettings.png "Recipe Settings")

## CI/CD

Continuous Integration and Continuous Delivery are done using [GitHub Actions for Unity](https://github.com/game-ci/unity-actions), provided by the [GameCI](https://game.ci/).

You can play the last WebGL build on [github-pages](https://hyagooliveira.github.io/KitchenChaos/).
