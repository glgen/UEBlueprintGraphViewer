# UE Blueprint Graph Viewer

A tool for viewing compiled blueprint code of Unreal Engine 4-5 games. 4.25 and later versions are supported.

![screenshot.png](screenshot.png)

### Features roadmap
Blueprint decompilation:
- Basic decompilation: done
- Loops: done
- Macros: mostly done (some built-in macros are missing)
- Timelines: done
- Input events: partially done
- Delegates: done
- Functions with multiple exec pins: not yet
- Anim BPs: not yet

Blueprint debugger

References search:
- Basic asset reference search: done
- Search for unreferenced assets: done but might be inaccurate
- Search for variable or function usage: not yet
- Search inside a graph: not yet

Not sure if i will do that:
- Blueprints compilation

### How to use

To use the tool you need to create a dump from the game process using [jmap_dumper](https://github.com/trumank/jmap).

1. Download and extract [jmap_dumper](https://github.com/trumank/jmap) v0.2.0 or above.

2. Make a .jmap dump file replacing 12345 with your game process id
   ```
   jmap_dumper --pid 12345 output.jmap
   ```

3. Launch the program and create new profile for your game. Provide a .jmap file you just generated.

### Loops and macros

Several engine's control flow nodes (including loops) are actually just BP macros, and they are getting inlined during compilation. The tool tries to find all possible macro instances in a graph and replace them with macro calls. The tool is shipped with some of the engine built-in macros. Creating your own macros is possible but very limited for now.

### Blueprint debugger
To debug a blueprint in runtime:
1. Install latest experimental dev version of [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) and a BlueprintDebbuger mod to your game.
2. Replace content of file config/uebpv.txt with a path to your UE Blueprint Graph Viewer installation.
   - If you are on linux and running a game through Proton, you need to specify path to windows version of the tool.
3. Close UE Blueprint Graph Viewer if it is opened.
4. Launch the game. Start the debugger by pressing a button in Blueprint Debugger tab on UE4SS's debug window.

To set a breakpoint, press "Toggle breakpoint" in node context menu. Use 2 buttons above graph view to navigate when breakpoint is hit. Mouse over a pin to find out its current value and a local variable name. Change a current variable value by double clicking its name in Debugger details tab.

### Compare two game builds

You can compare blueprints of two different versions of the game. To open a comparing mode settings, press "Compare" button at the top left. Diff view might be inaccurate.

### References search

Searching for reference is currently limited and possibly inaccurate. More search features will be added later.
