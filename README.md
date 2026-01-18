# Grey Hack Terminal UI

A BepInEx mod for Grey Hack that adds a programmable pixel canvas controlled from GreyScript via simple `#UI{ ... }` blocks embedded in `print` statements.

The canvas is rendered in a separate Unity window and driven by a lightweight scripting VM with persistent state per terminal.

---

## Features

- Pixel canvas windows automatically associated with Grey Hack terminals
- Drawing primitives: pixels, lines, rectangles, circles, and text
- Small scripting language with:
  - Variables and arithmetic
  - Conditionals (`if / else if / else`)
  - `while` loops
  - Built‑in math and utility functions
- Persistent VM context per terminal (state survives across `print` calls)
- One VM per terminal; old executions are stopped before new ones start
- Security & abuse protection:
    - Limits on variables, string length, loop iterations, execution time
    - Cooldown on resizing the canvas

While the initial implementation focuses on enabling rich UI via the canvas, the same scripting VM and intrinsic system can be extended to drive other client‑side features (for example, playing sounds, triggering visual effects, or exposing additional in‑game controls).

[![Watch the video](https://img.youtube.com/vi/sEcpcEcIYgs/hqdefault.jpg)](https://www.youtube.com/watch?v=sEcpcEcIYgs)

---

## Requirements

- Grey Hack (Steam)
- BepInEx 5 (recommended) or BepInEx 6 for Grey Hack
- .NET 6 SDK (only if you want to build from source)

---

## Installation

1. Install BepInEx 5 for Grey Hack (follow the BepInEx docs).
2. Download the compiled DLL for this mod (GreyHackTerminalUI5).
3. Copy the DLL into your BepInEx plugins folder, for example:  
   `[Grey Hack folder]/BepInEx/plugins/GreyHackTerminalUI5.dll`
4. Start Grey Hack.  
   The mod will log its startup in the BepInEx console.

For BepInEx 6, use the corresponding BepInEx 6 build of the plugin and place it in the appropriate plugins folder.

---

## Basic Usage

From GreyScript you print a `#UI{ ... }` block; the contents are intercepted by the mod instead of going to the terminal. Each terminal has its own VM and window.

Minimal example:

```greyscript
print("#UI{
    Canvas.setSize(200, 150)
    Canvas.setTitle(""Hello Canvas"")
    Canvas.show()
    Canvas.clear(""black"")
    Canvas.fillRect(""red"", 10, 10, 50, 50)
    Canvas.render()
}")
```

The language inside `#UI{ ... }` supports variables, arithmetic, `if / else`, `while`, basic math functions, and `hasInContext(name)` for checking persisted variables.

---

## Canvas API

### Window & Canvas Control

- `Canvas.show()`  
- `Canvas.hide()`  
- `Canvas.setSize(w, h)`  
- `Canvas.setTitle(title)`  
- `Canvas.clear()`  
- `Canvas.clear(color)`  

Example:

```greyscript
print("#UI{
    Canvas.setSize(320, 240)
    Canvas.setTitle(""My Game"")
    Canvas.show()
    Canvas.clear(""blue"")
    Canvas.render()
}")
```

### Drawing Primitives

- `Canvas.setPixel(color, x, y)`
- `Canvas.drawLine(color, x1, y1, x2, y2)`
- `Canvas.drawRect(color, x, y, w, h)`
- `Canvas.fillRect(color, x, y, w, h)`
- `Canvas.drawCircle(color, x, y, r)`
- `Canvas.fillCircle(color, x, y, r)`
- `Canvas.drawText(color, x, y, text[, size])`
- `Canvas.render()`

Resolution:

- `Canvas.width`, `Canvas.height`

Colors can be named (e.g. `"red"`), hex (`"#RRGGBB"`), or RGB strings (`"255,128,0"` or `"1.0,0.5,0.0"`).

---

## Sound API (Summary)

The `Sound` object manages named sound instances per terminal, each holding a sequence of MIDI‑style notes.

Manager functions:

- `Sound.create(name)` → create or get a sound instance
- `Sound.get(name)` → get an existing instance, or error
- `Sound.exists(name)` → `true` / `false`
- `Sound.destroy(name)` → remove an instance

Instance API (given `s = Sound.create("name")`):

- `s.addNote(pitch, duration[, velocity])` — MIDI pitch 0–127, seconds, velocity 0.0–1.0 (default 0.7)
- `s.play()`, `s.stop()`, `s.clear()`
- `s.setLoop(enabled)`
- `s.isPlaying` (read‑only bool)
- `s.loop` (read‑only bool)

Quick example:

```greyscript
print("#UI{
    bg = Sound.create(""background"")
    bg.clear()
    bg.addNote(60, 0.5)
    bg.addNote(64, 0.5)
    bg.addNote(67, 0.5)
    bg.setLoop(true)
    bg.play()
}")
```

---

## Security & Limits

To keep things safe and responsive, the VM enforces several limits:

- **Maximum sounds per terminal**: 100 sound instances (prevents resource exhaustion)
- **Maximum notes per sound instance**: 1000 notes (prevents memory abuse)
- Each terminal can have multiple independent sound instances
- Sounds play sequentially (no polyphony)
- Only simple sine wave synthesis (lightweight)
- **Variables per context**: max **500** variables (prevents unbounded memory growth)
- **Maximum string length**: max **204,800** characters (~200 KB)
- **Maximum loop iterations per execution**: max **100,000** iterations per `Execute` call
- **Maximum execution time per block**: hard time budget of **1,000 ms** per `Execute` call

Additionally:

- `Canvas.setSize` has a **cooldown per terminal** (10 seconds) to prevent resize spam.
- Each `print("#UI{ ... }")` is executed in its own VM run; previous run can be stopped cleanly.

If a script exceeds a limit, execution is aborted and an error is logged to BepInEx.

---

## Examples

The `examples` directory contains complete game implementations showcasing the canvas API:

- **[todo-app](examples/todo-app)** - Simple terminal-based todo list demonstrating basic UI layout and state persistence
- **[snake-game](examples/snake-game)** - Classic snake game with real-time rendering and dual-terminal input system
- **[raycast-fps](examples/raycast-fps)** - 2.5D first-person shooter with raycasting engine, enemy AI, and smooth movement controls

Each example includes its own README with detailed setup instructions and implementation notes.

---

## Building From Source

Project layout (high level):

- `src` — plugin source and project files
- `tests` — automated tests for the VM and scripting engine

Build commands (from the `src` directory):

```bash
# BepInEx 5
dotnet build -c Debug-BepInEx5
dotnet build -c Release-BepInEx5

# BepInEx 6
dotnet build -c Debug-BepInEx6
dotnet build -c Release-BepInEx6
```

Output DLLs land under:

- `src/bin/[Configuration]/BepInEx5/netstandard2.1/`
- `src/bin/[Configuration]/BepInEx6/netstandard2.1/`

You can run tests from the `tests` folder:

```bash
dotnet test
```

---

## License

This project is licensed under the MIT License. See `LICENSE` for details.