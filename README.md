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

References search:
- Basic asset reference search: done
- Search for unreferenced assets: done but might be inaccurate
- Search for variable or function usage: not yet
- Search inside a graph: not yet

Not sure if i will do that:
- Blueprints compilation
- Runtime blueprint debugger

### How to use

To use the tool you need to create a dump from the game process using [jmap_dumper](https://github.com/trumank/jmap). jmap_dumper 0.1.1 is missing interface dumping so you need to build the dumper yourself until the next release with this feature.

1. Install rust language for your system

2. Make a dump using [jmap_dumper](https://github.com/trumank/jmap)
   
   Clone jmap repository
   ```
   git clone https://github.com/trumank/jmap.git
   ```
   
   Navigate to jmap_dumper folder
   ```
   cd jmap/jmap_dumper/
   ```

   Make a dump file replacing 12345 with your game process id
   ```
   cargo run --release -- --pid 12345 output.jmap
   ```

3. Launch the program and create new profile for your game. Provide a .jmap file you just generated.

### Loops and macros

Several engine's control flow nodes (including loops) are actually just BP macros, and they are getting inlined during compilation. The tool tries to find all possible macro instances in a graph and replace them with macro calls. The tool is shipped with some of the engine built-in macros. Creating your own macros is possible but very limited for now.

### Compare two game builds

You can compare blueprints of two different versions of the game. To open a comparing mode settings, press "Compare" button at the top left. Diff view might be inaccurate.

### References search

Searching for reference is currently limited and possibly inaccurate. More search features will be added later.
