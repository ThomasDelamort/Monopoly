# Low-Poly Monopoly — Data Structures & Algorithms in C#

A 3D low-poly Monopoly game built with **Godot 4.7 (.NET / C#)**. The rules engine is a plain C#
library that runs on hand-written data structures and algorithms, and the game shows them live in an
in-game **DSA panel**.

- **Menu:** Play → choose **1, 2, 3 or 4** human players. There are always 4 tokens, so empty seats
  are filled with bots (1 human = 3 bots, 4 humans = no bots). Then pick names and characters on the
  pedestal screen and start.
- **Rules:** buying, rent (doubled on full color sets), houses and hotels with even building,
  mortgages, jail (doubles, fine, Get-Out-of-Jail-Free), Chance and Community Chest, auctions when a
  player declines to buy, raising funds, and bankruptcy. Last player standing wins, or set a round
  limit in Settings.

## Running

```
tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path game
```

Build the C# first if you changed code: `dotnet build game/Monopoly.csproj`.
Open `game/project.godot` in the same Godot editor to edit scenes or export an .exe.

**Controls:** Space rolls the dice or ends the turn · M opens Manage properties · Tab toggles the DSA panel ·
Q/E rotate the camera · mouse wheel zooms · V switches between overview and follow camera ·
click a tile to see its deed · Esc backs out of a menu or pauses the game.

## Where each data structure and algorithm is used

All structures are hand-written in `src/Monopoly.Core` (no `System.Collections.Generic` containers in the game logic).

| DSA | Code | In the game |
|---|---|---|
| Circular doubly linked list | `DataStructures/CircularDoublyLinkedList.cs` | The 40-tile board. Tokens follow `Next`, "Go back 3" follows `Prev`, and wrapping onto the head means passing GO. The 3D board is built by walking the list. |
| Queue (circular array) | `DataStructures/ArrayQueue.cs` | Turn order, Chance and Community Chest decks (draw from the top, return to the bottom), auction bidders (a bid goes to the back, a pass leaves), the debts queue, the game event queue (engine → animator), and the bounded event log. |
| Stack | `DataStructures/ArrayStack.cs` | Menu navigation (Esc pops a screen) and per-turn **Undo** of build, sell and mortgage actions (command pattern). |
| Hash map (separate chaining) | `DataStructures/HashMap.cs` | Tile lookup by name; color group → its properties. |
| Binary heap (priority queue) | `DataStructures/BinaryHeap.cs` | Bot AI: a max-heap picks the best house to build, and a min-heap picks the least painful asset to sell or mortgage. |
| Dynamic array | `DataStructures/DynamicArray.cs` | Player portfolios, and the buffers the sorts work on. |
| Merge sort (stable) | `Algorithms/Sorting.cs` | Live standings and final ranking by net worth. |
| Quick sort, heap sort, insertion sort | `Algorithms/Sorting.cs` | The Manage screen lets you pick the algorithm and shows comparisons and writes. Insertion sort orders the opening roll-off. |
| Binary search (lower bound) | `Algorithms/Searching.cs` | The Manage screen's name search (prefix search over the name-sorted deeds). |
| Linear search around the ring | `CircularDoublyLinkedList.FindNext` | "Advance to the nearest Railroad / Utility" cards. |
| Fisher–Yates shuffle | `Algorithms/Randomness.cs` | Shuffling both card decks (xorshift RNG, seedable). |
| Markov chain / DP | `Algorithms/LandingProbability.cs` | Long-run landing odds per tile, which bots use to value properties. |

The DSA panel (bottom-right, Tab) shows: the linked-list nodes around the current token (prev ⇄ current ⇄ next),
every queue, both stacks, the hash maps, and a live log of each data-structure operation with its step count.

## Project layout

```
src/Monopoly.Core/         rules engine + data structures + algorithms + bot AI (pure C#, no Godot)
tests/Monopoly.Core.Tests/ self-contained test runner: dotnet run --project tests/Monopoly.Core.Tests
game/                      Godot project: screens, HUD, 3D board, tokens, dice, camera
blender/                   Blender MCP generator scripts for the characters and props (+ .blend sources)
tools/godot/               Godot 4.7.2 .NET editor
```

## Tests

`dotnet run --project tests/Monopoly.Core.Tests` runs 58 tests: every data structure and algorithm (edge
cases included), the rules (rent, building, mortgage, jail, cards, auctions, debts, bankruptcy), and a
500-game bot simulation that checks the game's invariants after every single action (property ownership,
even building, house and hotel supply, no negative cash, turn-queue integrity).

## 3D assets (Blender MCP)

`blender/characters.py` builds the four characters (Tycoon, Skater, Student, Showman) and renders their
portrait sprites. `blender/props.py` builds the trees, buildings, vault, dice, pedestal and tile icons.
Run them inside Blender through the Blender MCP; they write `.glb` files into `game/assets/` and never
modify the open Blender scene (each asset gets its own `MP_*` scene). Until those files exist, the game
uses equivalent procedural low-poly stand-ins.

## Debug switches

Pass these after `--`: `--start title|count|seat|settings|game`, `--humans N`, `--autoplay`, `--seed N`,
`--rounds N`, `--speed 0-2`, `--shots DIR --shot-every SEC --quit-after SEC` (automated screenshots),
`--give 6,8,9` (deeds for player 1), `--script roll,decline,bid,manage,build,end` (auto-press the human's buttons).
